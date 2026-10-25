// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Scheduling.Kubernetes.Leases;

/// <summary>
/// API Server 失败应答里的 Status 对象（只取原因与消息）。
/// </summary>
internal sealed class StatusDocument
{
    /// <summary>
    /// 原因（如 <c>Conflict</c>、<c>NotFound</c>）。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 消息。
    /// </summary>
    public string? Message { get; set; }
}
