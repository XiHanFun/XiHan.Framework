// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives.Manifests;
using ProtoBinding = XiHan.Framework.Plugins.Protocol.V1.Binding;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 组装 <c>Configure</c> 请求
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主共用（<c>spec/plugin/lifecycle.md</c> §2，ADR-0260）。工作目录由载体给出：进程外在组装时准备，Wasm 在实例化时已经准备好
/// </remarks>
internal static class PluginConfigureRequests
{
    /// <summary>
    /// 配置节 JSON、已授予权限、清单声明的绑定与工作目录
    /// </summary>
    /// <remarks>
    /// 清单没有声明配置节时配置为 <c>{}</c>；已授予权限等于清单的 <c>permissions</c>（缺一项就过不了预检）。绑定只读宿主配置的 <c>XiHan:Bindings:&lt;绑定名&gt;</c>，在配置里没有值的不下发
    /// </remarks>
    /// <param name="manifest">清单</param>
    /// <param name="configuration">宿主配置</param>
    /// <param name="workDirectory">宿主为这个插件准备的工作目录</param>
    public static ConfigureRequest Build(Manifest manifest, IConfiguration configuration, string workDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(workDirectory);
        var request = new ConfigureRequest
        {
            Configuration = manifest.Contributes.Config is { } config ? PluginJson.SerializeSection(configuration.GetSection(config.Section)) : "{}",
            WorkDirectory = workDirectory,
        };
        request.GrantedPermissions.AddRange(manifest.Permissions);
        foreach (var binding in manifest.Bindings)
        {
            // 只读配置来源：XiHan:Bindings:<name> 的 value 子键或节本身的标量值作主值，type 子键作类型、没写时取清单声明的类型
            // 内核的 BindingResolver 另读 SERVICE_BINDING_ROOT 目录，这里不经它：只以目录给出的绑定不会下发给插件
            var section = configuration.GetSection($"{BindingResolver.ConfigurationSection}:{binding.Name}");
            var value = section["value"] ?? section.Value;
            if (value is not null)
            {
                request.Bindings.Add(new ProtoBinding { Name = binding.Name, Type = section["type"] ?? binding.Type, Value = value });
            }
        }

        return request;
    }
}
