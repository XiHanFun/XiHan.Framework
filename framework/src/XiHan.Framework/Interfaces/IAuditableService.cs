#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAuditableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可审计服务接口
/// </summary>
public interface IAuditableService : IFrameworkService
{
    /// <summary>
    /// 记录审计日志
    /// </summary>
    /// <param name="action">操作</param>
    /// <param name="details">详细信息</param>
    /// <param name="userId">用户ID</param>
    Task LogAuditAsync(string action, string details, string? userId = null);

    /// <summary>
    /// 获取审计日志
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="userId">用户ID</param>
    /// <returns>审计日志</returns>
    Task<IEnumerable<object>> GetAuditLogsAsync(DateTime startDate, DateTime endDate, string? userId = null);
}
