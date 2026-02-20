#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EntityAuditContext
// Guid:58f8f513-d60e-4f38-9f68-bf8b2f885f1e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 14:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// 实体审计赋值上下文
/// </summary>
public readonly record struct EntityAuditContext(long? UserId, string? UserName, long? TenantId)
{
    /// <summary>
    /// 从当前用户与租户上下文创建审计上下文
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <param name="tenantId">当前租户标识</param>
    /// <returns></returns>
    public static EntityAuditContext From(ICurrentUser? currentUser, long? tenantId)
    {
        if (currentUser?.IsAuthenticated != true)
        {
            return new EntityAuditContext(null, null, tenantId);
        }

        return new EntityAuditContext(currentUser.UserId, currentUser.UserName, tenantId);
    }
}
