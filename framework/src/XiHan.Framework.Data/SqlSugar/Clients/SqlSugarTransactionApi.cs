// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Data;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 工作单元事务适配器
/// </summary>
/// <remarks>
/// 必须传入<b>具体</b>的上下文客户端（<c>SqlSugarScopeProvider.ScopedContext</c> 物化出的 <c>SqlSugarProvider</c>，
/// 由 <c>SqlSugarClientResolver</c> 钉住后提供），使 Begin/Commit/Rollback 与业务操作作用于同一连接；
/// 若传入 <c>SqlSugarScopeProvider</c>，其 <c>.Ado</c> 按异步上下文动态解析，提交帧可能落在另一个无事务的连接上静默空转丢写。
/// </remarks>
public sealed class SqlSugarTransactionApi : ITransactionApi, ISupportsRollback
{
    private readonly ISqlSugarClient _dbClient;
    private readonly bool _ownsTransaction;
    private readonly bool _ownsClient;
    private bool _completed;
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbClient">SqlSugar 客户端（须为钉住的具体上下文客户端，见类型注释）</param>
    /// <param name="isolationLevel">事务隔离级别</param>
    /// <param name="requireOwnTransaction">
    /// 是否必须由本适配器开启事务。<see langword="true"/> 时若连接上已有事务则直接抛出，
    /// 而不是退化为不提交也不回滚的空操作——静默退化会让调用方误以为拿到了独立事务。
    /// </param>
    /// <param name="ownsClient">
    /// 是否由本适配器负责释放 <paramref name="dbClient"/>；用于工作单元独占的隔离连接。
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="requireOwnTransaction"/> 为 <see langword="true"/> 但连接上已存在事务。
    /// </exception>
    public SqlSugarTransactionApi(
        ISqlSugarClient dbClient,
        IsolationLevel? isolationLevel,
        bool requireOwnTransaction = false,
        bool ownsClient = false)
    {
        _dbClient = dbClient;
        _ownsClient = ownsClient;

        if (!_dbClient.Ado.IsNoTran())
        {
            if (requireOwnTransaction)
            {
                // 隔离连接理应是全新的、其上不可能有事务。走到这里说明连接不是独占的，
                // 继续下去内层提交将是空操作、写入由外层事务决定去留，必须响亮失败。
                throw new InvalidOperationException(
                    "要求独立事务的工作单元拿到了一条已存在事务的连接，无法保证独立提交。");
            }

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

        // fail-closed：本 API 持有具体连接，Begin 之后事务应始终在位；
        // 此刻查无事务说明事务与连接再次脱钩（或被外部提交）——静默 no-op 会把「写入丢失」伪装成提交成功，必须显式报错。
        if (_dbClient.Ado.IsNoTran())
        {
            throw new InvalidOperationException("提交失败：当前连接上已无活动事务（事务与连接脱钩），拒绝静默空提交。");
        }

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
        try
        {
            if (!_completed && _ownsTransaction)
            {
                _dbClient.Ado.RollbackTran();
                _completed = true;
            }
        }
        finally
        {
            // 隔离连接由本适配器独占，必须随工作单元一起释放，否则每次 requiresNew 都会泄漏一条连接。
            if (_ownsClient)
            {
                _dbClient.Dispose();
            }
        }
    }
}
