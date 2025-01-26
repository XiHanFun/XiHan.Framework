#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ImageProcessingOptions
// Guid:a6321a04-1f2a-4858-8473-75e703d6d3ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:29:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 图片处理选项
/// </summary>
public class ImageProcessingOptions
{
    /// <summary>
    /// 任务类型：分类、生成、编辑等
    /// </summary>
    public string TaskType { get; set; } = "classification";

    /// <summary>
    /// 输出格式：jpeg、png 等
    /// </summary>
    public string OutputFormat { get; set; } = "jpeg";

    /// <summary>
    /// 输出分辨率
    /// </summary>
    public int Resolution { get; set; } = 512;
}
