using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using static suggu.Cli.Commands.Common.ListFolderCommand;

namespace suggu.Cli.Commands.Common
{
    internal sealed class ListFolderCommand : Command<ListFolderCommand.Settings>
    {

        public sealed class Settings : CommandSettings
        {
            [CommandOption("-p|--path <PATH>")]
            [Description("Directory to list. Defaults to current directory.")]
            public string? Path { get; init; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var basePath = settings.Path ?? Directory.GetCurrentDirectory();

            if (!Directory.Exists(basePath))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] path not found: {basePath}");
                return 1;
            }

            var folders = Directory.GetDirectories(basePath);

            if (folders.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]–[/] no folders here");
                return 0;
            }

            foreach (var folder in folders)
            {
                var name = System.IO.Path.GetFileName(folder);
                AnsiConsole.MarkupLine($"[blue]📁[/] {name}");
            }

            return 0;
        }
    }
}
