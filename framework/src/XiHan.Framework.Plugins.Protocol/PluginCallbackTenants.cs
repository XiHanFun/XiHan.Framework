// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 插件回调宿主时的租户判定
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §5（ADR-0195）：插件身份没有租户声明可比对，回调元数据的 <c>xihan.tenant</c> 与发布信封的 <c>xihantenant</c> 只能是宿主交给该插件处理的调用的租户；不带租户的回调是无租户调用。两种宿主共用，「交给插件处理的调用」由各自的运行时判定
/// </remarks>
internal static class PluginCallbackTenants
{
    /// <summary>
    /// 回调元数据自报的租户越界时的错误
    /// </summary>
    /// <remarks>
    /// 元数据不带 <c>xihan.tenant</c> 时为 <see langword="null"/>；带了就交给 <paramref name="allows"/> 判定，空字符串同样要判定
    /// </remarks>
    /// <param name="metadata">回调的元数据</param>
    /// <param name="pluginId">插件的清单 ID</param>
    /// <param name="allows">该租户是不是宿主交给插件处理的调用的租户</param>
    public static Error? Check(InvocationMetadata? metadata, string pluginId, Func<string, bool> allows)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(allows);
        return metadata is not null && metadata.Baggage.TryGetValue(BaggageKeys.Tenant, out var tenant) && !allows(tenant) ? Denied(pluginId, tenant) : null;
    }

    /// <summary>
    /// 发布的信封自带的租户扩展越界时的错误
    /// </summary>
    /// <remarks>
    /// 信封不带 <c>xihantenant</c> 时为 <see langword="null"/>，发布器按当前调用补齐
    /// </remarks>
    /// <param name="envelope">发布的信封</param>
    /// <param name="pluginId">插件的清单 ID</param>
    /// <param name="allows">该租户是不是宿主交给插件处理的调用的租户</param>
    public static Error? Check(Envelope envelope, string pluginId, Func<string, bool> allows)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(allows);
        return envelope.Extensions.TryGetValue(EnvelopeExtensions.Tenant, out var tenant) && !allows(tenant) ? Denied(pluginId, tenant) : null;
    }

    /// <summary>
    /// 租户越界的错误
    /// </summary>
    private static Error Denied(string pluginId, string tenant)
        => Error.Denied(PluginErrors.CallbackDenied, PluginMessages.CallbackTenantDenied, MessageArguments.Of(("id", pluginId), ("tenant", tenant)));
}
