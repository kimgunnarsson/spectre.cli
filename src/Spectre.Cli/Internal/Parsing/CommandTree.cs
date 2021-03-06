using System.Collections.Generic;
using Spectre.Cli.Internal.Exceptions;
using Spectre.Cli.Internal.Modelling;

namespace Spectre.Cli.Internal.Parsing
{
    internal sealed class CommandTree
    {
        public CommandInfo Command { get; }
        public List<(CommandParameter, string)> Mapped { get; }
        public List<CommandParameter> Unmapped { get; }
        public CommandTree Parent { get; }
        public CommandTree Next { get; set; }
        public bool ShowHelp { get; set; }

        public CommandTree(CommandTree parent, CommandInfo command)
        {
            Parent = parent;
            Command = command;
            Mapped = new List<(CommandParameter, string)>();
            Unmapped = new List<CommandParameter>();
        }

        public CommandSettings CreateSettings(ITypeResolver resolver)
        {
            if (resolver.Resolve(Command.SettingsType) is CommandSettings settings)
            {
                return settings;
            }

            throw ParseException.CouldNotCreateSettings(Command.SettingsType);
        }

        public ICommand CreateCommand(ITypeResolver resolver)
        {
            if (resolver.Resolve(Command.CommandType) is ICommand command)
            {
                return command;
            }

            throw ParseException.CouldNotCreateCommand(Command.CommandType);
        }
    }
}