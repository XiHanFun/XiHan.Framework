// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;

namespace XiHan.Framework.Kernel;

/// <summary>
/// XiHan 框架元数据。
/// 提供框架名称、版本、作者、仓库地址等身份标识。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public static class XiHanMetadata
{
    /// <summary>
    /// 框架 NuGet 包名前缀。
    /// </summary>
    public const string Name = "XiHan.Framework";

    /// <summary>
    /// 框架显示名称。
    /// </summary>
    public const string DisplayName = "曦寒框架";

    /// <summary>
    /// 版权及贡献者信息。
    /// </summary>
    public const string Author = "XiHanFun Contributors";

    /// <summary>
    /// 组织名称。
    /// </summary>
    public const string Organization = "XiHanFun";

    /// <summary>
    /// 仓库地址。
    /// </summary>
    public const string RepositoryUrl = "https://github.com/XiHanFun/XiHan.Framework";

    /// <summary>
    /// 文档地址。
    /// </summary>
    public const string DocumentationUrl = "https://docs.xihanfun.com";

    /// <summary>
    /// 许可证类型。
    /// </summary>
    public const string License = "MIT";

    /// <summary>
    /// 许可证地址。
    /// </summary>
    public const string LicenseUrl = "https://github.com/XiHanFun/XiHan.Framework/blob/main/LICENSE";

    /// <summary>
    /// 支持的平台。
    /// </summary>
    public static readonly string[] SupportedPlatforms = ["Windows", "Linux", "MacOS"];

    /// <summary>
    /// 框架版本号，从程序集信息中读取。
    /// </summary>
    public static string Version => typeof(XiHanMetadata).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(XiHanMetadata).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    /// 当前运行中应用的入口程序集名称。
    /// </summary>
    public static string EntryAssemblyName => Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    /// <summary>
    /// 获取框架信息摘要。
    /// </summary>
    public static string GetSummary()
        => $"{Name} v{Version} — {Organization} ({RepositoryUrl})";
}
