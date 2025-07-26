#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHan
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5b5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework;

/// <summary>
/// 曦寒项目信息
/// </summary>
public static class XiHan
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
    /// 框架标语
    /// </summary>
    public const string Tagline = "快速、轻量、高效、用心的开发框架和组件库。基于 .NET 9 构建。";

    /// <summary>
    /// 框架描述
    /// </summary>
    public const string Description = "专为前后端分离的 ASP.NET Core 应用设计的模块化框架，基于 .NET 9，优先使用原生功能，减少第三方依赖，确保模块化、可扩展和易用性。";

    /// <summary>
    /// 框架关键词
    /// </summary>
    public static readonly string[] Keywords =
    [
        "csharp",
        "aspnetcore",
        "web",
        "webapp",
        "xihan",
        "framework",
        "zhaifanhua",
        "xihanfun",
        "dotnet",
        "net9",
        "modular",
        "extensible"
    ];

    /// <summary>
    /// 框架支持的 .NET 版本
    /// </summary>
    public static readonly string[] SupportedFrameworks =
    [
        "net9.0"
    ];

    /// <summary>
    /// 框架支持的平台
    /// </summary>
    public static readonly string[] SupportedPlatforms =
    [
        "Windows",
        "Linux",
        "macOS"
    ];

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
              —— 致她
";

    /// <summary>
    /// 框架版本
    /// </summary>
    public static string Version => Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// 框架主要版本
    /// </summary>
    public static int MajorVersion => Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.Major ?? 0;

    /// <summary>
    /// 框架次要版本
    /// </summary>
    public static int MinorVersion => Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.Minor ?? 0;

    /// <summary>
    /// 框架修订版本
    /// </summary>
    public static int PatchVersion => Assembly.GetAssembly(typeof(XiHan))?.GetName().Version?.Build ?? 0;

    /// <summary>
    /// 框架完整版本信息
    /// </summary>
    public static Version FullVersion => Assembly.GetAssembly(typeof(XiHan))?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// 获取框架信息摘要
    /// </summary>
    /// <returns>框架信息摘要</returns>
    public static string GetSummary()
    {
        return $"{DisplayName} v{Version} - {Tagline}";
    }

    /// <summary>
    /// 获取框架详细信息
    /// </summary>
    /// <returns>框架详细信息</returns>
    public static string GetDetails()
    {
        return $"""
            欢迎使用{DisplayName} v{Version}
            {Description}
            {Tagline}
            {SendWord}

            作者: {Author} ({AuthorEmail})
            组织: {Organization} ({OrganizationUrl})
            仓库: {RepositoryUrl}
            文档: {DocumentationUrl}
            许可证: {License} ({LicenseUrl})

            支持的框架: {string.Join(", ", SupportedFrameworks)}
            支持的平台: {string.Join(", ", SupportedPlatforms)}
            项目版本:{EntryAssemblyVersion}
            """;
    }
}
