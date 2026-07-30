// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.WeCom.Models;

/// <summary>
/// 文件上传返回结果
/// </summary>
public class WeComUploadResultDto
{
    /// <summary>
    /// 返回消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 介质ID
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// 媒体文件类型，分别有图片(image)、语音(voice)、视频(video)，普通文件(file)
    /// </summary>
    public string Type { get; set; } = string.Empty;
}
