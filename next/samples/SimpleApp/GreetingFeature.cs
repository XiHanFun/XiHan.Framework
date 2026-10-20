// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
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

/// <summary>
/// 已处理的调用次数。
/// </summary>
public sealed class GreetingCounter
{
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}

public sealed class CountingMiddleware(GreetingCounter counter) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        counter.Increment();
        await nextStep(context);
    }
}

public sealed record Greeting(string Message, long Served);

[JsonSerializable(typeof(Greeting))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
