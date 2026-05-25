using Xunit;
using Lua;
using Pacmine.Core;

namespace Pacmine.PackageCraft.Tests;

public class LuaTableHelperTests
{
    // ── List / Array ─────────────────────────────────────────────────────

    [Fact]
    public void ListToLuaArray_Roundtrip_Strings()
    {
        var original = new List<string> { "a", "b", "c" };
        var table = LuaTableHelper.ListToLuaArray(original);
        var restored = LuaTableHelper.LuaArrayToList<string>(table);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void ListToLuaArray_EmptyList_ProducesEmptyTable()
    {
        var original = new List<string>();
        var table = LuaTableHelper.ListToLuaArray(original);
        var restored = LuaTableHelper.LuaArrayToList<string>(table);

        Assert.Empty(restored);
    }

    [Fact]
    public void ListToLuaArray_IsOneIndexed()
    {
        var list = new List<string> { "first", "second" };
        var table = LuaTableHelper.ListToLuaArray(list);

        Assert.Equal("first", table[1].Read<string>());
        Assert.Equal("second", table[2].Read<string>());
    }

    // ── Dictionary ───────────────────────────────────────────────────────

    [Fact]
    public void DictionaryToLuaTable_Roundtrip_VersionRange()
    {
        var original = new Dictionary<string, VersionRange>
        {
            { "dep-a", new VersionRange("^1.0.0") },
            { "dep-b", new VersionRange("~2.0.0") }
        };

        var table = LuaTableHelper.DictionaryToLuaTable(original, v => v.ToString());
        var restored = LuaTableHelper.LuaTableToDictionary(table, s => new VersionRange(s));

        Assert.Equal(2, restored.Count);
        Assert.True(restored["dep-a"].Contains(new VersionIdentifier("1.5.0")));
        Assert.True(restored["dep-b"].Contains(new VersionIdentifier("2.0.5")));
    }

    [Fact]
    public void DictionaryToLuaTable_Roundtrip_VersionIdentifier()
    {
        var original = new Dictionary<string, VersionIdentifier>
        {
            { "virtual-a", new VersionIdentifier("1.0.0") },
            { "virtual-b", new VersionIdentifier("2.0.0-beta") }
        };

        var table = LuaTableHelper.DictionaryToLuaTable(original, v => v.ToString());
        var restored = LuaTableHelper.LuaTableToDictionary(table, s => new VersionIdentifier(s));

        Assert.Equal(2, restored.Count);
        Assert.Equal("1.0.0", restored["virtual-a"].RawString);
        Assert.Equal("2.0.0-beta", restored["virtual-b"].RawString);
    }

    [Fact]
    public void DictionaryToLuaTable_EmptyDict_ProducesEmptyTable()
    {
        var original = new Dictionary<string, string>();
        var table = LuaTableHelper.DictionaryToLuaTable(original, v => v);
        var restored = LuaTableHelper.LuaTableToDictionary(table, s => s);

        Assert.Empty(restored);
    }
}
