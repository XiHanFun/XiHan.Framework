// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.InProcess;

/// <summary>
/// 进程内插件宿主的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，宿主应用用 <c>MessageCatalog.FromEmbeddedResources("plugins", typeof(PluginMessages))</c> 注册。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class PluginMessages
{
    /// <summary>插件目录里的清单不合法。参数：<c>directory</c>、<c>reason</c>。</summary>
    public const string ManifestInvalid = "plugins.manifest.invalid";

    /// <summary>清单描述的不是进程内插件。参数：<c>id</c>、<c>kind</c>、<c>isolation</c>。</summary>
    public const string NotInProcess = "plugins.manifest.not_in_process";

    /// <summary>插件声明的权限未被授予。参数：<c>id</c>、<c>permissions</c>。</summary>
    public const string PermissionsNotGranted = "plugins.permission.not_granted";

    /// <summary>清单缺少 <c>package</c> 或 <c>entry</c>。参数：<c>id</c>。</summary>
    public const string EntryRequired = "plugins.manifest.entry_required";

    /// <summary>插件程序集不存在。参数：<c>path</c>。</summary>
    public const string AssemblyMissing = "plugins.assembly.missing";

    /// <summary>插件已加载。参数：<c>id</c>。</summary>
    public const string AlreadyLoaded = "plugins.plugin.already_loaded";

    /// <summary>插件未加载。参数：<c>id</c>。</summary>
    public const string NotLoaded = "plugins.plugin.not_loaded";

    /// <summary>入口类型不存在。参数：<c>entry</c>、<c>package</c>。</summary>
    public const string EntryNotFound = "plugins.entry.not_found";

    /// <summary>入口类型未实现 <c>IPlugin</c>。参数：<c>entry</c>。</summary>
    public const string EntryNotPlugin = "plugins.entry.not_plugin";

    /// <summary>入口类型的清单 ID 与清单文件不一致。参数：<c>actual</c>、<c>expected</c>。</summary>
    public const string EntryManifestMismatch = "plugins.entry.manifest_mismatch";

    /// <summary>插件加载失败。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string LoadFailed = "plugins.plugin.load_failed";

    /// <summary>插件停止失败。参数：<c>id</c>、<c>reason</c>。</summary>
    public const string StopFailed = "plugins.plugin.stop_failed";
}
