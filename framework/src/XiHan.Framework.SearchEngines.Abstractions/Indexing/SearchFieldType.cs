// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Abstractions.Indexing;

/// <summary>
/// 索引字段类型
/// </summary>
/// <remarks>
/// 只保留各搜索后端都能表达的类型。Elasticsearch 与 PostgreSQL 全文检索都能落地这一组，
/// 不引入任一后端独有的字段类型，否则抽象会被钉死在某一实现上。
/// </remarks>
public enum SearchFieldType
{
    /// <summary>
    /// 全文文本，参与分词与相关度打分
    /// </summary>
    Text,

    /// <summary>
    /// 关键字，整体匹配、不分词，可用于过滤与排序
    /// </summary>
    Keyword,

    /// <summary>
    /// 整数
    /// </summary>
    Integer,

    /// <summary>
    /// 浮点数
    /// </summary>
    Double,

    /// <summary>
    /// 布尔
    /// </summary>
    Boolean,

    /// <summary>
    /// 日期时间
    /// </summary>
    DateTime
}
