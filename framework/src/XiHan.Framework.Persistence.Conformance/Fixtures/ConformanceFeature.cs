// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件自带的特性
/// </summary>
/// <remarks>
/// 在 Tenant 阶段把 Baggage 里的 <c>xihan.tenant</c> 写入安全上下文，让用例不依赖 MultiTenancy 子系统。另登记两个执行用例脚本的操作：只读的 <c>conformance-scripts.get</c> 与可写的 <c>conformance-scripts.run</c>，只读工作单元与嵌套调用的用例经它们走真实的操作契约
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.persistence.conformance",
        Package = "XiHan.Framework.Persistence.Conformance",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(TenantFromBaggageMiddleware).FullName!, PipelineStage.Tenant)],
        },
        Entry = typeof(ConformanceFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddMiddleware<TenantFromBaggageMiddleware>(PipelineStage.Tenant);
        context.Services.TryAddSingleton<ConformanceScripts>();
        context.AddOperation<ReadScriptOperation, ConformanceScriptRequest, ConformanceScriptResponse>(new OperationContract(ReadScriptOperation.Id), ConformanceJsonContext.Default.ConformanceScriptRequest, ConformanceJsonContext.Default.ConformanceScriptResponse);
        context.AddOperation<WriteScriptOperation, ConformanceScriptRequest, ConformanceScriptResponse>(new OperationContract(WriteScriptOperation.Id), ConformanceJsonContext.Default.ConformanceScriptRequest, ConformanceJsonContext.Default.ConformanceScriptResponse);
    }
}
