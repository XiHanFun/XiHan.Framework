#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDataExecutingHandler
// Guid:457c4556-1967-4f2a-9ad4-92bb7ac0dce3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/27 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Data.SqlSugar.Auditing;

/// <summary>
/// SqlSugar 数据执行 AOP 处理器
/// </summary>
/// <remarks>
/// 统一处理雪花主键、创建/修改/删除审计字段、租户标识和 TraceId 注入。
/// 仓储层不再关心这些基础字段，业务侧也不应手动传入 TenantId。
/// </remarks>
public sealed class SqlSugarDataExecutingHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="idGenerator">分布式 ID 生成器</param>
    public SqlSugarDataExecutingHandler(
        IServiceScopeFactory scopeFactory,
        IDistributedIdGenerator<long> idGenerator)
    {
        _scopeFactory = scopeFactory;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 处理 SqlSugar DataExecuting 事件
    /// </summary>
    /// <param name="entityInfo">数据执行模型</param>
    public void Handle(DataFilterModel entityInfo)
    {
        if (entityInfo.EntityValue is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        var currentTenant = scope.ServiceProvider.GetService<ICurrentTenant>();
        var auditContext = EntityAuditContext.From(currentUser, currentTenant?.Id);

        switch (entityInfo.OperationType)
        {
            case DataFilterType.InsertByObject:
                entityInfo.TrySetSnowflakeId(_idGenerator.NextId());
                entityInfo.ToCreated(auditContext);
                TryFillTraceId(scope.ServiceProvider, entityInfo.EntityValue);
                break;

            case DataFilterType.UpdateByObject:
                entityInfo.ToModified(auditContext);
                entityInfo.ToDeleted(auditContext);
                break;

            case DataFilterType.DeleteByObject:
                entityInfo.ToDeleted(auditContext);
                break;
        }
    }

    private static void TryFillTraceId(IServiceProvider serviceProvider, object entity)
    {
        if (entity is not ITraceableEntity traceable || !string.IsNullOrWhiteSpace(traceable.TraceId))
        {
            return;
        }

        var traceId = serviceProvider.GetService<ITraceIdProvider>()?.GetCurrentTraceId();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            traceable.TraceId = traceId;
        }
    }
}
