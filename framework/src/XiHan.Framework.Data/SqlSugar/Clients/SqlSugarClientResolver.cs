#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarClientResolver
// Guid:8d6f1c89-2e4a-4a7f-9d9b-1b5f6a8c3e2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 客户端解析器默认实现
/// </summary>
/// <remarks>
/// 基于 <see cref="SqlSugarScope"/>（线程安全单例）+ <see cref="ISqlSugarTenantConnectionResolver"/> 组合。
/// 当前租户上下文变化时，由 <c>ISqlSugarTenantConnectionResolver</c> 重新解析 ConfigId。
/// </remarks>
public sealed class SqlSugarClientResolver : ISqlSugarClientResolver
{
    private const string TransactionApiPrefix = "SqlSugarTransaction";

    private readonly SqlSugarScope _sqlSugarScope;
    private readonly ISqlSugarTenantConnectionResolver _tenantConnectionResolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope">SqlSugar 根作用域</param>
    /// <param name="tenantConnectionResolver">租户连接解析器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    public SqlSugarClientResolver(
        SqlSugarScope sqlSugarScope,
        ISqlSugarTenantConnectionResolver tenantConnectionResolver,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _sqlSugarScope = sqlSugarScope;
        _tenantConnectionResolver = tenantConnectionResolver;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <inheritdoc />
    public ISqlSugarClient GetCurrentClient()
    {
        var configId = _tenantConnectionResolver.ResolveCurrentConfigId();
        return GetClient(configId);
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        var client = _sqlSugarScope.GetConnectionScope(configId.Trim());
        EnlistCurrentUnitOfWork(client);
        return client;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return _tenantConnectionResolver.GetConfigIds();
    }

    /// <inheritdoc />
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        foreach (var configId in GetAllConfigIds())
        {
            yield return _sqlSugarScope.GetConnectionScope(configId);
        }
    }

    /// <inheritdoc />
    public ITenant AsTenant()
    {
        return _sqlSugarScope;
    }

    private void EnlistCurrentUnitOfWork(ISqlSugarClient client)
    {
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork is null ||
            unitOfWork.IsReserved ||
            unitOfWork.IsDisposed ||
            unitOfWork.IsCompleted ||
            !unitOfWork.Options.IsTransactional)
        {
            return;
        }

        var configId = client.CurrentConnectionConfig.ConfigId?.ToString();
        if (string.IsNullOrWhiteSpace(configId))
        {
            return;
        }

        var transactionKey = $"{TransactionApiPrefix}:{configId}";
        unitOfWork.GetOrAddTransactionApi(
            transactionKey,
            () => new SqlSugarTransactionApi(client, unitOfWork.Options.IsolationLevel));
    }
}
