// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Scheduling.Kubernetes.Leases;

/// <summary>
/// Lease 的元数据：名字、命名空间与乐观并发用的 resourceVersion。
/// </summary>
internal sealed class LeaseMetadata
{
    /// <summary>
    /// 名字。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间。
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 资源版本。
    /// </summary>
    /// <remarks>
    /// 更新时带上，API Server 据此拒绝过期写入（409）。
    /// </remarks>
    public string? ResourceVersion { get; set; }
}
