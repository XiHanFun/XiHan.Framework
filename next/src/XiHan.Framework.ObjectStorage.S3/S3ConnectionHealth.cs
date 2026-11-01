// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 缺省实例的健康项 <see cref="S3ObjectStorageFeature.HealthName"/>：连通性加条件写入的状态。
/// </summary>
/// <remarks>
/// 连通性由周期探针（对 <see cref="S3ObjectStorageOptions.HealthBucket"/> HEAD，没配时列桶）缓存；条件写入的状态每次读取时即时并入（<see cref="S3ConditionalWriteGate.Annotate"/>）：后台重试一确认，条目不等下一轮连通性探测就恢复（ADR-0090）。
/// </remarks>
/// <param name="connection">连通性的周期探针。</param>
/// <param name="conditionalWrites">条件写入的状态。</param>
internal sealed class S3ConnectionHealth(PeriodicHealthContributor connection, S3ConditionalWriteGate conditionalWrites) : IHealthContributor
{
    /// <summary>
    /// 连通性的周期探针。
    /// </summary>
    private readonly PeriodicHealthContributor _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    /// <summary>
    /// 条件写入的状态。
    /// </summary>
    private readonly S3ConditionalWriteGate _conditionalWrites = conditionalWrites ?? throw new ArgumentNullException(nameof(conditionalWrites));

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check() => [.. _connection.Check().Select(_conditionalWrites.Annotate)];
}
