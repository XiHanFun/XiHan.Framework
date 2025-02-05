#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TextResult
// Guid:1b85a24d-0117-42c6-8e0f-3211304834c2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/5 20:01:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 文本处理结果
/// </summary>
public class TextResult
{
    /// <summary>
    /// 任务输出文本
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 附加元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
