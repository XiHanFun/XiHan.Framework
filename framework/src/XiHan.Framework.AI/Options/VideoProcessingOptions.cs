#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VideoProcessingOptions
// Guid:f638855f-4cf6-4dde-86d5-bebf9f4162af
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:32:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 视频处理选项
/// </summary>
public class VideoProcessingOptions
{
    /// <summary>
    /// 任务类型：目标检测、转码、字幕生成等
    /// </summary>
    public string TaskType { get; set; } = "object-detection";

    /// <summary>
    /// 输出格式
    /// </summary>
    public string OutputFormat { get; set; } = "mp4";

    /// <summary>
    /// 是否包含音频
    /// </summary>
    public bool IncludeAudio { get; set; } = true;
}
