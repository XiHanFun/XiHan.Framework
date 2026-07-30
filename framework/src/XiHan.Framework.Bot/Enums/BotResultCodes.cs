// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace XiHan.Framework.Bot.Enums;

/// <summary>
/// 机器人通用返回结果状态码
/// </summary>
public enum BotResultCodes
{
    /// <summary>
    /// 请求成功
    /// </summary>
    [Description("请求成功")]
    Success = 200,

    /// <summary>
    /// 请求错误
    /// </summary>
    [Description("请求错误")]
    BadRequest = 400,

    /// <summary>
    /// 服务器内部错误
    /// </summary>
    [Description("服务器内部错误")]
    Failed = 500,
}
