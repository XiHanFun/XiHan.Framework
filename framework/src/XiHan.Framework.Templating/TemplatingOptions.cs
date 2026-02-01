#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplatingOptions
// Guid:f8a6eab7-1285-4e0e-b0c9-5f3a962c6a2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating;

/// <summary>
/// 模板选项
/// </summary>
public class TemplatingOptions
{
    /// <summary>
    /// 默认模板引擎
    /// </summary>
    public string DefaultEngine { get; set; } = "Scriban";

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// 缓存过期时间
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 最大缓存大小
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;

    /// <summary>
    /// 是否启用调试模式
    /// </summary>
    public bool EnableDebugMode { get; set; } = false;

    /// <summary>
    /// 是否启用性能监控
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = false;

    /// <summary>
    /// 模板渲染超时时间
    /// </summary>
    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 最大模板大小（字节）
    /// </summary>
    public int MaxTemplateSize { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// 默认文件编码
    /// </summary>
    public string DefaultEncoding { get; set; } = "UTF-8";

    /// <summary>
    /// 模板文件扩展名
    /// </summary>
    public string[] TemplateFileExtensions { get; set; } = [".html", ".htm", ".liquid", ".scriban"];

    /// <summary>
    /// 是否启用安全检查
    /// </summary>
    public bool EnableSecurityChecks { get; set; } = true;

    /// <summary>
    /// 是否启用模板预编译
    /// </summary>
    public bool EnablePrecompilation { get; set; } = false;

    /// <summary>
    /// 模板根目录
    /// </summary>
    public string? TemplateRootDirectory { get; set; }

    /// <summary>
    /// 布局模板目录
    /// </summary>
    public string? LayoutDirectory { get; set; }

    /// <summary>
    /// 片段模板目录
    /// </summary>
    public string? PartialDirectory { get; set; }
}
