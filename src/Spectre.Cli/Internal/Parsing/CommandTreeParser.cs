using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Cli.Internal.Exceptions;
using Spectre.Cli.Internal.Modelling;

namespace Spectre.Cli.Internal.Parsing
{
    internal class CommandTreeParser
    {
        private readonly CommandModel _configuration;
        private readonly CommandOptionAttribute _help;

        public enum Mode
        {
            Normal = 0,
            Remaining = 1
        }

        public CommandTreeParser(CommandModel configuration)
        {
            _configuration = configuration;
            _help = new CommandOptionAttribute("-h|--help");
        }

        public (CommandTree tree, IRemainingArguments remaining) Parse(IEnumerable<string> args)
        {
            var context = new CommandTreeParserContext(args);
            var (tokens, rawRemaining) = CommandTreeTokenizer.Tokenize(context.Arguments);

            var result = (CommandTree)null;
            if (tokens.Count > 0)
            {
                // Not a command?
                var token = tokens.Current;
                if (token.TokenKind != CommandTreeToken.Kind.String)
                {
                    // Got a default command?
                    if (_configuration.DefaultCommand != null)
                    {
                        result = ParseCommandParameters(context, _configuration.DefaultCommand, null, tokens);
                        return (result, new RemainingArguments(context.GetRemainingArguments(), rawRemaining));
                    }

                    // Show help?
                    if (_help != null)
                    {
                        if (_help.ShortName?.Equals(token.Value, StringComparison.Ordinal) == true ||
                            _help.LongName?.Equals(token.Value, StringComparison.Ordinal) == true)
                        {
                            return (null, new RemainingArguments(context.GetRemainingArguments(), rawRemaining));
                        }
                    }

                    // Unexpected option.
                    throw ParseException.UnexpectedOption(context.Arguments, token);
                }

                // Does the token value match a command?
                var command = _configuration.FindCommand(token.Value);
                if (command == null)
                {
                    if (_configuration.DefaultCommand != null)
                    {
                        result = ParseCommandParameters(context, _configuration.DefaultCommand, null, tokens);
                        return (result, new RemainingArguments(context.GetRemainingArguments(), rawRemaining));
                    }
                }

                // Parse the command.
                result = ParseCommand(context, _configuration, null, tokens);
            }
            else
            {
                // Is there a default command?
                if (_configuration.DefaultCommand != null)
                {
                    result = ParseCommandParameters(context, _configuration.DefaultCommand, null, tokens);
                }
            }

            return (result, new RemainingArguments(context.GetRemainingArguments(), rawRemaining));
        }

        private CommandTree ParseCommand(
            CommandTreeParserContext context,
            ICommandContainer current,
            CommandTree parent,
            CommandTreeTokenStream stream)
        {
            // Find the command.
            var commandToken = stream.Consume(CommandTreeToken.Kind.String);
            var command = current.FindCommand(commandToken.Value);
            if (command == null)
            {
                throw ParseException.UnknownCommand(_configuration, parent, context.Arguments, commandToken);
            }

            return ParseCommandParameters(context, command, parent, stream);
        }

        private CommandTree ParseCommandParameters(
            CommandTreeParserContext context,
            CommandInfo command,
            CommandTree parent,
            CommandTreeTokenStream stream)
        {
            context.ResetArgumentPosition();

            var node = new CommandTree(parent, command);
            while (stream.Peek() != null)
            {
                var token = stream.Peek();
                switch (token.TokenKind)
                {
                    case CommandTreeToken.Kind.LongOption:
                        // Long option
                        ParseOption(context, stream, token, node, true);
                        break;
                    case CommandTreeToken.Kind.ShortOption:
                        // Short option
                        ParseOption(context, stream, token, node, false);
                        break;
                    case CommandTreeToken.Kind.String:
                        // Command
                        ParseString(context, stream, node);
                        break;
                    case CommandTreeToken.Kind.Remaining:
                        // Remaining
                        stream.Consume(CommandTreeToken.Kind.Remaining);
                        context.Mode = Mode.Remaining;
                        break;
                    default:
                        throw new InvalidOperationException($"Encountered unknown token ({token.TokenKind}).");
                }
            }

            // Add unmapped parameters.
            foreach (var parameter in node.Command.Parameters)
            {
                if (node.Mapped.All(m => m.Item1 != parameter))
                {
                    node.Unmapped.Add(parameter);
                }
            }

            return node;
        }

        private void ParseString(
            CommandTreeParserContext context,
            CommandTreeTokenStream stream,
            CommandTree node)
        {
            if (context.Mode == Mode.Remaining)
            {
                stream.Consume(CommandTreeToken.Kind.String);
                return;
            }

            var token = stream.Expect(CommandTreeToken.Kind.String);

            // Command?
            var command = node.Command.FindCommand(token.Value);
            if (command != null)
            {
                if (context.Mode == Mode.Normal)
                {
                    node.Next = ParseCommand(context, node.Command, node, stream);
                }
                return;
            }

            // Current command has no arguments?
            if (!node.HasArguments())
            {
                throw ParseException.UnknownCommand(_configuration, node, context.Arguments, token);
            }

            // Argument?
            var parameter = node.FindArgument(context.CurrentArgumentPosition);
            if (parameter == null)
            {
                // No parameters left. Any commands after this?
                if (node.Command.Children.Count > 0)
                {
                    throw ParseException.UnknownCommand(_configuration, node, context.Arguments, token);
                }

                throw ParseException.CouldNotMatchArgument(context.Arguments, token);
            }

            // Yes, this was an argument.
            var value = stream.Consume(CommandTreeToken.Kind.String).Value;
            node.Mapped.Add((parameter, value));
            context.IncreaseArgumentPosition();
        }

        private void ParseOption(
            CommandTreeParserContext context,
            CommandTreeTokenStream stream,
            CommandTreeToken token,
            CommandTree node,
            bool isLongOption)
        {
            // Consume the option token.
            stream.Consume(isLongOption ? CommandTreeToken.Kind.LongOption : CommandTreeToken.Kind.ShortOption);

            if (context.Mode == Mode.Normal)
            {
                // Find the option.
                var option = node.FindOption(token.Value, isLongOption);
                if (option != null)
                {
                    node.Mapped.Add((option, ParseOptionValue(context, stream, token, node, option)));
                    return;
                }

                // Help?
                if (_help != null)
                {
                    if (_help.ShortName?.Equals(token.Value, StringComparison.Ordinal) == true ||
                        _help.LongName?.Equals(token.Value, StringComparison.Ordinal) == true)
                    {
                        node.ShowHelp = true;
                        return;
                    }
                }
            }

            if (context.Mode == Mode.Remaining)
            {
                ParseOptionValue(context, stream, token, node, null);
                return;
            }

            throw ParseException.UnknownOption(context.Arguments, token);
        }

        private static string ParseOptionValue(
            CommandTreeParserContext context,
            CommandTreeTokenStream stream,
            CommandTreeToken token,
            CommandTree current,
            CommandParameter parameter)
        {
            var value = (string)null;

            // Parse the value of the token (if any).
            var valueToken = stream.Peek();
            if (valueToken?.TokenKind == CommandTreeToken.Kind.String)
            {
                var parseValue = true;
                if (token.TokenKind == CommandTreeToken.Kind.ShortOption && token.IsGrouped)
                {
                    parseValue = false;
                }

                if (context.Mode == Mode.Normal && parseValue)
                {
                    // Is this a command?
                    if (current.Command.FindCommand(valueToken.Value) == null)
                    {
                        if (parameter != null)
                        {
                            if (parameter.ParameterKind == ParameterKind.Single)
                            {
                                value = stream.Consume(CommandTreeToken.Kind.String).Value;
                            }
                            else if (parameter.ParameterKind == ParameterKind.Flag)
                            {
                                // Flags cannot be assigned a value.
                                throw ParseException.CannotAssignValueToFlag(context.Arguments, token);
                            }
                        }
                        else
                        {
                            // Unknown parameter value.
                            value = stream.Consume(CommandTreeToken.Kind.String).Value;
                        }
                    }
                }
                else
                {
                    context.AddRemainingArgument(token.Value, parseValue ? valueToken.Value : null);
                }
            }
            else
            {
                if (context.Mode == Mode.Remaining)
                {
                    context.AddRemainingArgument(token.Value, null);
                }
            }

            // No value?
            if (context.Mode == Mode.Normal)
            {
                if (value == null && parameter != null)
                {
                    if (parameter.ParameterKind == ParameterKind.Flag)
                    {
                        value = "true";
                    }
                    else
                    {
                        switch (parameter)
                        {
                            case CommandOption option:
                                throw ParseException.OptionHasNoValue(context.Arguments, token, option);
                            default:
                                // This should not happen at all. If it does, it's because we've added a new
                                // option type which isn't a CommandOption for some reason.
                                throw new InvalidOperationException($"Found invalid parameter type '{parameter.GetType().FullName}'.");
                        }
                    }
                }
            }

            return value;
        }
    }
}
