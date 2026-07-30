// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;

namespace XiHan.Framework.Bot.Models;

/// <summary>
/// 机器人通用返回结果
/// </summary>
public class BotResult
{
    /// <summary>
    /// 业务码（默认 200 表示成功，序列化到 JSON 为 int）
    /// </summary>
    public BotResultCodes Code { get; set; } = BotResultCodes.Success;

    /// <summary>
    /// 响应信息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 数据集合
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Code == BotResultCodes.Success;

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 从基础结果创建
    /// </summary>
    public static BotResult From(BotResult result, string? provider = null)
    {
        return new BotResult
        {
            Code = result.Code,
            Message = result.Message,
            Data = result.Data,
            Provider = provider
        };
    }

    /// <summary>
    /// 成功结果
    /// </summary>
    public static BotResult Success(object? data = null, string? provider = null)
    {
        return new BotResult
        {
            Code = BotResultCodes.Success,
            Data = data,
            Provider = provider
        };
    }

    /// <summary>
    /// 请求错误结果
    /// </summary>
    public static BotResult BadRequest(string? errorMessage = null, string? provider = null)
    {
        return new BotResult
        {
            Code = BotResultCodes.BadRequest,
            Message = errorMessage,
            Provider = provider
        };
    }

    /// <summary>
    /// 失败结果
    /// </summary>
    public static BotResult Failed(string? errorMessage = null, string? provider = null)
    {
        return new BotResult
        {
            Code = BotResultCodes.Failed,
            Message = errorMessage,
            Provider = provider
        };
    }
}
