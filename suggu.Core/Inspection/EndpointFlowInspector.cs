using System.Text.RegularExpressions;

namespace suggu.Core.Inspection;

public enum FlowCertainty
{
    Confirmed,
    Ambiguous,
    Unresolved,
}

public sealed record EndpointFlowStep(
    string Description,
    string FilePath,
    int Line,
    FlowCertainty Certainty,
    string? Note,
    IReadOnlyList<EndpointFlowStep> Children);

public sealed record EndpointFlowResult(
    bool Found,
    string Controller,
    string Method,
    EndpointFlowStep? Root,
    string? Error);

/// <summary>
/// Conservative source tracer for controller actions. It follows direct receiver.method calls,
/// constructor/field dependency types, and common MediatR Send(request) dispatch. Results state
/// uncertainty explicitly because runtime DI, reflection, delegates, and polymorphism are not
/// fully knowable from lightweight static inspection.
/// </summary>
public static partial class EndpointFlowInspector
{
    private const int MaximumDepth = 10;

    public static EndpointFlowResult Trace(string solutionRoot, string controllerName, string methodName)
    {
        var files = EnumerateSourceFiles(solutionRoot).ToList();
        var normalizedController = controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? controllerName
            : controllerName + "Controller";

        var controllers = FindType(files, normalizedController);
        if (controllers.Count == 0)
        {
            return new EndpointFlowResult(false, normalizedController, methodName, null,
                $"controller '{normalizedController}' was not found");
        }

        var actionCandidates = controllers
            .Select(controller =>
            {
                var source = File.ReadAllText(controller.File);
                return (Controller: controller, Source: source, Method: FindMethod(source, methodName));
            })
            .Where(candidate => candidate.Method is not null)
            .ToList();
        if (actionCandidates.Count == 0)
        {
            return new EndpointFlowResult(false, normalizedController, methodName, null,
                $"method '{methodName}' was not found in any '{normalizedController}' source file");
        }
        if (actionCandidates.Count > 1)
        {
            var locations = string.Join(Environment.NewLine, actionCandidates.Select(candidate => candidate.Controller.File));
            return new EndpointFlowResult(false, normalizedController, methodName, null,
                $"method '{methodName}' is ambiguous because it exists in multiple '{normalizedController}' source files:{Environment.NewLine}{locations}");
        }

        var action = actionCandidates[0];
        var controller = action.Controller;
        var source = action.Source;
        var method = action.Method!.Value;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{controller.File}|{methodName}"
        };
        var children = TraceCalls(files, controller.File, source, method.Body, visited, depth: 0);
        var root = new EndpointFlowStep(
            $"{normalizedController}.{methodName}",
            controller.File,
            LineOf(source, method.Start),
            FlowCertainty.Confirmed,
            children.Count == 0 ? "No traceable source calls were found in the action body." : null,
            children);

        return new EndpointFlowResult(true, normalizedController, methodName, root, null);
    }

    private static IReadOnlyList<EndpointFlowStep> TraceCalls(
        IReadOnlyList<string> files,
        string currentFile,
        string currentSource,
        string body,
        HashSet<string> visited,
        int depth)
    {
        if (depth >= MaximumDepth)
        {
            return [new EndpointFlowStep("trace depth limit reached", currentFile, 1,
                FlowCertainty.Unresolved, $"Stopped after {MaximumDepth} source-call levels.", [])];
        }

        var dependencies = ReadVariableTypes(currentSource, body, files);
        var aliases = ReadTypeAliases(currentSource);
        var steps = new List<EndpointFlowStep>();
        foreach (Match call in CallRegex().Matches(body))
        {
            var receiver = call.Groups["receiver"].Value;
            var calledMethod = call.Groups["method"].Value;
            if (IgnoredReceivers.Contains(receiver))
            {
                continue;
            }

            if (calledMethod.Equals("Send", StringComparison.Ordinal) &&
                TryReadMediatRRequest(body, call.Index, out var requestType))
            {
                steps.AddRange(TraceMediatR(files, currentFile, requestType!, visited, depth));
                continue;
            }

            dependencies.TryGetValue(receiver, out var receiverType);
            receiverType ??= char.IsUpper(receiver.FirstOrDefault()) ? receiver : null;
            if (receiverType is null)
            {
                continue;
            }
            receiverType = aliases.GetValueOrDefault(receiverType, receiverType);

            var candidates = FindImplementations(files, receiverType)
                .Select(candidate => (candidate, Method: FindMethod(File.ReadAllText(candidate.File), calledMethod)))
                .Where(item => item.Method is not null)
                .ToList();

            if (candidates.Count == 0)
            {
                // This command describes connected workspace files. Framework, package,
                // runtime-bound, and local-variable calls with no source target are omitted.
                continue;
            }

            foreach (var item in candidates)
            {
                var candidateSource = File.ReadAllText(item.candidate.File);
                var candidateMethod = item.Method!.Value;
                var key = $"{item.candidate.File}|{calledMethod}";
                var certainty = candidates.Count == 1 ? FlowCertainty.Confirmed : FlowCertainty.Ambiguous;
                var note = candidates.Count == 1
                    ? null
                    : $"{candidates.Count} implementations match {receiverType}; runtime DI decides which one executes.";
                IReadOnlyList<EndpointFlowStep> children = [];
                if (visited.Add(key))
                {
                    children = TraceCalls(files, item.candidate.File, candidateSource, candidateMethod.Body, visited, depth + 1);
                    visited.Remove(key);
                }

                steps.Add(new EndpointFlowStep(
                    $"{item.candidate.TypeName}.{calledMethod}()",
                    item.candidate.File,
                    LineOf(candidateSource, candidateMethod.Start),
                    certainty,
                    note,
                    children));
            }
        }

        return steps
            .DistinctBy(step => (step.Description, step.FilePath, step.Line))
            .ToList();
    }

    private static IReadOnlyList<EndpointFlowStep> TraceMediatR(
        IReadOnlyList<string> files,
        string currentFile,
        string requestType,
        HashSet<string> visited,
        int depth)
    {
        var handlerPattern = new Regex(
            $@"\bclass\s+(?<name>[A-Za-z_]\w*)[^{{:]*:\s*[^{{]*\bIRequestHandler\s*<\s*{Regex.Escape(requestType)}\b",
            RegexOptions.Multiline);
        var handlers = files
            .Select(file => (File: file, Source: File.ReadAllText(file)))
            .Select(item => (item.File, item.Source, Match: handlerPattern.Match(item.Source)))
            .Where(item => item.Match.Success)
            .ToList();

        if (handlers.Count == 0)
        {
            return [new EndpointFlowStep(
                $"MediatR.Send({requestType})",
                currentFile,
                1,
                FlowCertainty.Unresolved,
                "The request type was found, but no IRequestHandler implementation was located in source.",
                [])];
        }

        return handlers.Select(handler =>
        {
            var method = FindMethod(handler.Source, "Handle");
            var handlerName = handler.Match.Groups["name"].Value;
            if (method is null)
            {
                return new EndpointFlowStep($"{handlerName}.Handle({requestType})", handler.File, 1,
                    FlowCertainty.Unresolved, "Handler type found, but its Handle method could not be parsed.", []);
            }

            var key = $"{handler.File}|Handle";
            IReadOnlyList<EndpointFlowStep> children = [];
            if (visited.Add(key))
            {
                children = TraceCalls(files, handler.File, handler.Source, method.Value.Body, visited, depth + 1);
                visited.Remove(key);
            }

            return new EndpointFlowStep(
                $"{handlerName}.Handle({requestType})",
                handler.File,
                LineOf(handler.Source, method.Value.Start),
                handlers.Count == 1 ? FlowCertainty.Confirmed : FlowCertainty.Ambiguous,
                handlers.Count == 1 ? "Resolved through the MediatR IRequestHandler convention." :
                    $"{handlers.Count} request handlers were found; runtime registration determines behavior.",
                children);
        }).ToList();
    }

    private static Dictionary<string, string> ReadVariableTypes(
        string source,
        string body,
        IReadOnlyList<string> files)
    {
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match field in FieldRegex().Matches(source))
        {
            dependencies[field.Groups["name"].Value] = SimpleTypeName(field.Groups["type"].Value);
        }

        foreach (Match parameter in DependencyParameterRegex().Matches(source))
        {
            var type = SimpleTypeName(parameter.Groups["type"].Value);
            var name = parameter.Groups["name"].Value;
            dependencies.TryAdd(name, type);
            dependencies.TryAdd("_" + name.TrimStart('_'), type);
        }

        foreach (Match declaration in LocalDeclarationRegex().Matches(body))
        {
            var name = declaration.Groups["name"].Value;
            var declaredType = declaration.Groups["type"].Value;
            var expression = declaration.Groups["expression"].Value;
            var inferredType = !declaredType.Equals("var", StringComparison.Ordinal)
                ? SimpleTypeName(declaredType)
                : InferExpressionType(expression, dependencies, files);
            if (inferredType is not null) dependencies[name] = inferredType;
        }

        foreach (Match lambda in LambdaParameterRegex().Matches(body))
        {
            if (dependencies.TryGetValue(lambda.Groups["collection"].Value, out var elementType))
                dependencies[lambda.Groups["parameter"].Value] = elementType;
        }

        return dependencies;
    }

    private static string? InferExpressionType(
        string expression,
        IReadOnlyDictionary<string, string> knownTypes,
        IReadOnlyList<string> files)
    {
        var query = GenericQueryRegex().Match(expression);
        if (query.Success) return query.Groups["type"].Value;

        var construction = NewExpressionRegex().Match(expression);
        if (construction.Success) return SimpleTypeName(construction.Groups["type"].Value);

        var call = CallRegex().Match(expression);
        if (!call.Success || !knownTypes.TryGetValue(call.Groups["receiver"].Value, out var receiverType))
            return null;

        var methodName = call.Groups["method"].Value;
        foreach (var candidate in FindImplementations(files, receiverType))
        {
            var source = File.ReadAllText(candidate.File);
            var returnType = FindMethodReturnType(source, methodName);
            if (returnType is not null) return returnType;
        }
        return null;
    }

    private static string? FindMethodReturnType(string source, string methodName)
    {
        var match = new Regex(
            $@"\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?<type>[A-Za-z_]\w*(?:\s*<[^{{;=]+>)?[?]?)\s+{Regex.Escape(methodName)}\s*\(",
            RegexOptions.Multiline).Match(source);
        return match.Success ? SimpleTypeName(match.Groups["type"].Value) : null;
    }

    private static Dictionary<string, string> ReadTypeAliases(string source) =>
        TypeAliasRegex().Matches(source).Cast<Match>().ToDictionary(
            match => match.Groups["alias"].Value,
            match => match.Groups["type"].Value.Split('.').Last(),
            StringComparer.Ordinal);

    private static IReadOnlyList<TypeLocation> FindImplementations(IReadOnlyList<string> files, string typeName)
    {
        var simple = SimpleTypeName(typeName);
        var results = new List<TypeLocation>();
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (Match declaration in ClassRegex().Matches(source))
            {
                var className = declaration.Groups["name"].Value;
                var bases = declaration.Groups["bases"].Value;
                if (className.Equals(simple, StringComparison.Ordinal) ||
                    Regex.IsMatch(bases, $@"\b{Regex.Escape(simple)}\b"))
                {
                    results.Add(new TypeLocation(className, file));
                }
            }
        }
        return results;
    }

    private static IReadOnlyList<TypeLocation> FindType(IReadOnlyList<string> files, string typeName) =>
        files.SelectMany(file => ClassRegex().Matches(File.ReadAllText(file)).Cast<Match>()
                .Where(match => match.Groups["name"].Value.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                .Select(match => new TypeLocation(match.Groups["name"].Value, file)))
            .ToList();

    private static MethodBody? FindMethod(string source, string methodName)
    {
        var declaration = new Regex(
            $@"\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[^;{{}}=]+?\b{Regex.Escape(methodName)}\s*\(",
            RegexOptions.Multiline).Match(source);
        if (!declaration.Success)
        {
            return null;
        }

        var openParen = source.IndexOf('(', declaration.Index);
        var closeParen = FindMatching(source, openParen, '(', ')');
        if (closeParen < 0)
        {
            return null;
        }

        var cursor = closeParen + 1;
        while (cursor < source.Length && char.IsWhiteSpace(source[cursor])) cursor++;
        if (source.AsSpan(cursor).StartsWith("=>"))
        {
            var semicolon = source.IndexOf(';', cursor + 2);
            return semicolon < 0 ? null : new MethodBody(declaration.Index, source[(cursor + 2)..semicolon]);
        }

        var openBrace = source.IndexOf('{', cursor);
        if (openBrace < 0)
        {
            return null;
        }
        var closeBrace = FindMatching(source, openBrace, '{', '}');
        return closeBrace < 0 ? null : new MethodBody(declaration.Index, source[(openBrace + 1)..closeBrace]);
    }

    private static int FindMatching(string source, int start, char open, char close)
    {
        var depth = 0;
        for (var index = start; index < source.Length; index++)
        {
            if (source[index] == open) depth++;
            else if (source[index] == close && --depth == 0) return index;
        }
        return -1;
    }

    private static bool TryReadMediatRRequest(string body, int callIndex, out string? requestType)
    {
        var tail = body[callIndex..];
        var match = MediatRRequestRegex().Match(tail);
        requestType = match.Success ? match.Groups["request"].Value : null;
        return match.Success;
    }

    private static int LineOf(string source, int index) =>
        1 + source.AsSpan(0, Math.Clamp(index, 0, source.Length)).Count('\n');

    private static string SimpleTypeName(string type) =>
        type.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Split('<', '[', '?')[0].Split('.').Last().Trim();

    private static IEnumerable<string> EnumerateSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var normalized = file.Replace('\\', '/');
                return !normalized.Contains("/bin/") &&
                    !normalized.Contains("/obj/") &&
                    !normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) &&
                    !Regex.IsMatch(normalized, @"/(?:[^/]+\.)?(?:Tests|IntegrationTests)/", RegexOptions.IgnoreCase);
            });

    private static readonly HashSet<string> IgnoredReceivers = new(StringComparer.Ordinal)
    {
        "Console", "Math", "string", "DateTime", "DateTimeOffset", "Guid", "Task", "Enumerable",
    };

    private sealed record TypeLocation(string TypeName, string File);
    private readonly record struct MethodBody(int Start, string Body);

    [GeneratedRegex(@"\b(?<receiver>_?[A-Za-z_]\w*)\s*\.\s*(?<method>[A-Za-z_]\w*)\s*\(")]
    private static partial Regex CallRegex();

    [GeneratedRegex(@"\b(?:private|protected|internal|public)\s+(?:static\s+)?(?:readonly\s+)?(?<type>(?:global::)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\s*<[^;=]+>)?[?]?)\s+(?<name>_?[a-z][A-Za-z0-9_]*)\s*[;=]")]
    private static partial Regex FieldRegex();

    [GeneratedRegex(@"(?<type>(?:global::)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\s*<[^,)]+>)?[?]?)\s+(?<name>[a-z_][A-Za-z0-9_]*)\s*(?=[,)=])")]
    private static partial Regex DependencyParameterRegex();

    [GeneratedRegex(@"\b(?:using\s+)?(?<type>var|[A-Za-z_]\w*(?:\s*<[^;=]+>)?[?]?)\s+(?<name>[a-z_][A-Za-z0-9_]*)\s*=\s*(?<expression>[^;]+);")]
    private static partial Regex LocalDeclarationRegex();

    [GeneratedRegex(@"\bQuery[A-Za-z0-9_]*\s*<\s*(?<type>[A-Za-z_]\w*)\s*>")]
    private static partial Regex GenericQueryRegex();

    [GeneratedRegex(@"\bnew\s+(?<type>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)")]
    private static partial Regex NewExpressionRegex();

    [GeneratedRegex(@"\b(?<collection>[a-z_][A-Za-z0-9_]*)\s*\.\s*(?:Select|Where|SelectMany)\s*\(\s*(?<parameter>[a-z_][A-Za-z0-9_]*)\s*=>")]
    private static partial Regex LambdaParameterRegex();

    [GeneratedRegex(@"\busing\s+(?<alias>[A-Za-z_]\w*)\s*=\s*(?<type>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+)\s*;")]
    private static partial Regex TypeAliasRegex();

    [GeneratedRegex(@"\bclass\s+(?<name>[A-Za-z_]\w*)(?:\s*\([^)]*\))?(?:\s*:\s*(?<bases>[^\{]+))?\s*\{")]
    private static partial Regex ClassRegex();

    [GeneratedRegex(@"\.\s*Send\s*\(\s*(?:await\s+)?(?:new\s+)?(?<request>[A-Za-z_]\w*)")]
    private static partial Regex MediatRRequestRegex();
}
