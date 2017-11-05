﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Shouldly;
using Spectre.CommandLine.Internal.Configuration;
using Spectre.CommandLine.Internal.Modelling;
using Spectre.CommandLine.Internal.Parsing;
using Spectre.CommandLine.Tests.Data;
using Xunit;

namespace Spectre.CommandLine.Tests.Unit.Internal.Parsing
{
    public sealed class CommandTreeParserTests
    {
        [Fact]
        public void Should_Capture_Remaining_Arguments()
        {
            // Given, When
            var result = Fixture.Parse(new[] { "dog", "--woof" }, config =>
            {
                config.AddCommand<DogCommand>("dog");
            });

            // Then
            result.remaining.Count.ShouldBe(1);
            result.remaining.Contains("--woof").ShouldBe(true);
        }

        [Fact]
        public void Should_Capture_Option_Beloning_To_Parent_Commands_As_Remaining_Arguments()
        {
            // Given, When
            var result = Fixture.Parse(new[] { "animal", "dog", "--alive" }, config =>
            {
                config.AddCommand<AnimalSettings>("animal", animal =>
                {
                    animal.AddCommand<DogCommand>("dog");
                });
            });

            // Then
            result.remaining.Count.ShouldBe(1);
            result.remaining.Contains("--alive").ShouldBe(true);
        }

        [Fact]
        public void Should_Not_Capture_Option_Belonging_To_Parent_Commands_As_Remaining_Arguments_If_Option_Was_Assigned_To_Parent_Command()
        {
            // Given, When
            var result = Fixture.Parse(new[] { "animal", "--alive", "dog", "--alive" }, config =>
            {
                config.AddCommand<AnimalSettings>("animal", animal =>
                {
                    animal.AddCommand<DogCommand>("dog");
                });
            });

            // Then
            result.remaining.Count.ShouldBe(0);
        }

        /// <remarks>
        /// https://github.com/spectresystems/spectre.commandline/wiki/Test-cases#test-case-1
        /// </remarks>
        [Theory]
        [EmbeddedResourceData("Spectre.CommandLine.Tests/Data/Resources/Parsing/case1.xml")]
        public void Should_Parse_Correct_Tree_For_Case_1(string expected)
        {
            // Given, When
            var result = Fixture.Serialize(
                new[] { "animal", "--alive", "mammal", "--name", "Rufus", "dog", "12", "--good-boy" },
                config =>
                {
                    config.AddCommand<AnimalSettings>("animal", animal =>
                    {
                        animal.AddCommand<MammalSettings>("mammal", mammal =>
                        {
                            mammal.AddCommand<DogCommand>("dog");
                            mammal.AddCommand<HorseCommand>("horse");
                        });
                    });
                });

            // Then
            result.ShouldBe(expected);
        }

        /// <remarks>
        /// https://github.com/spectresystems/spectre.commandline/wiki/Test-cases#test-case-2
        /// </remarks>
        [Theory]
        [EmbeddedResourceData("Spectre.CommandLine.Tests/Data/Resources/Parsing/case2.xml")]
        public void Should_Parse_Correct_Tree_For_Case_2(string expected)
        {
            // Given, When
            var result = Fixture.Serialize(
                new[] { "dog", "12", "--good-boy", "--name", "Rufus", "--alive" },
                config =>
                {
                    config.AddCommand<DogCommand>("dog");
                });

            // Then
            result.ShouldBe(expected);
        }

        /// <remarks>
        /// https://github.com/spectresystems/spectre.commandline/wiki/Test-cases#test-case-3
        /// </remarks>
        [Theory]
        [EmbeddedResourceData("Spectre.CommandLine.Tests/Data/Resources/Parsing/case3.xml")]
        public void Should_Parse_Correct_Tree_For_Case_3(string expected)
        {
            // Given, When
            var result = Fixture.Serialize(
                new[] { "animal", "dog", "12", "--good-boy", "--name", "Rufus" },
                config =>
            {
                config.AddCommand<AnimalSettings>("animal", animal =>
                {
                    animal.AddCommand<DogCommand>("dog");
                    animal.AddCommand<HorseCommand>("horse");
                });
            });

            // Then
            result.ShouldBe(expected);
        }

        private static class Fixture
        {
            public static (CommandTree tree, ILookup<string, string> remaining) Parse(IEnumerable<string> args, Action<Configurator> func)
            {
                var configurator = new Configurator();
                func(configurator);

                var model = CommandModelBuilder.Build(configurator);
                return new CommandTreeParser(model).Parse(args);
            }

            public static string Serialize(IEnumerable<string> args, Action<Configurator> func)
            {
                var result = Parse(args, func);

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\n",
                    OmitXmlDeclaration = false
                };

                using (var buffer = new StringWriter())
                using (var xmlWriter = XmlWriter.Create(buffer, settings))
                {
                    CommandTreeSerializer.Serialize(result.tree).WriteTo(xmlWriter);
                    xmlWriter.Flush();
                    return buffer.GetStringBuilder().ToString().NormalizeLineEndings();
                }
            }
        }
    }
}
