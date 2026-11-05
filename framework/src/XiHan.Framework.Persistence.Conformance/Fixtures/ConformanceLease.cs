// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件实体：落在命名连接上，不实现任何协议，删除即物理删除
/// </summary>
/// <remarks>
/// 只在 Provider 给出 <see cref="RepositoryConformance.NamedConnection"/> 时登记到那个连接，供绑定连接的工作单元读写；EF Core 的 DbContext 模型要包含它
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConformanceLease : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 持有者
    /// </summary>
    public string Holder { get; set; } = string.Empty;
}
