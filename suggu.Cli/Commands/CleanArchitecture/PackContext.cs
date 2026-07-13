using suggu.Core.Packs;

namespace suggu.Cli.Commands.CleanArchitecture;

/// <summary>
/// The pack loaded once at startup and shared with commands. Kept deliberately tiny;
/// if command dependencies grow beyond this, switch to Spectre's TypeRegistrar DI instead.
/// </summary>
internal static class PackContext
{
    private static (PackManifest Manifest, IPackFileProvider Files)? _current;

    public static (PackManifest Manifest, IPackFileProvider Files) Current =>
        _current ??= PackLoader.LoadDefault();
}
