#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmptyHostingEnvironment
// Guid:2fb6ef51-e121-43ba-b38f-5afa7628c697
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 19:49:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.Web.Core.Extensions.DependencyInjection;

/// <summary>
/// 空主机环境
/// </summary>
public class EmptyHostingEnvironment : IWebHostEnvironment
{
    /// <summary>
    /// 环境名称
    /// </summary>
    public string EnvironmentName { get; set; } = null!;

    /// <summary>
    /// 应用名称
    /// </summary>
    public string ApplicationName { get; set; } = null!;

    /// <summary>
    /// Web 根路径
    /// </summary>
    public string WebRootPath { get; set; } = null!;

    /// <summary>
    /// Web 根文件提供者
    /// </summary>
    public IFileProvider WebRootFileProvider { get; set; } = null!;

    /// <summary>
    /// 内容根路径
    /// </summary>
    public string ContentRootPath { get; set; } = null!;

    /// <summary>
    /// 内容根文件提供者
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
