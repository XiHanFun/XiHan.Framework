// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol.Proxies;

/// <summary>
/// 把一个插件的权限码与代理操作登记进宿主。
/// </summary>
/// <remarks>
/// 权限码是清单 <c>contributes.permissions</c> 声明的，代理操作是握手声明的。进程外与 Wasm 宿主共用（<c>plugin/invocation.md</c> §1，ADR-0080）：一次 <see cref="OperationRegistry.AddRange"/>，归属是插件的清单 ID，权限码与操作全进或全不进；卸载时按同一归属一次撤回（<see cref="Withdraw"/>，ADR-0085）。
/// </remarks>
internal static class PluginContributions
{
    /// <summary>
    /// 登记插件的权限码与代理操作。
    /// </summary>
    /// <remarks>
    /// 只登记清单声明了的权限码，显示名取权限码本身（插件可在握手的消息目录里给 <c>permissions.&lt;码&gt;.name</c> 的译文）。声明的码与已登记的重复、操作重名、引用了未登记的权限码或挂上的核对不通过，都返回失败且登记表不变；配置阶段的调用方据此启动失败，之后的调用方按发现时被拒处理。
    /// </remarks>
    /// <param name="registry">宿主的操作登记表（绑定了权限登记表）。</param>
    /// <param name="manifest">插件清单（已由清单读取器核对过权限码文法与重复）。</param>
    /// <param name="handshake">成功的握手。</param>
    /// <param name="invoker">代理操作执行时转交的插件运行时。</param>
    public static Result Register(OperationRegistry registry, Manifest manifest, HandshakeInfo handshake, IPluginInvoker invoker)
        => registry.AddRange(
            manifest.Id,
            handshake.Operations.Select(operation => (OperationDescriptor)new ProxyOperationDescriptor(operation.Contract, manifest.Id, invoker, operation.Schema)).ToList(),
            manifest.Contributes.Permissions.Select(static code => new PermissionDefinition(code, code)).ToList());

    /// <summary>
    /// 撤回插件登记的权限码与代理操作。
    /// </summary>
    /// <remarks>
    /// 卸载插件时调用（ADR-0085）：按清单 ID 一次撤回（<see cref="OperationRegistry.RemoveOwnedBy"/>，先操作后权限码），之后同一插件可以直接再登记。没有登记过（发现时被拒、握手还没成功）时什么都不做。
    /// </remarks>
    /// <param name="registry">宿主的操作登记表。</param>
    /// <param name="manifestId">插件的清单 ID。</param>
    /// <returns>撤回的代理操作数。</returns>
    public static int Withdraw(OperationRegistry registry, string manifestId) => registry.RemoveOwnedBy(manifestId);
}
