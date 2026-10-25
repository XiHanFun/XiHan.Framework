// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件实体：整数标识、不实现任何协议，删除即物理删除。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceItem : IEntity<int>
{
    /// <inheritdoc />
    public int Id { get; init; }

    /// <summary>标签。</summary>
    public string Label { get; set; } = string.Empty;
}
