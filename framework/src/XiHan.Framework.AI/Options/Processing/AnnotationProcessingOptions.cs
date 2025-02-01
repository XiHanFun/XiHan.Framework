#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AnnotationProcessingOptions
// Guid:9337d78f-1990-451a-bf4c-f3ea353fe0fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:57:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options.Processing;

/// <summary>
/// 注释处理选项
/// </summary>
public class AnnotationProcessingOptions
{
    /// <summary>
    /// 目标语言
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// 是否包含示例
    /// </summary>
    public bool IncludeExamples { get; set; } = false;
}
