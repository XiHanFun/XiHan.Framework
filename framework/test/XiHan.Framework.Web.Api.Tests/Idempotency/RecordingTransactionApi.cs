// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 记录提交与回滚动作的事务 API
/// </summary>
/// <remarks>
/// 释放时若未提交则视为回滚，与 SqlSugar 事务适配器一致。
/// </remarks>
internal sealed class RecordingTransactionApi : ITransactionApi, ISupportsRollback
{
    /// <summary>
    /// 是否已提交
    /// </summary>
    public bool Committed { get; private set; }

    /// <summary>
    /// 是否已回滚
    /// </summary>
    public bool RolledBack { get; private set; }

    /// <summary>
    /// 提交
    /// </summary>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回滚
    /// </summary>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放，未提交则回滚
    /// </summary>
    public void Dispose()
    {
        if (!Committed)
        {
            RolledBack = true;
        }
    }
}
