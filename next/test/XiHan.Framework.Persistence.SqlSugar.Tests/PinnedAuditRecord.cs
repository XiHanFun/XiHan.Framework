// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 用 SqlSugar 的 <c>[Tenant]</c> 固定落在 <c>audit</c> 连接上的实体；多连接用例验证显式落点优先于默认规则。
/// </summary>
[Tenant("audit")]
public sealed class PinnedAuditRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 内容。
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
