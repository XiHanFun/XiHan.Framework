// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Indexing;

/// <summary>
/// 索引字段定义
/// </summary>
/// <param name="Name">字段名</param>
/// <param name="Type">字段类型</param>
/// <param name="Searchable">是否参与关键字检索，仅对 <see cref="SearchFieldType.Text"/> 有意义</param>
/// <param name="Sortable">是否可用于排序</param>
public sealed record SearchFieldDefinition(
    string Name,
    SearchFieldType Type,
    bool Searchable = false,
    bool Sortable = false);

/// <summary>
/// 索引定义
/// </summary>
/// <remarks>
/// 只描述各后端共有的结构：字段清单与语言。分片、副本、分析器链等后端独有的调优项
/// 不进抽象，由各实现从自己的配置节读取。
/// </remarks>
public sealed class SearchIndexDefinition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">索引名</param>
    /// <param name="fields">字段定义</param>
    public SearchIndexDefinition(string name, IReadOnlyList<SearchFieldDefinition> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(fields);

        Name = name;
        Fields = fields;
    }

    /// <summary>
    /// 索引名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 字段定义
    /// </summary>
    public IReadOnlyList<SearchFieldDefinition> Fields { get; }

    /// <summary>
    /// 文本分析所用语言，为空时由实现取默认值
    /// </summary>
    public string? Language { get; init; }
}
