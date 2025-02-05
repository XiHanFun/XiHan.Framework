#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TextProcessingOptions
// Guid:1fbd6e0b-8fb1-48e6-b251-5f37c7c2ff9c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:29:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options.Processing;

/// <summary>
/// 文本处理选项
/// </summary>
public class TextProcessingOptions
{
    /// <summary>
    /// 任务类型：生成、分析、分类等
    /// </summary>
    public string TaskType { get; set; } = "generation";

    /// <summary>
    /// 控制随机性
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// 最大生成长度
    /// </summary>
    public int MaxTokens { get; set; } = 500;

    /// <summary>
    /// 停止词
    /// </summary>
    public List<string> StopWords { get; set; } = [];
}
