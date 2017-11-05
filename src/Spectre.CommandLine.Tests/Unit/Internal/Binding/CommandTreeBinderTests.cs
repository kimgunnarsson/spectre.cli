using System;
using System.Collections.Generic;
using Shouldly;
using Spectre.CommandLine.Internal;
using Spectre.CommandLine.Internal.Configuration;
using Spectre.CommandLine.Internal.Modelling;
using Spectre.CommandLine.Internal.Parsing;
using Spectre.CommandLine.Tests.Data;
using Xunit;

namespace Spectre.CommandLine.Tests.Unit.Internal.Binding
{
    public sealed class CommandTreeBinderTests
    {
        /// <remarks>
        /// https://github.com/spectresystems/spectre.commandline/wiki/Test-cases#test-case-1
        /// </remarks>
        [Fact]
        public void Should_Bind_Parameters_Correctly_For_Case_1()
        {
            // Given, When
            var settings = Fixture.Bind<DogSettings>(
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
            settings.Age.ShouldBe(12);
            settings.GoodBoy.ShouldBe(true);
            settings.Name.ShouldBe("Rufus");
            settings.IsAlive.ShouldBe(true);
        }

        /// <remarks>
        /// https://github.com/spectresystems/spectre.commandline/wiki/Test-cases#test-case-2
        /// </remarks>
        [Fact]
        public void Should_Bind_Parameters_Correctly_For_Case_2()
        {
            // Given, When
            var settings = Fixture.Bind<DogSettings>(
                new[] { "dog", "12", "--good-boy", "--name", "Rufus", "--alive" },
                config =>
                {
                    config.AddCommand<DogCommand>("dog");
                });

            // Then
            settings.Age.ShouldBe(12);
            settings.GoodBoy.ShouldBe(true);
            settings.Name.ShouldBe("Rufus");
            settings.IsAlive.ShouldBe(true);
        }

        /// <remarks>
        /// https://github.com/spectresystems/spectre.commandline/wiki/Test-cases#test-case-3
        /// </remarks>
        [Fact]
        public void Should_Bind_Parameters_Correctly_For_Case_3()
        {
            // Given, When
            var settings = Fixture.Bind<DogSettings>(
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
            settings.Age.ShouldBe(12);
            settings.GoodBoy.ShouldBe(true);
            settings.Name.ShouldBe("Rufus");
            settings.IsAlive.ShouldBe(false);
        }

        internal static class Fixture
        {
            public static T Bind<T>(IEnumerable<string> args, Action<Configurator> action)
                where T : class, new()
            {
                // Configure
                var configurator = new Configurator();
                action(configurator);

                // Parse command tree.
                var parser = new CommandTreeParser(CommandModelBuilder.Build(configurator));
                var result = parser.Parse(args);

                // Bind the settings to the tree.
                object settings = new T();
                CommandBinder.Bind(result.tree, ref settings);

                // Return the settings.
                return (T)settings;
            }
        }
    }
}
