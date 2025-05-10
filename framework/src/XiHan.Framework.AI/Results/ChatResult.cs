#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ChatResult
// Guid:49d79315-e582-45ef-b944-9585c5d9c8fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 聊天结果
/// </summary>
public class ChatResult : AIResult
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ChatResult() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="content">内容</param>
    public ChatResult(bool isSuccess, string content) : base(isSuccess)
    {
        Content = content;
    }

    /// <summary>
    /// 回复内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 响应时间(毫秒)
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// 工具调用结果
    /// </summary>
    public ToolCallResult[]? ToolCalls { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="content">回复内容</param>
    /// <returns>成功结果</returns>
    public static ChatResult Success(string content)
    {
        return new ChatResult(true, content);
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>失败结果</returns>
    public static ChatResult Failure(string errorMessage)
    {
        var result = new ChatResult(false, string.Empty)
        {
            ErrorMessage = errorMessage
        };
        return result;
    }
}

/// <summary>
/// 工具调用结果
/// </summary>
public class ToolCallResult
{
    /// <summary>
    /// 工具ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数JSON
    /// </summary>
    public string ArgumentsJson { get; set; } = string.Empty;
}
