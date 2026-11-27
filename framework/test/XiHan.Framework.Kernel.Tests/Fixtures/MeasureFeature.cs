// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization.Metadata;
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
/// <param name="info">请求与响应的序列化元数据；不给是 <see cref="MeasureJsonContext"/> 的</param>
internal sealed class MeasureFeature(JsonTypeInfo<MeasureRequest>? info = null) : IFeature
{
    /// <summary>
    /// 请求与响应的 Schema
    /// </summary>
    private static readonly OperationSchema _schema = new("""{"type":"object"}""", """{"type":"object"}""");

    /// <summary>
    /// 请求与响应的序列化元数据
    /// </summary>
    private readonly JsonTypeInfo<MeasureRequest> _info = info ?? MeasureJsonContext.Default.MeasureRequest;

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
        => context.AddOperation<MeasureOperation, MeasureRequest, MeasureRequest>(new(MeasureOperation.Id), _info, _info, _schema);
}
