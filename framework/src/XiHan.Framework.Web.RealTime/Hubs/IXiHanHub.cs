#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanHub
// Guid:2d5e6f7a-8b9c-4d3e-9f1a-2b3c4d5e6f7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 04:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
