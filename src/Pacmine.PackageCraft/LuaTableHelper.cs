using Lua;

namespace Pacmine.PackageCraft;

/// <summary>
/// Helper methods for conversion between Lua tables and C# data structures.
/// </summary>
public static class LuaTableHelper
{
    /// <summary>
    /// Converts a Lua table (treated as a 1-indexed array) to a <see cref="List{T}"/>.
    /// </summary>
    public static List<T> LuaArrayToList<T>(LuaTable table)
    {
        List<T> list = [];
        foreach (var pair in table)
        {
            list.Add(pair.Value.Read<T>());
        }
        return list;
    }

    /// <summary>
    /// Converts a <see cref="List{T}"/> to a Lua table (1-indexed array).
    /// </summary>
    public static LuaTable ListToLuaArray<T>(List<T> list)
    {
        LuaTable table = [];
        for (int i = 0; i < list.Count; i++)
        {
            // index in lua arrays starts from 1
            table[i + 1] = LuaValue.FromObject(list[i]!);
        }
        return table;
    }

    /// <summary>
    /// Converts a Lua table (treated as a string-keyed dictionary) to a <see cref="Dictionary{TKey, TValue}"/>.
    /// Each Lua key is read as a string, and each value is read as a string then converted via <paramref name="valueFactory"/>.
    /// </summary>
    public static Dictionary<string, TValue> LuaTableToDictionary<TValue>(
        LuaTable table,
        Func<string, TValue> valueFactory)
    {
        Dictionary<string, TValue> dict = [];
        foreach (var item in table)
        {
            dict[item.Key.Read<string>()] = valueFactory(item.Value.Read<string>());
        }
        return dict;
    }

    /// <summary>
    /// Converts a <see cref="Dictionary{TKey, TValue}"/> to a Lua table with string keys.
    /// Each value is converted to a string via <paramref name="valueFormatter"/>.
    /// </summary>
    public static LuaTable DictionaryToLuaTable<TValue>(
        Dictionary<string, TValue> dict,
        Func<TValue, string> valueFormatter)
    {
        LuaTable table = [];
        foreach (var (key, value) in dict)
        {
            table[key] = valueFormatter(value);
        }
        return table;
    }
}