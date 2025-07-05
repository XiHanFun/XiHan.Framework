#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileUploadInfo
// Guid:b69a8917-2aff-4ccd-8ef2-83e83256d941
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/5 1:20:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Http.Models;

/// <summary>
/// 文件上传信息
/// </summary>
public class FileUploadInfo
{
    /// <summary>
    /// 文件流
    /// </summary>
    public Stream FileStream { get; set; } = null!;

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = "file";

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; set; }
}
