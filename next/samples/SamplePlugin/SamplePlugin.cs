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
    /// <summary>
    /// 清单：与插件目录里的 <c>xihan.manifest.json</c> 保持一致，宿主会核对两者的 ID；<c>Entry</c> 是入口类型的全名。
    /// </summary>
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

    /// <summary>
    /// 校验宿主授予的权限后，向插件私有容器注册 <see cref="Greeter"/>。
    /// </summary>
    public void Configure(PluginContext context)
    {
        // 宿主加载前已按清单校验过权限；这里再查一次是演示插件如何在运行期自检，缺权限时以框架异常让加载失败
        if (!context.Permissions.IsGranted("capability:kernel.diagnostics"))
        {
            throw new FrameworkException("SAMPLES_DENIED_001", "The host has not granted access to the diagnostics capability.");
        }

        context.Services.AddSingleton<Greeter>();
    }

    /// <summary>
    /// 从插件私有容器解析 <see cref="Greeter"/> 并问候宿主一次，证明容器与宿主隔离。
    /// </summary>
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _ = services.GetRequiredService<Greeter>().Greet("host");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 无需清理，直接完成。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
