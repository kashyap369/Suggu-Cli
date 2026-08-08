using suggu.Core.Inspection;

namespace suggu.Tests.Inspection;

public sealed class BuildInspectorTests
{
    [Fact]
    public void Parses_compiler_and_project_diagnostics()
    {
        var output = """
            D:\Shop\Order.cs(12,8): error CS0246: The type or namespace name 'Thing' could not be found [D:\Shop\Shop.csproj]
            D:\Shop\Shop.csproj : warning NU1900: Package vulnerability data unavailable [D:\Shop\Shop.sln]
            """;

        var diagnostics = BuildInspector.ParseDiagnostics(output);

        Assert.Equal(2, diagnostics.Count);
        Assert.Equal(BuildDiagnosticSeverity.Error, diagnostics[0].Severity);
        Assert.Equal("CS0246", diagnostics[0].Code);
        Assert.Equal(12, diagnostics[0].Line);
        Assert.Equal(8, diagnostics[0].Column);
        Assert.Equal(BuildDiagnosticSeverity.Warning, diagnostics[1].Severity);
        Assert.Equal("NU1900", diagnostics[1].Code);
    }
}
