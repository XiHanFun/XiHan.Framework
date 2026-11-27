// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 登记带 Schema 的回声操作 <see cref="MeasureOperation"/> 的特性
/// </summary>
/// <remarks>
/// 带 Schema 的描述才让类型化调用在 Validate 阶段编码请求；Schema 只要求是对象，不另加约束
/// </remarks>
internal sealed class MeasureFeature : IFeature
{
    /// <summary>
    /// 请求与响应的 Schema
    /// </summary>
    private static readonly OperationSchema _schema = new("""{"type":"object"}""", """{"type":"object"}""");

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.measure",
        Package = "XiHan.Framework.Kernel.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(MeasureFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
        => context.AddOperation<MeasureOperation, MeasureRequest, MeasureRequest>(new(MeasureOperation.Id), MeasureJsonContext.Default.MeasureRequest, MeasureJsonContext.Default.MeasureRequest, _schema);
}
