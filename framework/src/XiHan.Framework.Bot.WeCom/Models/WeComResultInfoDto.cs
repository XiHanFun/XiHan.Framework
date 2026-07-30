// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.WeCom.Models;

/// <summary>
/// 结果信息
/// </summary>
public record WeComResultInfoDto
{
    /// <summary>
    /// 结果代码
    /// 成功 0
    /// </summary>
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    /// <summary>
    /// 结果消息
    /// 成功 ok
    /// </summary>
    [JsonPropertyName("errmsg")]
    public string ErrMsg { get; set; } = string.Empty;

    /// <summary>
    /// 媒体文件类型，分别有图片(image)、语音(voice)、视频(video)，普通文件(file)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 媒体文件上传后获取的唯一标识，3天内有效
    /// </summary>
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// 媒体文件上传时间戳
    /// </summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
}
