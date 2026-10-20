// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.InProcess;

/// <summary>
/// 插件宿主错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class PluginErrors
{
    /// <summary>清单、种类或隔离级别不合法。</summary>
    public const string InvalidSource = "PLUGINS_VALIDATION_001";

    /// <summary>入口类型未实现 <see cref="Kernel.Plugins.IPlugin"/>，或实例清单与文件清单不一致。</summary>
    public const string InvalidEntry = "PLUGINS_VALIDATION_002";

    /// <summary>清单文件、程序集或入口类型不存在。</summary>
    public const string NotFound = "PLUGINS_NOTFOUND_001";

    /// <summary>清单声明的权限未被宿主授予。</summary>
    public const string PermissionDenied = "PLUGINS_DENIED_001";

    /// <summary>同一清单 ID 的插件已加载。</summary>
    public const string AlreadyLoaded = "PLUGINS_CONFLICT_001";

    /// <summary>加载、配置或启动时发生未处理异常。</summary>
    public const string LoadFailed = "PLUGINS_UNEXPECTED_001";
}
