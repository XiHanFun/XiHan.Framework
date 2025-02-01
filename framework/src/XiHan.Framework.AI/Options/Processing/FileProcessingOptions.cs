#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileProcessingOptions
// Guid:64d7d317-7f75-426b-9190-ce1bcf3d855c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:57:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options.Processing;

/// <summary>
/// 文件处理选项
/// </summary>
public class FileProcessingOptions
{
    /// <summary>
    /// 文件类型
    /// </summary>
    public string FileType { get; set; } = "pdf";

    /// <summary>
    /// 任务类型（如提取文本/转换格式）
    /// </summary>
    public string? TaskType { get; set; } = "conversion";
}
