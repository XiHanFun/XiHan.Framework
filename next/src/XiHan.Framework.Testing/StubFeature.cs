// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Testing;

/// <summary>
/// 用能力 ID 文本快速构造清单。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class TestManifests
{
    /// <summary>
    /// 创建清单。
    /// </summary>
    /// <param name="id">清单 ID。</param>
    /// <param name="provides">提供的能力，<c>id@version</c>。</param>
    /// <param name="requires">依赖的能力，<c>id@[^]version[?]</c>。</param>
    /// <param name="role">角色。</param>
    /// <param name="kind">种类。</param>
    /// <param name="bindings">绑定。</param>
    public static Manifest Create(
        string id,
        IEnumerable<string>? provides = null,
        IEnumerable<string>? requires = null,
        ManifestRole role = ManifestRole.Provider,
        ManifestKind kind = ManifestKind.Capability,
        IEnumerable<Binding>? bindings = null) => new()
    {
        Id = id,
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = kind,
        Role = role,
        Provides = (provides ?? []).Select(CapabilityDeclaration.Parse).ToList(),
        Requires = (requires ?? []).Select(CapabilityRequirement.Parse).ToList(),
        Bindings = (bindings ?? []).ToList(),
    };
}

/// <summary>
/// 记录生命周期事件、可注入行为的特性桩。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class StubFeature : IHostedFeature
{
    private readonly List<string> _events = [];

    /// <summary>
    /// 创建特性桩。
    /// </summary>
    public StubFeature(Manifest manifest)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <inheritdoc />
    public Manifest Manifest { get; }

    /// <summary>
    /// 已记录的事件：<c>configure</c>、<c>start</c>、<c>stop</c>。
    /// </summary>
    public IReadOnlyList<string> Events => _events;

    /// <summary>
    /// 配置时执行。
    /// </summary>
    public Action<FeatureContext>? OnConfigure { get; init; }

    /// <summary>
    /// 启动时执行。
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task>? OnStart { get; init; }

    /// <summary>
    /// 停止时执行。
    /// </summary>
    public Func<CancellationToken, Task>? OnStop { get; init; }

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        _events.Add("configure");
        OnConfigure?.Invoke(context);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _events.Add("start");
        if (OnStart is not null)
        {
            await OnStart(services, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _events.Add("stop");
        if (OnStop is not null)
        {
            await OnStop(cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// 不依赖宿主的调用上下文。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class TestInvocationContext
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// 创建上下文；未提供服务提供器时使用空提供器。
    /// </summary>
    public static InvocationContext Create(
        IServiceProvider? services = null,
        DateTimeOffset? deadline = null,
        IReadOnlyDictionary<string, string>? baggage = null,
        CancellationToken cancellationToken = default)
        => App.CreateContext(services ?? EmptyServiceProvider.Instance, cancellationToken, deadline: deadline, baggage: baggage);
}
