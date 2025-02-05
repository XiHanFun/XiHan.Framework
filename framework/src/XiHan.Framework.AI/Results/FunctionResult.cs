#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FunctionResult
// Guid:ce8c0f67-7879-4c48-93a6-6d6e4fb72a32
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:53:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 函数执行结果
/// </summary>
public class FunctionResult
{
    /// <summary>
    /// 函数执行的唯一标识
    /// </summary>
    public string FunctionId { get; set; } = string.Empty;

    /// <summary>
    /// 函数执行结果（JSON 格式）
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 是否执行完成
    /// </summary>
    public bool IsCompleted { get; set; }
}
