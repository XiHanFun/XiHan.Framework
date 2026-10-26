// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Auditing.Database;

/// <summary>
/// 数据库审计的能力包入口：提供 <c>auditing.trail@1</c>，依赖 <c>persistence.relational@1</c>。装配后顶替默认的内存审计：被选中时把 <see cref="DatabaseAuditTrail"/> 绑定为接收器与查询并挂上审计中间件；
/// 启动后运行后台刷写循环，停止时先把队列里剩余的事件刷完。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseAuditingFeature : IHostedFeature, IDisposable
{
    /// <summary>
    /// 启动后保存的审计存储，供停止时刷写。
    /// </summary>
    private DatabaseAuditTrail? _trail;

    /// <summary>
    /// 停止信号；<see cref="StopAsync"/> 取消它让刷写循环退出。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 正在运行的刷写循环；停止时等待它结束。
    /// </summary>
    private Task? _loop;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.auditing.database",
        Package = "XiHan.Framework.Auditing.Database",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(AuditingCapabilities.Trail, AuditingCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions { Config = new ConfigContribution(AuditingOptions.ConfigurationSection) },
        Entry = typeof(DatabaseAuditingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(AuditingMessages.Catalog);
        context.Services.AddSingleton(DatabaseAuditingOptions.FromConfiguration(context.Configuration));
        context.Services.AddRepository<AuditRecord, Guid>();
        context.Services.AddSingleton<DatabaseAuditTrail>();
        if (context.Node.IsSelectedFor(AuditingCapabilities.Trail))
        {
            // 特性在内核的默认注册之前配置，所以 TryAdd 能先于丢弃实现占住 IAuditSink
            context.Services.TryAddSingleton<IAuditSink>(static sp => sp.GetRequiredService<DatabaseAuditTrail>());
            context.Services.TryAddSingleton<IAuditTrail>(static sp => sp.GetRequiredService<DatabaseAuditTrail>());
            context.AddAuditing();
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _trail = services.GetRequiredService<DatabaseAuditTrail>();
        _stopping = new CancellationTokenSource();
        _loop = _trail.RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_trail is null || _stopping is null || _loop is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _loop.ConfigureAwait(false);
        // 停止信号只停循环，不丢事件：先停止接收，再把队列里剩余的事件刷完；之后到达的事件按丢弃计数
        _trail.Complete();
        await _trail.FlushAsync(cancellationToken).ConfigureAwait(false);
        _stopping.Dispose();
        _stopping = null;
        _loop = null;
        _trail = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }
}
