// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// <c>coordination.k8s.io/v1</c> 的 Lease 对象（只保留用到的字段）。
/// </summary>
internal sealed class LeaseDocument
{
    /// <summary>
    /// API 版本。
    /// </summary>
    public string ApiVersion { get; set; } = "coordination.k8s.io/v1";

    /// <summary>
    /// 种类。
    /// </summary>
    public string Kind { get; set; } = "Lease";

    /// <summary>
    /// 元数据。
    /// </summary>
    public LeaseMetadata Metadata { get; set; } = new();

    /// <summary>
    /// 规约。
    /// </summary>
    public LeaseSpec Spec { get; set; } = new();
}
