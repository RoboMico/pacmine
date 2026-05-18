using Xunit;
using Pacmine.Core;

namespace Pacmine.PackageCraft.Tests;

/// <summary>
/// PackageBuilder has a private constructor and can only be created via
/// PackageBuilder.CreateAsync(script) which processes a Lua script.
/// These tests cover what can be tested without instantiating PackageBuilder
/// directly — specifically the Lua object conversion layer and GlobalFunctions.
/// Full PackageBuilder integration tests require a Lua runtime and are
/// better suited for an integration test suite.
/// </summary>
public class PackageBuilderTests
{
    // PackageBuilder cannot be instantiated directly (private constructor).
    // Tests for configuration methods like ConfigureWorkingDirectory(),
    // ConfigureSourceDirectory(), etc. would need an instance created via
    // CreateAsync() with a valid Lua script, which crosses into integration
    // test territory. The unit-testable components are covered in
    // LuaObjectConversionTests and FilesysLuaLibraryTests.
}
