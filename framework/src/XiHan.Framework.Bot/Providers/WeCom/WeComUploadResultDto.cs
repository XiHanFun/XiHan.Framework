#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WeComUploadResultDto
// Guid:9385c398-2a75-4259-9328-c0039cd26823
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/08/10 02:20:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.WeCom;

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
