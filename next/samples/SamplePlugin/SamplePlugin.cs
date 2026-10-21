// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace SamplePlugin;

/// <summary>
/// 示例进程内插件：拥有自己的服务容器，启动时解析自己的服务。
/// </summary>
public sealed class SamplePlugin : IPlugin
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.samples.plugin",
        Package = "SamplePlugin",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Plugin,
        Isolation = PluginIsolation.InProcess,
        Requires = [new CapabilityRequirement(KernelCapabilities.Plugins, KernelCapabilities.Version)],
        Permissions = ["capability:kernel.diagnostics"],
        Entry = "SamplePlugin.SamplePlugin",
    };

    public void Configure(PluginContext context)
    {
        if (!context.Permissions.IsGranted("capability:kernel.diagnostics"))
        {
            throw new FrameworkException("SAMPLES_DENIED_001", "宿主没有授予诊断能力访问权限。");
        }

        context.Services.AddSingleton<Greeter>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _ = services.GetRequiredService<Greeter>().Greet("host");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
