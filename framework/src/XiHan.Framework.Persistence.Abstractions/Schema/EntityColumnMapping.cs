// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 实体的一个属性映射到的列
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record EntityColumnMapping
{
    /// <summary>
    /// 属性名
    /// </summary>
    public required string Property { get; init; }

    /// <summary>
    /// 列名（映射声明的写法）
    /// </summary>
    public required string Column { get; init; }

    /// <summary>
    /// 属性的 CLR 类型
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// 是否是主键
    /// </summary>
    public bool IsKey { get; init; }

    /// <summary>
    /// 列是否可空
    /// </summary>
    public bool IsNullable { get; init; }
}
