#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultEntityAuditContextProvider
// Guid:a1a10747-d886-47e5-bc66-663e6e85bb39
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Auditing;

/// <summary>
/// 默认实体审计上下文提供器
/// </summary>
/// <remarks>
/// 从 <see cref="ICurrentUser"/> 和 <see cref="ICurrentTenant"/> 中填充审计记录的基础字段。
/// 业务层可通过注册自定义 <see cref="IEntityAuditContextProvider"/> 覆盖（例如补充 HTTP 请求上下文信息）。
/// </remarks>
public class DefaultEntityAuditContextProvider : IEntityAuditContextProvider
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant? _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentUser">当前用户</param>
    /// <param name="currentTenant">当前租户（可选）</param>
    public DefaultEntityAuditContextProvider(
        ICurrentUser currentUser,
        ICurrentTenant? currentTenant = null)
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    /// <inheritdoc />
    public EntityDiffLogRecord CreateBaseRecord()
    {
        var record = new EntityDiffLogRecord();

        // 用户信息
        if (_currentUser.IsAuthenticated)
        {
            record.UserId = _currentUser.UserId;
            record.UserName = _currentUser.UserName ?? _currentUser.Name;
        }

        // 租户信息
        record.TenantId = _currentUser.TenantId ?? _currentTenant?.Id;

        return record;
    }

    /// <inheritdoc />
    public bool ShouldAudit(Type entityType)
    {
        if (entityType is null)
        {
            return false;
        }

        // 排除框架内部实体和审计日志自身
        var fullName = entityType.FullName ?? string.Empty;
        return !fullName.StartsWith("XiHan.Framework.Auditing", StringComparison.Ordinal) &&
               !fullName.Contains("AuditLog", StringComparison.Ordinal) &&
               !fullName.Contains("DiffLog", StringComparison.Ordinal);
    }
}
