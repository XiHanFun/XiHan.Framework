#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIResult
// Guid:9ce8f646-7816-4761-bd64-31425983752c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// AI结果基类
/// </summary>
public class XiHanAIResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage = string.Empty;

    /// <summary>
    /// 令牌使用情况
    /// </summary>
    public TokenUsage? TokenUsage;

    /// <summary>
    /// 原始响应JSON
    /// </summary>
    public string? RawResponse;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanAIResult()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isSuccess">是否成功</param>
    public XiHanAIResult(bool isSuccess)
    {
        IsSuccess = isSuccess;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="errorMessage">错误消息</param>
    public XiHanAIResult(bool isSuccess, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// 令牌使用情况
/// </summary>
public class TokenUsage
{
    /// <summary>
    /// 提示令牌数
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// 完成令牌数
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 总令牌数
    /// </summary>
    public int TotalTokens { get; set; }
}
