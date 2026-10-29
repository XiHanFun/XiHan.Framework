// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database.Tests.Fixtures;

/// <summary>
/// 每个操作都抛出的审计记录仓储。
/// </summary>
/// <remarks>
/// 模拟审计存储不可用。在特性装配前注册，持久化 Provider 的 <c>TryAdd</c> 就不再覆盖它。
/// </remarks>
internal sealed class ThrowingAuditRepository : IRepository<AuditRecord, Guid>
{
    /// <summary>
    /// 每个操作抛出的异常消息。
    /// </summary>
    public const string Message = "The audit store is offline.";

    /// <inheritdoc />
    public ValueTask<Maybe<AuditRecord>> FindAsync(Guid id, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> FindManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> ListAsync(EntityQuery<AuditRecord> query, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<long> CountAsync(EntityQuery<AuditRecord> query, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<Result> AddAsync(AuditRecord entity, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<IReadOnlyList<Result>> AddRangeAsync(IReadOnlyList<AuditRecord> entities, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<Result> UpdateAsync(AuditRecord entity, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<IReadOnlyList<Result>> UpdateRangeAsync(IReadOnlyList<AuditRecord> entities, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<Result> RemoveAsync(AuditRecord entity, CancellationToken cancellationToken) => throw Offline();

    /// <inheritdoc />
    public Task<IReadOnlyList<Result>> RemoveRangeAsync(IReadOnlyList<AuditRecord> entities, CancellationToken cancellationToken) => throw Offline();

    /// <summary>
    /// 存储不可用的异常。
    /// </summary>
    private static InvalidOperationException Offline() => new(Message);
}
