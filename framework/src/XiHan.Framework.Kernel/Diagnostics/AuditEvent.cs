// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 审计事件
/// </summary>
/// <remarks>
/// 格式是规范性内容（RFC-0001 附属），审计子系统的 Provider 负责持久化。成员都经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuditEvent
{
    /// <summary>
    /// 发生时间（UTC）
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 动作，例如 <c>orders.create</c>
    /// </summary>
    /// <exception cref="ArgumentException">为空或空白</exception>
    public required string Action
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// 结果
    /// </summary>
    public required Result Outcome { get; init; }

    /// <summary>
    /// 所属调用的 trace-id
    /// </summary>
    /// <exception cref="ArgumentNullException">为 <see langword="null"/></exception>
    public required string TraceId
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// 资源名（<c>xrn:</c>）
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// 主体 ID
    /// </summary>
    public string? PrincipalId { get; init; }

    /// <summary>
    /// 租户 ID
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 附加细节，名字到 JSON 值
    /// </summary>
    /// <remarks>
    /// 值是 <see cref="JsonElement"/>，任何接收器写入再读回都是同一类型（ADR-0159）
    /// </remarks>
    public IReadOnlyDictionary<string, JsonElement>? Details { get; init; }
}
