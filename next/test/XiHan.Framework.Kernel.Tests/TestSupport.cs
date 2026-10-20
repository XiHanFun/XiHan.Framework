// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests;

internal static class Manifests
{
    public static Manifest Create(
        string id,
        string[]? provides = null,
        string[]? requires = null,
        ManifestRole role = ManifestRole.Provider,
        ManifestKind kind = ManifestKind.Capability,
        Binding[]? bindings = null) => new()
    {
        Id = id,
        Version = "5.0.0",
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = kind,
        Role = role,
        Provides = (provides ?? []).Select(CapabilityDeclaration.Parse).ToList(),
        Requires = (requires ?? []).Select(CapabilityRequirement.Parse).ToList(),
        Bindings = bindings ?? [],
    };
}

/// <summary>
/// 记录生命周期事件的特性；可注入启动与停止行为。
/// </summary>
internal sealed class RecordingFeature(Manifest manifest, List<string> log) : IHostedFeature
{
    public Manifest Manifest { get; } = manifest;

    public Func<CancellationToken, Task>? OnStart { get; init; }

    public Func<CancellationToken, Task>? OnStop { get; init; }

    public Action<FeatureContext>? OnConfigure { get; init; }

    public void Configure(FeatureContext context)
    {
        log.Add($"configure:{Manifest.Id}");
        OnConfigure?.Invoke(context);
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        log.Add($"start:{Manifest.Id}");
        if (OnStart is not null)
        {
            await OnStart(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        log.Add($"stop:{Manifest.Id}");
        if (OnStop is not null)
        {
            await OnStop(cancellationToken);
        }
    }
}

internal sealed class TracingMiddleware(List<string> log, string name) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        log.Add($"enter:{name}");
        await nextStep(context);
        log.Add($"exit:{name}");
    }
}

internal sealed class MiddlewareA(List<string> log) : IPipelineMiddleware
{
    private readonly TracingMiddleware _inner = new(log, "A");

    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => _inner.InvokeAsync(context, nextStep);
}

internal sealed class MiddlewareB(List<string> log) : IPipelineMiddleware
{
    private readonly TracingMiddleware _inner = new(log, "B");

    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => _inner.InvokeAsync(context, nextStep);
}

internal sealed class MiddlewareC(List<string> log) : IPipelineMiddleware
{
    private readonly TracingMiddleware _inner = new(log, "C");

    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => _inner.InvokeAsync(context, nextStep);
}

internal sealed class MiddlewareD : IPipelineMiddleware
{
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
}

internal static class Hosts
{
    public static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    public static (ServiceProvider Provider, App App) BuildApp(Action<AppBuilder> configure, IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, configuration ?? Configuration());
        configure(builder);
        builder.Build();
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<App>());
    }

    public static InvocationContext Context(IServiceProvider services, DateTimeOffset? deadline = null, CancellationToken cancellationToken = default)
        => App.CreateContext(services, cancellationToken, deadline: deadline);
}
