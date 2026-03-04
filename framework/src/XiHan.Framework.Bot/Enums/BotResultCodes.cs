#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotResultCodes
// Guid:b8d18694-3045-471e-af04-6d4373632468
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 02:36:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
