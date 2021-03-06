﻿using Sample.Commands;
using Sample.Commands.Settings;
using Spectre.Cli;

namespace Sample
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<BuildCommand>("build");
                config.AddBranch<AddSettings>("add", add =>
                {
                    add.SetDescription("Add reference to the project.");

                    add.AddCommand<AddPackageCommand>("package");
                    add.AddCommand<AddReferenceCommand>("reference");
                });
            });

            return app.Run(args);
        }
    }
}
