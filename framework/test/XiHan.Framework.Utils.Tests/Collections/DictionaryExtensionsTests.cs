// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 字典扩展方法测试
/// </summary>
public class DictionaryExtensionsTests
{
    /// <summary>
    /// 命中时返回值，未命中时返回默认值
    /// </summary>
    [Fact]
    public void GetOrDefault_OnDictionary_ReturnsValueOrDefault()
    {
        var dictionary = new Dictionary<string, int> { ["a"] = 1 };

        Assert.Equal(1, dictionary.GetOrDefault("a"));
        Assert.Equal(0, dictionary.GetOrDefault("missing"));
    }

    /// <summary>
    /// 引用类型未命中时返回 null
    /// </summary>
    [Fact]
    public void GetOrDefault_WhenValueIsReferenceType_ReturnsNullOnMiss()
    {
        var dictionary = new Dictionary<int, string> { [1] = "one" };

        Assert.Equal("one", dictionary.GetOrDefault(1));
        Assert.Null(dictionary.GetOrDefault(2));
    }

    /// <summary>
    /// 接口静态类型走 IDictionary 重载，语义一致
    /// </summary>
    [Fact]
    public void GetOrDefault_OnInterfaceTypedDictionary_ReturnsValueOrDefault()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int> { ["a"] = 1 };

        Assert.Equal(1, dictionary.GetOrDefault("a"));
        Assert.Equal(0, dictionary.GetOrDefault("missing"));
    }

    /// <summary>
    /// 只读字典重载语义一致
    /// </summary>
    [Fact]
    public void GetOrDefault_OnReadOnlyDictionary_ReturnsValueOrDefault()
    {
        IReadOnlyDictionary<string, int> dictionary = new Dictionary<string, int> { ["a"] = 1 };

        Assert.Equal(1, dictionary.GetOrDefault("a"));
        Assert.Equal(0, dictionary.GetOrDefault("missing"));
    }

    /// <summary>
    /// 并发字典重载语义一致
    /// </summary>
    [Fact]
    public void GetOrDefault_OnConcurrentDictionary_ReturnsValueOrDefault()
    {
        var dictionary = new ConcurrentDictionary<string, int>();
        dictionary["a"] = 1;

        Assert.Equal(1, dictionary.GetOrDefault("a"));
        Assert.Equal(0, dictionary.GetOrDefault("missing"));
    }

    /// <summary>
    /// 未命中时用工厂创建并写回字典，命中时不再调用工厂
    /// </summary>
    [Fact]
    public void GetOrAdd_WithKeyFactory_CreatesOnceAndCachesValue()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();
        var calls = 0;

        var first = dictionary.GetOrAdd("a", key =>
        {
            calls++;
            return key.Length + 10;
        });
        var second = dictionary.GetOrAdd("a", key =>
        {
            calls++;
            return key.Length + 20;
        });

        Assert.Equal(11, first);
        Assert.Equal(11, second);
        Assert.Equal(1, calls);
        Assert.Single(dictionary);
    }

    /// <summary>
    /// 无参工厂重载同样只在未命中时调用
    /// </summary>
    [Fact]
    public void GetOrAdd_WithValueFactory_CreatesOnce()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();
        var calls = 0;

        var first = dictionary.GetOrAdd("a", () =>
        {
            calls++;
            return 7;
        });
        var second = dictionary.GetOrAdd("a", () =>
        {
            calls++;
            return 8;
        });

        Assert.Equal(7, first);
        Assert.Equal(7, second);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// 并发字典的无参工厂重载语义一致
    /// </summary>
    [Fact]
    public void GetOrAdd_OnConcurrentDictionary_CreatesOnce()
    {
        var dictionary = new ConcurrentDictionary<string, int>();

        var first = dictionary.GetOrAdd("a", () => 7);
        var second = dictionary.GetOrAdd("a", () => 8);

        Assert.Equal(7, first);
        Assert.Equal(7, second);
    }

    /// <summary>
    /// 转换为动态对象后仍可按键读取
    /// </summary>
    [Fact]
    public void ConvertToDynamicObject_KeepsAllEntries()
    {
        var source = new Dictionary<string, object>
        {
            ["name"] = "曦寒",
            ["count"] = 2
        };

        object converted = source.ConvertToDynamicObject();

        var asDictionary = (IDictionary<string, object?>)converted;
        Assert.Equal(2, asDictionary.Count);
        Assert.Equal("曦寒", asDictionary["name"] as string);
        Assert.Equal(2, (int)asDictionary["count"]!);
    }

    /// <summary>
    /// 空字典转换后得到空的动态对象
    /// </summary>
    [Fact]
    public void ConvertToDynamicObject_WhenEmpty_ReturnsEmptyObject()
    {
        object converted = new Dictionary<string, object>().ConvertToDynamicObject();

        var asDictionary = (IDictionary<string, object?>)converted;
        Assert.Empty(asDictionary);
    }

    /// <summary>
    /// 查询串按键的序数序排序，并跳过空白值
    /// </summary>
    [Fact]
    public void BuildQueryString_SortsByKeyAndSkipsBlankValues()
    {
        var parameters = new Dictionary<string, string>
        {
            ["b"] = "2",
            ["a"] = " 1 ",
            ["c"] = "   ",
            ["d"] = null!
        };

        var query = parameters.BuildQueryString();

        Assert.Equal("a=1&b=2", query);
    }

    /// <summary>
    /// 全部值为空白时返回空串
    /// </summary>
    [Fact]
    public void BuildQueryString_WhenAllValuesBlank_ReturnsEmpty()
    {
        var parameters = new Dictionary<string, string> { ["a"] = " " };

        Assert.Equal(string.Empty, parameters.BuildQueryString());
    }

    /// <summary>
    /// 字典为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void BuildQueryString_WhenNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DictionaryExtensions.BuildQueryString(null!));
    }

    /// <summary>
    /// 类型匹配时取出强类型值
    /// </summary>
    [Fact]
    public void TryGetValue_WhenTypeMatches_ReturnsTrueAndValue()
    {
        IDictionary<string, object> dictionary = new Dictionary<string, object>
        {
            ["num"] = 42,
            ["text"] = "abc"
        };

        var numberFound = DictionaryExtensions.TryGetValue<int>(dictionary, "num", out var number);
        var textFound = DictionaryExtensions.TryGetValue<string>(dictionary, "text", out var text);

        Assert.True(numberFound);
        Assert.Equal(42, number);
        Assert.True(textFound);
        Assert.Equal("abc", text);
    }

    /// <summary>
    /// 类型不匹配或键不存在时返回假与默认值
    /// </summary>
    [Fact]
    public void TryGetValue_WhenTypeMismatchOrMissing_ReturnsFalse()
    {
        IDictionary<string, object> dictionary = new Dictionary<string, object> { ["num"] = 42 };

        var mismatched = DictionaryExtensions.TryGetValue<string>(dictionary, "num", out var text);
        var missing = DictionaryExtensions.TryGetValue<int>(dictionary, "none", out var number);

        Assert.False(mismatched);
        Assert.Null(text);
        Assert.False(missing);
        Assert.Equal(0, number);
    }

    /// <summary>
    /// 按键移除返回新字典，原字典不变
    /// </summary>
    [Fact]
    public void RemoveByKeys_ReturnsNewDictionaryWithoutTouchingSource()
    {
        IDictionary<string, object> source = new Dictionary<string, object>
        {
            ["a"] = 1,
            ["b"] = 2,
            ["c"] = 3
        };

        var result = source.RemoveByKeys("a", "missing");

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey("a"));
        Assert.True(result.ContainsKey("b"));
        Assert.Equal(3, source.Count);
    }

    /// <summary>
    /// 未指定键时原样返回同一个字典实例
    /// </summary>
    [Fact]
    public void RemoveByKeys_WithNoKeys_ReturnsSameInstance()
    {
        IDictionary<string, object> source = new Dictionary<string, object> { ["a"] = 1 };

        var result = source.RemoveByKeys();

        Assert.Same(source, result);
    }

    /// <summary>
    /// 字典为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void RemoveByKeys_WhenNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DictionaryExtensions.RemoveByKeys(null!, "a"));
    }
}
