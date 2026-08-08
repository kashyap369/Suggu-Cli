namespace suggu.Core.Rulebooks;

public static class RulebookTemplate
{
    public static string Content => """
        # Suggu Rulebook

        <!--
        This file defines project-specific composite commands without changing Suggu itself.

        1. Replace the project aliases below with real project names from this workspace.
        2. Add or edit commands and their ordered actions.
        3. Validate with:  suggu --rulebook --check
        4. Preview with:   suggu --rulebook add entity Book --dry-run
        5. Execute with:   suggu --rulebook add entity Book

        Supported v1 action commands:
          create solution, create project, create folder, create file, add class,
          add interface, add controller, add json, add reference, and add package.

        Project paths are relative to the workspace; artifact paths are relative to their
        selected project. Omit "project" from create folder/file/json to target the workspace.
        create project types: webapi, mvc, console, classlib, and xunit.
        Templates are arrays of lines.
        Set "template": "template-name" on a file-producing action to use one.
        Available placeholders include command parameters plus {{Namespace}}, {{TypeName}},
        {{ProjectName}}, and {{SolutionName}}. Suggu never executes shell commands from a rulebook.

        The starter recipe below creates an entity, repository interface, and configuration
        class. Update the domain/infrastructure aliases before running it.
        -->

        <!-- suggu-rulebook:start -->
        ```json
        {
          "schema": "suggu/v1",
          "projects": {
            "domain": "YourProject.Domain",
            "infrastructure": "YourProject.Infra"
          },
          "commands": [
            {
              "name": "add entity",
              "description": "Create an entity and its related persistence files",
              "parameters": [
                { "name": "Name", "required": true, "type": "csharp-identifier" }
              ],
              "actions": [
                {
                  "command": "add class",
                  "project": "domain",
                  "path": "Entities",
                  "name": "{{Name}}"
                },
                {
                  "command": "add interface",
                  "project": "domain",
                  "path": "Repositories",
                  "name": "{{Name}}Repository"
                },
                {
                  "command": "add class",
                  "project": "infrastructure",
                  "path": "Persistence/Configurations",
                  "name": "{{Name}}Configuration"
                }
              ]
            }
          ],
          "templates": {
            "example-custom-class": [
              "namespace {{Namespace}};",
              "",
              "public sealed class {{TypeName}}",
              "{",
              "}"
            ]
          }
        }
        ```
        <!-- suggu-rulebook:end -->

        ## Notes

        Add human-readable architecture decisions below this line. Suggu only parses the
        marked JSON block above, so normal Markdown notes are safe here.
        """ + Environment.NewLine;
}
