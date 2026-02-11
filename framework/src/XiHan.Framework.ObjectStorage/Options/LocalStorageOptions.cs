#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalStorageOptions
// Guid:3aadb9e7-709a-4451-857e-c88a8c7c2541
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// 本地存储配置
/// </summary>
public class LocalStorageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:ObjectStorage:Local";

    /// <summary>
    /// 根目录路径
    /// </summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>
    /// URL前缀
    /// </summary>
    public string UrlPrefix { get; set; } = "/uploads";
}
