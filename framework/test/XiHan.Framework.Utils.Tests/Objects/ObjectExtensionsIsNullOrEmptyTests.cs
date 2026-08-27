// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Utils.Tests.Objects;

/// <summary>
/// 对象扩展判空的集合语义回归测试
/// </summary>
/// <remarks>
/// 原实现只处理 null、string 与 DBNull（`if (data is not string) return data is DBNull;`），
/// 任何非 null 非字符串对象（空 List、空数组、空字典……）一律被判成"非空"，
/// 调用方的空集合短路因此完全失效。本文件把语义与 GenericExtensions.IsNullOrEmpty 对齐后锁住。
/// 统一用静态调用形式，避免与 GenericExtensions 的同名泛型扩展在重载解析上产生歧义。
/// </remarks>
public class ObjectExtensionsIsNullOrEmptyTests
{
    /// <summary>
    /// 空列表判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyList_ReturnsTrue()
    {
        Assert.True(ObjectExtensions.IsNullOrEmpty(new List<int>()));
    }

    /// <summary>
    /// 非空列表判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNonEmptyList_ReturnsFalse()
    {
        Assert.False(ObjectExtensions.IsNullOrEmpty(new List<int> { 1 }));
    }

    /// <summary>
    /// 空数组判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyArray_ReturnsTrue()
    {
        Assert.True(ObjectExtensions.IsNullOrEmpty(Array.Empty<string>()));
    }

    /// <summary>
    /// 非空数组判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNonEmptyArray_ReturnsFalse()
    {
        Assert.False(ObjectExtensions.IsNullOrEmpty(new[] { "a" }));
    }

    /// <summary>
    /// 空字典判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyDictionary_ReturnsTrue()
    {
        Assert.True(ObjectExtensions.IsNullOrEmpty(new Dictionary<string, string>()));
    }

    /// <summary>
    /// 空哈希集合走非泛型 IEnumerable 兜底分支，同样判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyHashSet_ReturnsTrue()
    {
        Assert.True(ObjectExtensions.IsNullOrEmpty(new HashSet<int>()));
    }

    /// <summary>
    /// 非空哈希集合判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNonEmptyHashSet_ReturnsFalse()
    {
        Assert.False(ObjectExtensions.IsNullOrEmpty(new HashSet<int> { 1 }));
    }

    /// <summary>
    /// 惰性空序列判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenLazyEmptySequence_ReturnsTrue()
    {
        Assert.True(ObjectExtensions.IsNullOrEmpty(EmptySequence()));
    }

    /// <summary>
    /// 惰性非空序列只推进一步就判定
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenLazyNonEmptySequence_PullsOnlyFirstElement()
    {
        List<int> pulled = [];

        Assert.False(ObjectExtensions.IsNullOrEmpty(CountingSequence(pulled)));
        Assert.Equal(new[] { 0 }, pulled);
    }

    /// <summary>
    /// null、空白字符串与 DBNull 仍判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_KeepsNullBlankStringAndDbNullSemantics()
    {
        Assert.True(ObjectExtensions.IsNullOrEmpty(null));
        Assert.True(ObjectExtensions.IsNullOrEmpty(string.Empty));
        Assert.True(ObjectExtensions.IsNullOrEmpty("   "));
        Assert.True(ObjectExtensions.IsNullOrEmpty(DBNull.Value));
    }

    /// <summary>
    /// 非空字符串、值类型与普通对象仍判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_KeepsNonEmptyScalarSemantics()
    {
        Assert.False(ObjectExtensions.IsNullOrEmpty("a"));
        Assert.False(ObjectExtensions.IsNullOrEmpty(0));
        Assert.False(ObjectExtensions.IsNullOrEmpty(new object()));
    }

    /// <summary>
    /// 惰性空序列
    /// </summary>
    private static IEnumerable<int> EmptySequence()
    {
        yield break;
    }

    /// <summary>
    /// 每产出一个元素就记录一次，用来观察判空过程推进了几步
    /// </summary>
    private static IEnumerable<int> CountingSequence(List<int> pulled)
    {
        for (var i = 0; i < 3; i++)
        {
            pulled.Add(i);
            yield return i;
        }
    }
}
