// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 按脚本返回或抛出的仓储替身
/// </summary>
/// <remarks>
/// 每个操作先看取消令牌；预设了 <see cref="Failure"/> 时都抛出它，模拟驱动在读写时失败；否则查找返回 <see cref="Found"/>，写入返回 <see cref="WriteResult"/>
/// </remarks>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ScriptedRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 每个操作抛出的异常
    /// </summary>
    public Exception? Failure { get; init; }

    /// <summary>
    /// 查找与查询返回的那一行
    /// </summary>
    /// <remarks>
    /// 为空时查不到
    /// </remarks>
    public TEntity? Found { get; init; }

    /// <summary>
    /// 每条写入返回的结果
    /// </summary>
    public Result WriteResult { get; init; } = Result.Success();

    /// <inheritdoc />
    public ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
        => ValueTask.FromResult(Rows(cancellationToken) is [var row] ? Maybe.Some(row) : Maybe.None<TEntity>());

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TEntity>> FindManyAsync(IReadOnlyList<TKey> ids, CancellationToken cancellationToken) => ValueTask.FromResult(Rows(cancellationToken));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken) => ValueTask.FromResult(Rows(cancellationToken));

    /// <inheritdoc />
    public ValueTask<long> CountAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken) => ValueTask.FromResult<long>(Rows(cancellationToken).Count);

    /// <inheritdoc />
    public ValueTask<Result> AddAsync(TEntity entity, CancellationToken cancellationToken) => ValueTask.FromResult(Write(cancellationToken));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> AddRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken) => ValueTask.FromResult(Write(entities, cancellationToken));

    /// <inheritdoc />
    public ValueTask<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken) => ValueTask.FromResult(Write(cancellationToken));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> UpdateRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken) => ValueTask.FromResult(Write(entities, cancellationToken));

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken) => ValueTask.FromResult(Write(cancellationToken));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> RemoveRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken) => ValueTask.FromResult(Write(entities, cancellationToken));

    /// <summary>
    /// 读到的行
    /// </summary>
    private IReadOnlyList<TEntity> Rows(CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        return Found is null ? [] : [Found];
    }

    /// <summary>
    /// 一条写入的结果
    /// </summary>
    private Result Write(CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        return WriteResult;
    }

    /// <summary>
    /// 一批写入的逐项结果
    /// </summary>
    private IReadOnlyList<Result> Write(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        return [.. entities.Select(_ => WriteResult)];
    }

    /// <summary>
    /// 已取消就按取消抛出，预设了失败就抛出它
    /// </summary>
    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            throw Failure;
        }
    }
}
