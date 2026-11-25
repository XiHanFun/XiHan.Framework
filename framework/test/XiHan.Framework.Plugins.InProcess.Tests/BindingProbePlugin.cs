// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.InProcess.Tests;

/// <summary>
/// 记下自己拿到的绑定根目录与读到的绑定的进程内插件
/// </summary>
/// <remarks>
/// 测试把本程序集的副本放进插件目录，宿主在独立的加载上下文里加载它：两个上下文的静态字段各是各的，结果经进程级的 <see cref="AppContext"/> 数据交回，槽名从配置 <c>Probe:Slot</c> 读，结果是 <c>&lt;根目录&gt;|&lt;db 的主值&gt;</c>（根目录为空时前一段是空串）。插件照推荐的写法解析绑定：<see cref="BindingResolver.Resolve(Microsoft.Extensions.Configuration.IConfiguration, string?)"/> 收 <see cref="PluginContext.Configuration"/> 与 <see cref="PluginContext.ServiceBindingRoot"/>（ADR-0301）
/// </remarks>
public sealed class BindingProbePlugin : IPlugin
{
    /// <summary>
    /// 清单 ID
    /// </summary>
    public const string Id = "xihan.tests.probe";

    /// <summary>
    /// 配置里写结果槽名的键
    /// </summary>
    public const string SlotKey = "Probe:Slot";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = Id,
        Package = typeof(BindingProbePlugin).Assembly.GetName().Name,
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Plugin,
        Isolation = PluginIsolation.InProcess,
        Bindings = [new Binding("db", "connection:relational")],
        Entry = typeof(BindingProbePlugin).FullName,
    };

    /// <inheritdoc />
    public void Configure(PluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bindings = BindingResolver.Resolve(context.Configuration, context.ServiceBindingRoot);
        var value = bindings.TryGetValue("db", out var db) ? db.Value : null;
        AppContext.SetData(context.Configuration[SlotKey] ?? throw new InvalidOperationException($"Configuration key '{SlotKey}' is missing."), $"{context.ServiceBindingRoot}|{value}");
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
