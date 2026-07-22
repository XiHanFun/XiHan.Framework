// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.Versioning;

namespace XiHan.Framework.Metadata;

/// <summary>
/// 曦寒元数据信息
/// </summary>
public static class XiHanMetadata
{
    /// <summary>
    /// 框架名称
    /// </summary>
    public const string Name = "XiHan.Framework";

    /// <summary>
    /// 框架显示名称
    /// </summary>
    public const string DisplayName = "曦寒框架";

    /// <summary>
    /// 框架版权信息
    /// </summary>
    public const string Copyright = "Copyright ©2021-Present ZhaiFanhua All Rights Reserved.";

    /// <summary>
    /// 框架作者
    /// </summary>
    public const string Author = "ZhaiFanhua";

    /// <summary>
    /// 框架作者邮箱
    /// </summary>
    public const string AuthorEmail = "me@zhaifanhua.com";

    /// <summary>
    /// 框架组织
    /// </summary>
    public const string Organization = "XiHanFun";

    /// <summary>
    /// 框架组织网址
    /// </summary>
    public const string OrganizationUrl = "https://github.com/XiHanFun";

    /// <summary>
    /// 框架仓库地址
    /// </summary>
    public const string RepositoryUrl = "https://github.com/XiHanFun/XiHan.Framework";

    /// <summary>
    /// 框架文档地址
    /// </summary>
    public const string DocumentationUrl = "https://docs.xihanfun.com";

    /// <summary>
    /// 框架许可证
    /// </summary>
    public const string License = "MIT";

    /// <summary>
    /// 框架许可证地址
    /// </summary>
    public const string LicenseUrl = "https://github.com/XiHanFun/XiHan.Framework/blob/main/LICENSE";

    /// <summary>
    /// 框架关键词
    /// </summary>
    public static readonly string[] Keywords =
    [
        "dotnet",
        "aspnetcore",
        "csharp",
        "web",
        "webapp",
        "xihan",
        "framework",
        "zhaifanhua",
        "xihanfun",
        "modular",
        "extensible"
    ];

    /// <summary>
    /// 框架支持的平台
    /// </summary>
    public static readonly string[] SupportedPlatforms =
    [
        "Windows",
        "Linux",
        "MacOS"
    ];

    /// <summary>
    /// 框架当前目标框架标识（从程序集 <see cref="TargetFrameworkAttribute"/> 解析，如 "net10.0"）
    /// </summary>
    public static string TargetFramework { get; } = ResolveTargetFramework();

    /// <summary>
    /// 框架支持的 .NET 版本（由当前目标框架派生，升级大版本无需手改）
    /// </summary>
    public static string[] SupportedFrameworks { get; } = [TargetFramework];

    /// <summary>
    /// 框架描述（.NET 版本由目标框架派生）
    /// </summary>
    public static string Description { get; } = $"快速、轻量、高效、用心的开发框架和组件库。基于 {ResolveDotNetDisplay()} 构建。";

    /// <summary>
    /// 入口程序名称
    /// </summary>
    public static string EntryAssemblyName => Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    /// <summary>
    /// 入口程序版本
    /// </summary>
    public static string EntryAssemblyVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>
    /// 曦寒框架标志
    /// </summary>
    public static string Logo => @"
   _  __ ______  _____    _   __
  | |/ //  _/ / / /   |  / | / /
  |   / / // /_/ / /| | /  |/ /
 /   |_/ // __  / ___ |/ /|  /
/_/|_/___/_/ /_/_/  |_/_/ |_/";

    /// <summary>
    /// 曦寒框架寄语
    /// </summary>
    public static string SendWord => @"
碧落降恩承淑颜，共挚崎缘挽曦寒。
迁般故事终成忆，谨此葳蕤换思短。
              —— 致她";

    /// <summary>
    /// 框架版本
    /// </summary>
    public static string Version => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version?.ToString() ?? new Version(0, 0, 0).ToString();

    /// <summary>
    /// 框架完整版本信息
    /// </summary>
    public static Version FullVersion => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// 框架主要版本
    /// </summary>
    public static int MajorVersion => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version?.Major ?? 0;

    /// <summary>
    /// 框架次要版本
    /// </summary>
    public static int MinorVersion => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version?.Minor ?? 0;

    /// <summary>
    /// 框架修订版本
    /// </summary>
    public static int PatchVersion => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version?.Build ?? 0;

    /// <summary>
    /// 获取框架信息摘要
    /// </summary>
    /// <returns>框架信息摘要</returns>
    public static string GetSummary()
    {
        return $"""
            {Name} {DisplayName} v{Version}
            {Description}
            {SendWord}

            {EntryAssemblyName} v{EntryAssemblyVersion}
            """;
    }

    /// <summary>
    /// 获取框架详细信息
    /// </summary>
    /// <returns>框架详细信息</returns>
    public static string GetDetails()
    {
        return $"""
            {Name} {DisplayName} v{Version}
            {Description}
            {SendWord}
            作者: {Author} ({AuthorEmail})
            组织: {Organization} ({OrganizationUrl})
            仓库: {RepositoryUrl}
            文档: {DocumentationUrl}
            许可证: {License} ({LicenseUrl})

            {EntryAssemblyName} v{EntryAssemblyVersion}
            """;
    }

    /// <summary>
    /// 从程序集的 <see cref="TargetFrameworkAttribute"/> 解析目标框架标识（如 "net10.0"）
    /// </summary>
    private static string ResolveTargetFramework()
    {
        // FrameworkName 形如 ".NETCoreApp,Version=v10.0"
        var frameworkName = Assembly.GetAssembly(typeof(XiHanMetadata))?
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        if (!string.IsNullOrEmpty(frameworkName))
        {
            const string marker = "Version=v";
            var idx = frameworkName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var version = frameworkName[(idx + marker.Length)..];
                return $"net{version}";
            }
        }

        return "net10.0";
    }

    /// <summary>
    /// 把目标框架标识转成友好显示（如 ".NET 10"）
    /// </summary>
    private static string ResolveDotNetDisplay()
    {
        var tfm = ResolveTargetFramework();
        var version = tfm.StartsWith("net", StringComparison.OrdinalIgnoreCase) ? tfm[3..] : tfm;
        var major = version.Split('.')[0];
        return $".NET {major}";
    }
}
