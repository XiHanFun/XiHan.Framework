// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 协议 <c>xihan.plugin.v1</c> 的常量与版本协商（RFC-0017）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class PluginProtocol
{
    /// <summary>
    /// 本实现的协议版本。
    /// </summary>
    public const uint Version = 1;

    /// <summary>
    /// 插件回调时携带会话令牌的元数据键。
    /// </summary>
    public const string SessionMetadataKey = "xihan-plugin-session";

    /// <summary>
    /// 回调失败时携带框架错误码的尾部元数据键。
    /// </summary>
    public const string ErrorCodeTrailer = "xihan-error-code";

    /// <summary>
    /// 环境变量：宿主支持的协议版本（逗号分隔，升序）。
    /// </summary>
    public const string ProtocolVersionsVariable = "XIHAN_PLUGIN_PROTOCOL_VERSIONS";

    /// <summary>
    /// 环境变量：宿主的原语层大版本。
    /// </summary>
    public const string PrimitivesVariable = "XIHAN_PLUGIN_PRIMITIVES";

    /// <summary>
    /// 环境变量：会话令牌。
    /// </summary>
    public const string SessionVariable = "XIHAN_PLUGIN_SESSION";

    /// <summary>
    /// 环境变量：宿主回调端点（<c>&lt;network&gt; &lt;address&gt;</c>）。
    /// </summary>
    public const string HostAddressVariable = "XIHAN_PLUGIN_HOST_ADDRESS";

    /// <summary>
    /// 环境变量：实例 ID。
    /// </summary>
    public const string InstanceVariable = "XIHAN_PLUGIN_INSTANCE";

    /// <summary>
    /// 环境变量：宿主默认区域。
    /// </summary>
    public const string LocaleVariable = "XIHAN_PLUGIN_LOCALE";

    /// <summary>
    /// 本实现支持的协议版本，升序。
    /// </summary>
    public static IReadOnlyList<uint> SupportedVersions { get; } = [Version];

    /// <summary>
    /// 版本协商。
    /// </summary>
    /// <remarks>
    /// 取双方都支持的最高版本；没有交集为无值。
    /// </remarks>
    public static Maybe<uint> Negotiate(IEnumerable<uint> hostVersions, IEnumerable<uint> pluginVersions)
    {
        ArgumentNullException.ThrowIfNull(hostVersions);
        ArgumentNullException.ThrowIfNull(pluginVersions);
        var host = hostVersions.ToHashSet();
        var selected = pluginVersions.Where(host.Contains).DefaultIfEmpty(0u).Max();
        return selected > 0 ? Maybe.Some(selected) : default;
    }
}
