// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;

namespace XiHan.Framework.Kernel;

/// <summary>
/// 框架元数据。提供框架名称、版本、作者、仓库地址等身份信息。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public static class XiHanMetadata
{
    public const string Name = "XiHan.Framework";
    public const string DisplayName = "曦寒框架";
    public const string Author = "XiHanFun Contributors";
    public const string Organization = "XiHanFun";
    public const string RepositoryUrl = "https://github.com/XiHanFun/XiHan.Framework";
    public const string DocumentationUrl = "https://docs.xihanfun.com";
    public const string License = "MIT";
    public const string LicenseUrl = "https://github.com/XiHanFun/XiHan.Framework/blob/main/LICENSE";

    public static readonly string[] SupportedPlatforms = ["Windows", "Linux", "MacOS"];

    /// <summary>
    /// 框架版本。
    /// </summary>
    public static string Version => typeof(XiHanMetadata).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(XiHanMetadata).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    /// 运行中应用的入口程序集名称。
    /// </summary>
    public static string EntryAssemblyName => Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    /// <summary>
    /// 获取框架信息摘要。
    /// </summary>
    public static string GetSummary()
        => $"{Name} v{Version} — {Organization} ({RepositoryUrl})";
}
