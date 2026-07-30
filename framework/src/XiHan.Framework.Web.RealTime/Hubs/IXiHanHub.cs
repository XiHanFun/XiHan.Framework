// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.RealTime.Hubs;

/// <summary>
/// 曦寒 Hub 接口
/// </summary>
public interface IXiHanHub
{
    /// <summary>
    /// 获取当前连接 ID
    /// </summary>
    string? ConnectionId { get; }

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// 获取当前用户名
    /// </summary>
    string? UserName { get; }
}
