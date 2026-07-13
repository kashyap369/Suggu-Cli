using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace suggu.Cli.Commands.Common
{
    internal sealed class CreateFolderCommand : Command<CreateFolderCommand.Settings>
    {

        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<names>")]
            [Description("one or more folder names")]
            public string[] Names { get; init; } = [];
            [CommandOption("-p|--path <PATH>")]
            [Description("where to create them default to current directory")]
            public string? Path { get; init; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var basePath = settings.Path ?? Directory.GetCurrentDirectory();
            foreach (var name in settings.Names)
            {
                var full = System.IO.Path.Combine(basePath, name);
                if (Directory.Exists(full))
                {
                    AnsiConsole.MarkupLine($"[yellow]skipped[/] {name} (folder already exists)");
                    continue;
                }
                Directory.CreateDirectory(full);
                // check + word both green
                AnsiConsole.MarkupLine($"[green]✓ created[/] {name}");
            }
            return 0;
        }


    }
}
