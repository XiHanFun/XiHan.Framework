// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace XiHan.Framework.Web.RealTime.Services;

/// <summary>
/// 曦寒用户 ID 提供器
/// </summary>
public class XiHanUserIdProvider : IXiHanUserIdProvider
{
    /// <summary>
    /// 获取用户 ID
    /// </summary>
    /// <param name="connection">Hub 连接上下文</param>
    /// <returns></returns>
    public virtual string? GetUserId(HubConnectionContext connection)
    {
        // 优先使用 NameIdentifier (用户 ID)
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? connection.User?.FindFirst(ClaimTypes.Name)?.Value;
    }
}
