// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Queries;

/// <summary>
/// 查询的一项排序。
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
/// <param name="KeySelector">排序键。</param>
/// <param name="Descending">是否降序。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record Ordering<T>(Expression<Func<T, object?>> KeySelector, bool Descending = false);
