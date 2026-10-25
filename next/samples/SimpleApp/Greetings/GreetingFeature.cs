// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace SimpleApp.Greetings;

/// <summary>
/// 示例能力包：注册一个计数服务，并在 Observe 阶段贡献一个计数中间件。
/// </summary>
public sealed class GreetingFeature : IFeature
{
    /// <summary>
    /// 清单：只依赖内核宿主能力，声明贡献的中间件与健康项，让内核在装配前就能校验。
    /// </summary>
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

    /// <summary>
    /// 注册计数服务，并把计数中间件挂到 Observe 阶段；挂载的阶段必须与清单声明一致。
    /// </summary>
    public void Configure(FeatureContext context)
    {
        context.Services.AddSingleton<GreetingCounter>();
        context.AddMiddleware<CountingMiddleware>(PipelineStage.Observe);
    }
}
