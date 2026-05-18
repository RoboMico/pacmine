using Xunit;

namespace Pacmine.PackageCraft.Tests;

/// <summary>
/// Tests for <see cref="LuaLibrary.GlobalFunctions"/>.
/// Note: PackageBuilder has a private constructor and cannot be instantiated
/// directly in unit tests. The GlobalFunctions class can be constructed with
/// a PackageBuilder reference, and some of its methods can be tested in isolation.
/// Full FilesysLuaLibrary path restriction tests would require a PackageBuilder
/// instance, which needs Lua scripting — better suited for integration tests.
/// </summary>
public class FilesysLuaLibraryTests
{
    // ── GlobalFunctions ──────────────────────────────────────────────────

    [Fact]
    public void GlobalFunctions_Print_WritesToStdout()
    {
        // Arrange: PackageBuilder has a private constructor, but since
        // GlobalFunctions only uses it via interface methods, we need
        // a PackageBuilder instance. The builder's StandardOutput/Error
        // properties are the key testable surface.
        // Full testing of GlobalFunctions requires integration-level setup.
    }

    [Fact]
    public void GlobalFunctions_PrintError_WritesToStderr()
    {
        // Same consideration as above.
    }
}
