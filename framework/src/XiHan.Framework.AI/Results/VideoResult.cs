#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VideoResult
// Guid:13132983-1d96-4b0a-9991-e49f46bebc4f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:53:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 视频处理结果
/// </summary>
public class VideoResult
{
    /// <summary>
    /// 处理后的视频数据
    /// </summary>
    public required byte[] OutputVideo { get; set; }

    /// <summary>
    /// 检测到的目标信息（可选）
    /// </summary>
    public List<string> DetectedObjects { get; set; } = [];

    /// <summary>
    /// 附加元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
