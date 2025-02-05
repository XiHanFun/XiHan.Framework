#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AnnotationResult
// Guid:b7431b22-02d5-4e6d-a761-9283f848c4a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:00:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 注释结果
/// </summary>
public class AnnotationResult
{
    /// <summary>
    /// 注释后的文本
    /// </summary>
    public string AnnotatedText { get; set; } = string.Empty;

    /// <summary>
    /// 额外元信息
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
