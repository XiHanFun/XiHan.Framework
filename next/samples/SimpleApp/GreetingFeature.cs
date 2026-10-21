// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace SimpleApp;

/// <summary>
/// 示例能力包：注册一个计数服务，并在 Observe 阶段贡献一个计数中间件。
/// </summary>
public sealed class GreetingFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.samples.greeting",
        Package = "SimpleApp",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(CountingMiddleware).FullName!, PipelineStage.Observe)],
            Health = ["samples.greeting.counter"],
        },
        Entry = typeof(GreetingFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        context.Services.AddSingleton<GreetingCounter>();
        context.AddMiddleware<CountingMiddleware>(PipelineStage.Observe);
    }
}
