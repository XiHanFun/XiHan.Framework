// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 插件协议各载体共用的错误码。
/// </summary>
/// <remarks>
/// 进程外、Wasm。<c>PLUGINS_*</c>，见 <c>spec/conventions/error-codes.md</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class PluginErrors
{
    /// <summary>
    /// 插件清单、种类或隔离级别不合法。
    /// </summary>
    public const string InvalidSource = "PLUGINS_VALIDATION_001";

    /// <summary>
    /// 握手行不合法，或协议版本没有交集。
    /// </summary>
    public const string InvalidHandshake = "PLUGINS_VALIDATION_003";

    /// <summary>
    /// 握手响应与清单不一致。
    /// </summary>
    public const string HandshakeMismatch = "PLUGINS_VALIDATION_004";

    /// <summary>
    /// 插件目录、清单或可执行文件不存在，或插件未加载。
    /// </summary>
    public const string NotFound = "PLUGINS_NOTFOUND_001";

    /// <summary>
    /// 插件声明的权限未被宿主授予。
    /// </summary>
    public const string PermissionDenied = "PLUGINS_DENIED_001";

    /// <summary>
    /// 插件回调的会话令牌无效，或所需权限未授予。
    /// </summary>
    public const string CallbackDenied = "PLUGINS_DENIED_002";

    /// <summary>
    /// 同一清单 ID 的插件已加载。
    /// </summary>
    public const string AlreadyLoaded = "PLUGINS_CONFLICT_001";

    /// <summary>
    /// 插件进程未就绪、正在停止、已退出或不可达。
    /// </summary>
    public const string Unavailable = "PLUGINS_UNAVAILABLE_001";

    /// <summary>
    /// 握手、配置、启动或停止超时。
    /// </summary>
    public const string Timeout = "PLUGINS_TIMEOUT_001";

    /// <summary>
    /// 配置、启动、停止或调用时发生未预期的失败。
    /// </summary>
    public const string Unexpected = "PLUGINS_UNEXPECTED_001";

    /// <summary>
    /// 插件进程异常退出，或 Wasm 实例陷阱 / 导出函数返回 0。
    /// </summary>
    public const string Crashed = "PLUGINS_UNEXPECTED_002";

    /// <summary>
    /// Wasm 模块不合法，或运行期加载含中间件贡献的插件。
    /// </summary>
    public const string InvalidModule = "PLUGINS_VALIDATION_005";

    /// <summary>
    /// 沙箱资源不足：回调嵌套超限、客体内存分配失败。
    /// </summary>
    public const string ResourceExhausted = "PLUGINS_UNAVAILABLE_002";

    /// <summary>
    /// 宿主进程不满足载体要求：Windows 强制用户态影子栈（CET）时 Wasm 陷阱会终止进程。
    /// </summary>
    public const string HostUnsupported = "PLUGINS_UNAVAILABLE_003";
}
