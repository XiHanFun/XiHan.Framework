#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanUserIdProvider
// Guid:7c1d2e3f-4a5b-4c1d-9e2f-7a8b9c1d2e3f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
