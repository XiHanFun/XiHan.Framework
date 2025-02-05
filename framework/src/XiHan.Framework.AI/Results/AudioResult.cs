#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AudioResult
// Guid:a0d9e853-bfe8-405b-8806-0800b2aef433
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:52:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 音频处理结果
/// </summary>
public class AudioResult
{
    /// <summary>
    /// 处理后的音频数据
    /// </summary>
    public byte[] OutputAudio { get; set; } = [];

    /// <summary>
    /// 转录文本（可选）
    /// </summary>
    public string Transcription { get; set; } = string.Empty;

    /// <summary>
    /// 附加元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
