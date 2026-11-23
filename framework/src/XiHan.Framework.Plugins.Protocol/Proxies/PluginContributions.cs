// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol.Proxies;

/// <summary>
/// 把一个插件的消息目录、权限码与代理操作登记进宿主
/// </summary>
/// <remarks>
/// 目录与代理操作是握手声明的，权限码是清单 <c>contributes.permissions</c> 声明的。进程外与 Wasm 宿主共用（<c>plugin/invocation.md</c> §1，ADR-0080、ADR-0252）：归属是插件的清单 ID，目录先进本地化器，权限码与操作再一次 <see cref="OperationRegistry.AddRange"/>，全进或全不进；卸载时按同一归属一次撤回（<see cref="Withdraw"/>，ADR-0085）
/// </remarks>
internal static class PluginContributions
{
    /// <summary>
    /// 登记插件的消息目录、权限码与代理操作
    /// </summary>
    /// <remarks>
    /// 只登记清单声明了的权限码，显示名取权限码本身（插件可在握手的消息目录里给 <c>permissions.&lt;码&gt;.name</c> 的译文）。目录与同名目录冲突（<c>LOCALIZATION_CONFLICT_001</c>）、声明的码与已登记的重复、操作重名、引用了未登记的权限码或挂上的核对不通过，都返回失败且两个登记表都不变；配置阶段的调用方据此启动失败，之后的调用方按发现时被拒处理。目录先于操作登记：操作一出现在登记表里（接口文档随之重新生成），它们的文案就已经可以解析
    /// </remarks>
    /// <param name="registry">宿主的操作登记表（绑定了权限登记表）</param>
    /// <param name="catalogs">宿主的目录登记表；为空时不登记目录（配置阶段，目录由特性按归属注册进容器）</param>
    /// <param name="manifest">插件清单（已由清单读取器核对过权限码文法与重复）</param>
    /// <param name="handshake">成功的握手</param>
    /// <param name="invoker">代理操作执行时转交的插件运行时</param>
    public static Result Register(OperationRegistry registry, MessageCatalogRegistry? catalogs, Manifest manifest, HandshakeInfo handshake, IPluginInvoker invoker)
    {
        if (catalogs is not null)
        {
            try
            {
                catalogs.Add(manifest.Id, handshake.Catalogs);
            }
            catch (FrameworkException exception)
            {
                return exception.ToError();
            }
        }

        var registered = registry.AddRange(
            manifest.Id,
            handshake.Operations.Select(operation => (OperationDescriptor)new ProxyOperationDescriptor(operation.Contract, manifest.Id, invoker, operation.Schema)).ToList(),
            manifest.Contributes.Permissions.Select(static code => new PermissionDefinition(code, code)).ToList());
        if (registered.IsFailure)
        {
            catalogs?.RemoveOwnedBy(manifest.Id);
        }

        return registered;
    }

    /// <summary>
    /// 撤回插件登记的代理操作、权限码与消息目录
    /// </summary>
    /// <remarks>
    /// 卸载插件时调用（ADR-0085、ADR-0252）：按清单 ID 一次撤回（<see cref="OperationRegistry.RemoveOwnedBy"/>，先操作后权限码），再撤回目录，之后同一插件可以直接再登记。配置阶段按归属注册进容器的目录同样撤回。没有登记过（发现时被拒、握手还没成功）时什么都不做
    /// </remarks>
    /// <param name="registry">宿主的操作登记表</param>
    /// <param name="catalogs">宿主的目录登记表；为空时不撤回目录</param>
    /// <param name="manifestId">插件的清单 ID</param>
    /// <returns>撤回的代理操作数</returns>
    public static int Withdraw(OperationRegistry registry, MessageCatalogRegistry? catalogs, string manifestId)
    {
        var withdrawn = registry.RemoveOwnedBy(manifestId);
        catalogs?.RemoveOwnedBy(manifestId);
        return withdrawn;
    }
}
