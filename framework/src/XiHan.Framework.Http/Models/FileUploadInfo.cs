// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
