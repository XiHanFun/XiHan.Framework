#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AudioProcessingOptions
// Guid:479f4b16-42a8-4cdb-b5cf-856d722bdc05
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:32:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 音频处理选项
/// </summary>
public class AudioProcessingOptions
{
    /// <summary>
    /// 任务类型：转录、合成、分析等
    /// </summary>
    public string TaskType { get; set; } = "transcription";

    /// <summary>
    /// 音频语言
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// 输出格式
    /// </summary>
    public string OutputFormat { get; set; } = "wav";
}
