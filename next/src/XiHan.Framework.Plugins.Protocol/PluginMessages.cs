// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 插件协议各载体共用的消息键；文案在 <c>Localization/&lt;区域&gt;.json</c>（RFC-0010）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class PluginMessages
{
    /// <summary>消息目录名。</summary>
    public const string CatalogName = "plugins.protocol";

    /// <summary>插件目录里的清单不合法。参数：<c>directory</c>、<c>reason</c>。</summary>
    public const string ManifestInvalid = "plugins.process.manifest_invalid";

    /// <summary>清单描述的不是进程外插件。参数：<c>id</c>、<c>kind</c>、<c>isolation</c>。</summary>
    public const string NotOutOfProcess = "plugins.process.not_out_of_process";

    /// <summary>清单没有声明入口可执行文件。参数：<c>id</c>。</summary>
    public const string EntryRequired = "plugins.process.entry_required";

    /// <summary>入口可执行文件不存在。参数：<c>path</c>。</summary>
    public const string ExecutableMissing = "plugins.process.executable_missing";

    /// <summary>插件声明的权限未被授予。参数：<c>id</c>、<c>permissions</c>。</summary>
    public const string PermissionsNotGranted = "plugins.process.permissions_not_granted";

    /// <summary>插件已加载。参数：<c>id</c>。</summary>
    public const string AlreadyLoaded = "plugins.process.already_loaded";

    /// <summary>插件未加载。参数：<c>id</c>。</summary>
    public const string NotLoaded = "plugins.process.not_loaded";

    /// <summary>握手行不合法或协议版本没有交集。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string HandshakeInvalid = "plugins.handshake.invalid";

    /// <summary>握手响应与清单不一致。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string HandshakeMismatch = "plugins.handshake.mismatch";

    /// <summary>某阶段超时。参数：<c>id</c>、<c>phase</c>。</summary>
    public const string ProcessTimeout = "plugins.process.timeout";

    /// <summary>插件进程不可用。参数：<c>id</c>、<c>state</c>。</summary>
    public const string ProcessUnavailable = "plugins.process.unavailable";

    /// <summary>插件进程异常退出。参数：<c>id</c>、<c>exitCode</c>。</summary>
    public const string ProcessCrashed = "plugins.process.crashed";

    /// <summary>某阶段失败。参数：<c>id</c>、<c>phase</c>、<c>reason</c>。</summary>
    public const string ProcessFailed = "plugins.process.failed";

    /// <summary>插件回调需要的权限未授予。参数：<c>id</c>、<c>permission</c>。</summary>
    public const string CallbackDenied = "plugins.callback.denied";

    /// <summary>插件回调的会话令牌无效。</summary>
    public const string SessionInvalid = "plugins.callback.session_invalid";

    /// <summary>宿主没有消息发布器，无法代插件发布。参数：<c>id</c>。</summary>
    public const string MessagingUnavailable = "plugins.callback.messaging_unavailable";

    /// <summary>Wasm 模块不合法。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string ModuleInvalid = "plugins.module.invalid";

    /// <summary>Wasm 实例陷阱。参数：<c>id</c>、<c>trap</c>。</summary>
    public const string ModuleTrapped = "plugins.module.trapped";

    /// <summary>沙箱资源不足。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string SandboxExhausted = "plugins.sandbox.exhausted";

    /// <summary>
    /// 延迟装载的目录；首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </summary>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(PluginMessages)));

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => _catalog.Value;
}
