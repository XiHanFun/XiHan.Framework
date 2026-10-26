// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database.Trail;

/// <summary>
/// 一条审计记录：<c>AuditEvent</c> 的存储行，只追加——不软删除、无并发戳。租户只是普通列，不实现 <c>IMultiTenant</c>，写入与查询都不受当前调用租户影响。
/// EF Core 应用在自己的 <c>DbContext</c> 里 <c>modelBuilder.Entity&lt;AuditRecord&gt;()</c>，JSON 与消息列用长文本类型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AuditRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 写入序号：查询按它升序，续页令牌就是它。同一进程内严格递增（上一个加一与时钟 UTC 刻度的较大者），不同节点之间只按时钟大致有序。
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// 事件发生时间（UTC）。
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// 动作，例如 <c>orders.create</c>。
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 调用是否成功。
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 失败的错误码。
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 失败的消息键；字面错误（线上还原、异常转换）没有键。
    /// </summary>
    public string? ErrorMessageKey { get; set; }

    /// <summary>
    /// 写入时按进程默认区域渲染的错误消息；没有键的字面错误靠它还原。长文本列。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 错误细节（模板参数）的 JSON 对象文本；长文本列。
    /// </summary>
    public string? ErrorDetailsJson { get; set; }

    /// <summary>
    /// 所属调用的 trace-id。
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 资源名（<c>xrn:</c>）。
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// 主体 ID。
    /// </summary>
    public string? PrincipalId { get; set; }

    /// <summary>
    /// 租户 ID。
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 附加细节的 JSON 对象文本（键区分大小写）；长文本列。
    /// </summary>
    public string? DetailsJson { get; set; }
}
