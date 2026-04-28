#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarTransactionApi
// Guid:1f206c0f-b7c2-48ac-a7a9-c8ad1be134ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/27 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Data;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 工作单元事务适配器
/// </summary>
public sealed class SqlSugarTransactionApi : ITransactionApi, ISupportsRollback
{
    private readonly ISqlSugarClient _dbClient;
    private readonly bool _ownsTransaction;
    private bool _completed;
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbClient">SqlSugar 客户端</param>
    /// <param name="isolationLevel">事务隔离级别</param>
    public SqlSugarTransactionApi(ISqlSugarClient dbClient, IsolationLevel? isolationLevel)
    {
        _dbClient = dbClient;

        if (!_dbClient.Ado.IsNoTran())
        {
            return;
        }

        if (isolationLevel.HasValue)
        {
            _dbClient.Ado.BeginTran(isolationLevel.Value);
        }
        else
        {
            _dbClient.Ado.BeginTran();
        }

        _ownsTransaction = true;
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_completed || !_ownsTransaction)
        {
            _completed = true;
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _dbClient.Ado.CommitTranAsync();
        _completed = true;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed || !_ownsTransaction)
        {
            _completed = true;
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _dbClient.Ado.RollbackTranAsync();
        _completed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_completed && _ownsTransaction)
        {
            _dbClient.Ado.RollbackTran();
            _completed = true;
        }
    }
}
