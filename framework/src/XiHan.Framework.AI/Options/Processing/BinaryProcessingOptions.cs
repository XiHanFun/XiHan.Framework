#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BinaryProcessingOptions
// Guid:d22e5916-e3d1-4b1a-8a63-20ccd260b192
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:55:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options.Processing;

/// <summary>
/// 二进制处理选项
/// </summary>
public class BinaryProcessingOptions
{
    /// <summary>
    /// 任务类型（如压缩/加密）
    /// </summary>
    public string TaskType { get; set; } = "default";

    /// <summary>
    /// 可选参数
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}
