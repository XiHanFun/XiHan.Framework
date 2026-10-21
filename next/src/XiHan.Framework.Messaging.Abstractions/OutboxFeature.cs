// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 事务性发件箱特性：需要 <c>messaging.queue@1</c> 与 <c>persistence.relational@1</c>；把 <see cref="IMessagePublisher"/> 换成 <see cref="OutboxPublisher"/>，
/// 登记 <see cref="OutboxMessage"/> 实体，启动后运行投递循环。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxFeature : IHostedFeature, IDisposable
{
    /// <summary>
    /// 停止信号；<see cref="StopAsync"/> 取消它让投递循环退出。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 正在运行的投递循环；停止时等待它结束。
    /// </summary>
    private Task? _loop;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.messaging.outbox",
        Package = "XiHan.Framework.Messaging.Abstractions",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Messaging") },
        Entry = typeof(OutboxFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(OutboxOptions.FromConfiguration(context.Configuration));
        context.Services.AddRepository<OutboxMessage, Guid>();
        // 用 Replace 而不是 TryAdd：Provider 已注册的 DirectPublisher 必须让位给发件箱发布者
        context.Services.Replace(ServiceDescriptor.Scoped<IMessagePublisher, OutboxPublisher>());
        context.Services.TryAddSingleton<OutboxDispatcher>();
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _stopping = new CancellationTokenSource();
        _loop = services.GetRequiredService<OutboxDispatcher>().RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is null || _loop is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _loop.ConfigureAwait(false);
        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }
}
