// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;
using ProtoBinding = XiHan.Framework.Plugins.Protocol.V1.Binding;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 组装 <c>Configure</c> 请求
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主共用（<c>spec/plugin/lifecycle.md</c> §2，ADR-0260）。工作目录由载体给出：进程外在组装时准备，Wasm 在实例化时已经准备好。绑定与内核按同一套来源解析（ADR-0268）
/// </remarks>
internal static partial class PluginConfigureRequests
{
    /// <summary>
    /// 配置节 JSON、已授予权限、清单声明的绑定与工作目录
    /// </summary>
    /// <remarks>
    /// <para>清单没有声明配置节时配置为 <c>{}</c>；已授予权限等于清单的 <c>permissions</c>（缺一项就过不了预检）。</para>
    /// <para>绑定在每次组装时经内核的 <see cref="BindingResolver"/> 解析一遍：先读 Service Binding 目录、再读配置节 <c>XiHan:Bindings</c>，同名时配置整体覆盖目录。只下发清单声明的绑定（能不能声明由预检的 <c>binding:&lt;名&gt;</c> 授予把关，ADR-0177）；类型取来源声明的、来源没声明时取清单声明的；线协议的绑定只有名字、类型与主值，其余属性不下发，没有主值的绑定整个不下发并记警告。</para>
    /// <para>绑定目录读失败返回 <c>PLUGINS_UNEXPECTED_001</c>（原因只写异常类型，完整异常进日志），不当成「没有绑定」少给：由调用方按配置这一步失败处理。根目录不存在不是失败。</para>
    /// </remarks>
    /// <param name="manifest">清单</param>
    /// <param name="configuration">宿主配置</param>
    /// <param name="serviceBindingRoot">应用指定的 Service Binding 根目录（<see cref="FeatureContext.ServiceBindingRoot"/>）；<see langword="null"/> 时取环境变量 <c>SERVICE_BINDING_ROOT</c></param>
    /// <param name="workDirectory">宿主为这个插件准备的工作目录</param>
    /// <param name="logger">载体的日志器</param>
    public static Result<ConfigureRequest> Build(Manifest manifest, IConfiguration configuration, string? serviceBindingRoot, string workDirectory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(workDirectory);
        ArgumentNullException.ThrowIfNull(logger);
        var request = new ConfigureRequest
        {
            Configuration = manifest.Contributes.Config is { } config ? PluginJson.SerializeSection(configuration.GetSection(config.Section)) : "{}",
            WorkDirectory = workDirectory,
        };
        request.GrantedPermissions.AddRange(manifest.Permissions);
        if (manifest.Bindings.Count == 0)
        {
            // 没声明绑定的插件不为它读目录
            return request;
        }

        IReadOnlyDictionary<string, BindingValue> resolved;
        try
        {
            // 与内核同一个解析器、同一个根：配置插件的这一刻解析，重启或运行期加载的插件拿到的是此刻的值（配置节也是此刻读的）
            resolved = BindingResolver.Resolve(configuration, serviceBindingRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 目录在、却读不出来（权限、轮换到一半的挂载）：少给一个绑定会让插件带着缺的连接启动，所以让这一步失败
            // 错误里只写异常类型：异常消息带着挂载路径，完整异常只进日志
            LogBindingsUnreadable(logger, manifest.Id, exception);
            return Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, MessageArguments.Of(("id", manifest.Id), ("phase", "configure"), ("reason", exception.GetType().FullName)));
        }

        foreach (var binding in manifest.Bindings)
        {
            if (!resolved.TryGetValue(binding.Name, out var value))
            {
                continue;
            }

            if (value.Value is null)
            {
                // 只有属性、没有主值（目录里没有 uri 文件，或配置里没有 value）：线协议带不了属性，这个绑定对插件等于没有
                LogBindingWithoutValue(logger, binding.Name, manifest.Id);
                continue;
            }

            // 来源没声明类型（空字符串）时取清单声明的类型
            request.Bindings.Add(new ProtoBinding { Name = binding.Name, Type = value.Type.Length > 0 ? value.Type : binding.Type, Value = value.Value });
        }

        return request;
    }

    /// <summary>
    /// 记录绑定读不出来
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Resolving the bindings of plugin {PluginId} failed; the plugin is not configured.")]
    private static partial void LogBindingsUnreadable(ILogger logger, string pluginId, Exception exception);

    /// <summary>
    /// 记录一个没有主值、因此不下发的绑定
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Binding '{Binding}' declared by plugin {PluginId} has no main value (the 'uri' file of a mounted binding, or the 'value' key in configuration) and is not delivered: the plugin protocol carries only the name, type and main value of a binding.")]
    private static partial void LogBindingWithoutValue(ILogger logger, string binding, string pluginId);
}
