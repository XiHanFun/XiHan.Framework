using System.Reflection;

namespace XiHan.Framework.Metadata;

/// <summary>
/// 提供曦寒框架的元数据信息。
/// </summary>
public static class XiHanMetadata
{
    /// <summary>
    /// 获取框架名称。
    /// </summary>
    public static string Name => "XiHan.Framework";

    /// <summary>
    /// 获取框架显示名称。
    /// </summary>
    public static string DisplayName => "曦寒框架";

    /// <summary>
    /// 获取框架版权信息。
    /// </summary>
    public static string Copyright => "Copyright ©2021-Present ZhaiFanhua All Rights Reserved.";

    /// <summary>
    /// 获取框架作者。
    /// </summary>
    public static string Author => "ZhaiFanhua";

    /// <summary>
    /// 获取作者邮箱。
    /// </summary>
    public static string AuthorEmail => "me@zhaifanhua.com";

    /// <summary>
    /// 获取组织名称。
    /// </summary>
    public static string Organization => "XiHanFun";

    /// <summary>
    /// 获取组织地址。
    /// </summary>
    public static string OrganizationUrl => "https://github.com/XiHanFun";

    /// <summary>
    /// 获取代码仓库地址。
    /// </summary>
    public static string RepositoryUrl => "https://github.com/XiHanFun/XiHan.Framework";

    /// <summary>
    /// 获取文档地址。
    /// </summary>
    public static string DocumentationUrl => "https://docs.xihanfun.com";

    /// <summary>
    /// 获取许可证名称。
    /// </summary>
    public static string License => "MIT";

    /// <summary>
    /// 获取许可证地址。
    /// </summary>
    public static string LicenseUrl => "https://github.com/XiHanFun/XiHan.Framework/blob/main/LICENSE";

    /// <summary>
    /// 获取框架描述。
    /// </summary>
    public static string Description => "快速、轻量、高效、用心的开发框架和组件库。基于 .NET 10 构建。";

    /// <summary>
    /// 获取关键词集合。
    /// </summary>
    public static IReadOnlyList<string> Keywords =>
    [
        "dotnet",
        "aspnetcore",
        "csharp",
        "web",
        "xihan",
        "framework",
        "xihanfun",
        "modular",
        "extensible"
    ];

    /// <summary>
    /// 获取支持的目标框架。
    /// </summary>
    public static IReadOnlyList<string> SupportedFrameworks => ["net10.0"];

    /// <summary>
    /// 获取支持的平台。
    /// </summary>
    public static IReadOnlyList<string> SupportedPlatforms => ["Windows", "Linux", "MacOS"];

    /// <summary>
    /// 获取入口程序集名称。
    /// </summary>
    public static string EntryAssemblyName => Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    /// <summary>
    /// 获取入口程序集版本。
    /// </summary>
    public static string EntryAssemblyVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>
    /// 获取框架 Logo。
    /// </summary>
    public static string Logo => """
       _  __ ______  _____    _   __
      | |/ //  _/ / / /   |  / | / /
      |   / / // /_/ / /| | /  |/ /
     /   |_/ // __  / ___ |/ /|  /
    /_/|_/___/_/ /_/_/  |_/_/ |_/
    """;

    /// <summary>
    /// 获取框架版本字符串。
    /// </summary>
    public static string Version => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version?.ToString() ?? new Version(0, 0, 0).ToString();

    /// <summary>
    /// 获取框架完整版本。
    /// </summary>
    public static Version FullVersion => Assembly.GetAssembly(typeof(XiHanMetadata))?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// 获取框架信息摘要。
    /// </summary>
    public static string GetSummary()
    {
        return $"""
            {Name} {DisplayName} v{Version}
            {Description}

            {EntryAssemblyName} v{EntryAssemblyVersion}
            """;
    }

    /// <summary>
    /// 获取框架详细信息。
    /// </summary>
    public static string GetDetails()
    {
        return $"""
            {Name} {DisplayName} v{Version}
            {Description}
            作者: {Author} ({AuthorEmail})
            组织: {Organization} ({OrganizationUrl})
            仓库: {RepositoryUrl}
            文档: {DocumentationUrl}
            许可证: {License} ({LicenseUrl})

            {EntryAssemblyName} v{EntryAssemblyVersion}
            """;
    }
}
