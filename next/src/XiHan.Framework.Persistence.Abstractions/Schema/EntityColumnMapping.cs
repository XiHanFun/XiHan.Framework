// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 实体的一个属性映射到的列。
/// </summary>
/// <param name="Property">属性名。</param>
/// <param name="Column">列名（映射声明的写法）。</param>
/// <param name="Type">属性的 CLR 类型。</param>
/// <param name="IsKey">是否是主键。</param>
/// <param name="IsNullable">列是否可空。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record EntityColumnMapping(string Property, string Column, Type Type, bool IsKey, bool IsNullable);
