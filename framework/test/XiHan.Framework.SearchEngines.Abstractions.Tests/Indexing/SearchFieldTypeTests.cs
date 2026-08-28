// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Indexing;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Indexing;

/// <summary>
/// 索引字段类型的测试
/// </summary>
/// <remarks>
/// 枚举成员与其序号一并锁死：索引映射会被各实现落到自己的元数据里（映射缓存、迁移脚本、
/// 配置文件都可能按序号或名称持久化），重排或插入成员会造成静默的语义漂移。
/// 新增成员只允许追加到末尾。
/// </remarks>
public class SearchFieldTypeTests
{
    /// <summary>
    /// 各成员的序号不漂移
    /// </summary>
    /// <param name="type">字段类型</param>
    /// <param name="expected">期望序号</param>
    [Theory]
    [InlineData(SearchFieldType.Text, 0)]
    [InlineData(SearchFieldType.Keyword, 1)]
    [InlineData(SearchFieldType.Integer, 2)]
    [InlineData(SearchFieldType.Double, 3)]
    [InlineData(SearchFieldType.Boolean, 4)]
    [InlineData(SearchFieldType.DateTime, 5)]
    public void Value_IsStable(SearchFieldType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    /// <summary>
    /// 成员集合与顺序不漂移
    /// </summary>
    [Fact]
    public void Members_AreExactlyTheBackendIntersection()
    {
        Assert.Equal(
            [
                SearchFieldType.Text,
                SearchFieldType.Keyword,
                SearchFieldType.Integer,
                SearchFieldType.Double,
                SearchFieldType.Boolean,
                SearchFieldType.DateTime
            ],
            Enum.GetValues<SearchFieldType>());
    }

    /// <summary>
    /// 成员名称不漂移
    /// </summary>
    /// <remarks>
    /// 实现可能按名称往后端映射（Elasticsearch 的 text/keyword），改名同样是破坏性变更。
    /// </remarks>
    [Fact]
    public void Names_AreStable()
    {
        Assert.Equal(
            new[] { "Text", "Keyword", "Integer", "Double", "Boolean", "DateTime" },
            Enum.GetNames<SearchFieldType>());
    }

    /// <summary>
    /// 越界数值不属于已定义成员
    /// </summary>
    [Fact]
    public void IsDefined_ForOutOfRangeValue_IsFalse()
    {
        Assert.False(Enum.IsDefined((SearchFieldType)6));
        Assert.False(Enum.IsDefined((SearchFieldType)(-1)));
    }

    /// <summary>
    /// 默认值为全文文本
    /// </summary>
    /// <remarks>
    /// 未显式赋值的字段类型会落到 Text，这是最保守的选择（可检索但不可精确过滤），
    /// 若把 Keyword 挪到 0 位，所有默认字段会静默变成不分词。
    /// </remarks>
    [Fact]
    public void Default_IsText()
    {
        Assert.Equal(SearchFieldType.Text, default(SearchFieldType));
    }
}
