// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 插件回调宿主时宿主所用的身份
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主共用（<c>spec/plugin/invocation.md</c> §5，ADR-0260）：宿主以插件身份执行回调，主体 ID 是清单 ID、认证方案是 <see cref="Scheme"/>、没有声明
/// </remarks>
internal static class PluginIdentity
{
    /// <summary>
    /// 回调时宿主所用主体的认证方案
    /// </summary>
    public const string Scheme = "Plugin";

    /// <summary>
    /// 按回调的元数据建调用上下文，主体是插件自己
    /// </summary>
    /// <remarks>
    /// 元数据的还原同 <see cref="PluginMetadata.CreateContext"/>：元数据里的 <c>xihan.principal</c> 被覆盖成清单 ID，截止时间不晚于 <paramref name="notAfter"/>。租户由 Tenant 阶段按 Baggage 解析，调用方要先把它限定为宿主交给插件处理的调用的租户（ADR-0195）
    /// </remarks>
    /// <param name="scopedServices">回调所在作用域的容器</param>
    /// <param name="metadata">回调的元数据</param>
    /// <param name="manifestId">插件的清单 ID</param>
    /// <param name="notAfter">截止时间的上限（gRPC 调用自身的截止时间、Wasm 当前调用的截止时间）；没有时为空</param>
    /// <param name="cancellationToken">回调的取消令牌</param>
    public static InvocationContext CreateContext(IServiceProvider scopedServices, InvocationMetadata? metadata, string manifestId, DateTimeOffset? notAfter, CancellationToken cancellationToken)
    {
        var context = PluginMetadata.CreateContext(scopedServices, metadata, manifestId, cancellationToken, notAfter);
        // 新建的上下文还没有租户可带：租户由 Tenant 阶段按 Baggage 解析
        context.SetSecurity(new SecurityContext { Principal = new Principal(manifestId, Scheme), Tenant = null });
        return context;
    }
}
