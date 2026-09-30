// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.Idempotency;
using XiHan.Framework.Web.Api.SqlSugar.Entities;

namespace XiHan.Framework.Web.Api.SqlSugar.Idempotency;

/// <summary>
/// 幂等记录的 SqlSugar 存储
/// </summary>
/// <remarks>
/// 取得、释放、标记不确定与清理在新开的非事务工作单元中，经解析器客户端复制出的独立连接执行，立即提交，
/// 不随外层事务提交或回滚；
/// 完成写入经客户端解析器登记到当前工作单元，事务型工作单元内与业务同一事务提交。
/// 唯一索引建在记录键摘要上，由数据库串行化同一键的并发取得。
/// 完成与不确定记录超过保留期后可被重新取得；请求路径超过端点列长度时截断写入。
/// </remarks>
public class SqlSugarIdempotencyStore : IIdempotencyStore
{
    private const int EndpointMaxLength = 512;

    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly XiHanIdempotencyOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>构造函数</summary>
    public SqlSugarIdempotencyStore(
        ISqlSugarClientResolver clientResolver,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanIdempotencyOptions> options,
        TimeProvider timeProvider)
    {
        _clientResolver = clientResolver;
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyRecordKey key, string fingerprint, bool isTransactional, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        using var unitOfWork = BeginIndependent();
        using var client = _clientResolver.GetClientForEntity<SysIdempotencyRecord>().CopyNew();
        var result = await AcquireCoreAsync(client, key, fingerprint, isTransactional, cancellationToken);
        await unitOfWork.CompleteAsync(CancellationToken.None);
        return result;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(IdempotencyRecordKey key, Guid ownerToken, StoredResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(response);

        var keyHash = key.ComputeHash();
        var now = _timeProvider.GetUtcNow();
        var expiresTime = now.Add(_options.CompletedRetention);
        var client = _clientResolver.GetClientForEntity<SysIdempotencyRecord>();

        var affected = await client.Updateable<SysIdempotencyRecord>()
            .SetColumns(record => new SysIdempotencyRecord
            {
                Status = SysIdempotencyRecord.StatusCompleted,
                ResponseStatus = response.StatusCode,
                ResponseBody = response.Body,
                CompletedTime = now,
                ExpiresTime = expiresTime
            })
            .Where(record => record.KeyHash == keyHash &&
                             record.OwnerToken == ownerToken &&
                             record.Status == SysIdempotencyRecord.StatusProcessing)
            .ExecuteCommandAsync(cancellationToken);

        if (affected != 1)
        {
            throw new InvalidOperationException("幂等记录不存在、拥有者令牌不符或已不是处理中状态，无法写入完成。");
        }
    }

    /// <inheritdoc />
    /// <remarks>只删除处理中记录；与业务同事务提交的完成记录不受影响。</remarks>
    public async Task ReleaseAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var keyHash = key.ComputeHash();
        using var unitOfWork = BeginIndependent();
        using var client = _clientResolver.GetClientForEntity<SysIdempotencyRecord>().CopyNew();
        await client.Deleteable<SysIdempotencyRecord>()
            .Where(record => record.KeyHash == keyHash &&
                             record.OwnerToken == ownerToken &&
                             record.Status == SysIdempotencyRecord.StatusProcessing)
            .ExecuteCommandAsync(cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkIndeterminateAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var keyHash = key.ComputeHash();
        var expiresTime = _timeProvider.GetUtcNow().Add(_options.CompletedRetention);
        using var unitOfWork = BeginIndependent();
        using var client = _clientResolver.GetClientForEntity<SysIdempotencyRecord>().CopyNew();
        await client.Updateable<SysIdempotencyRecord>()
            .SetColumns(record => new SysIdempotencyRecord
            {
                Status = SysIdempotencyRecord.StatusIndeterminate,
                ExpiresTime = expiresTime
            })
            .Where(record => record.KeyHash == keyHash &&
                             record.OwnerToken == ownerToken &&
                             record.Status == SysIdempotencyRecord.StatusProcessing)
            .ExecuteCommandAsync(cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
    }

    /// <summary>
    /// 删除已过期的完成记录与不确定记录
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除的记录数</returns>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        using var unitOfWork = BeginIndependent();
        using var client = _clientResolver.GetClientForEntity<SysIdempotencyRecord>().CopyNew();
        var deleted = await client.Deleteable<SysIdempotencyRecord>()
            .Where(record => (record.Status == SysIdempotencyRecord.StatusCompleted ||
                              record.Status == SysIdempotencyRecord.StatusIndeterminate) &&
                             record.ExpiresTime <= now)
            .ExecuteCommandAsync(cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return deleted;
    }

    private IUnitOfWork BeginIndependent()
    {
        return _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);
    }

    private async Task<IdempotencyAcquireResult> AcquireCoreAsync(ISqlSugarClient client, IdempotencyRecordKey key, string fingerprint, bool isTransactional, CancellationToken cancellationToken)
    {
        var keyHash = key.ComputeHash();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var now = _timeProvider.GetUtcNow();
            var ownerToken = Guid.NewGuid();
            try
            {
                await client.Insertable(CreateRecord(key, keyHash, fingerprint, ownerToken, isTransactional, now))
                    .ExecuteCommandAsync(cancellationToken);
                return IdempotencyAcquireResult.Acquired(ownerToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var existing = await client.Queryable<SysIdempotencyRecord>()
                    .Where(record => record.KeyHash == keyHash)
                    .FirstAsync(cancellationToken);
                if (existing is null)
                {
                    throw;
                }

                var decided = await ResolveExistingAsync(client, existing, fingerprint, ownerToken, now, cancellationToken);
                if (decided is not null)
                {
                    return decided;
                }
            }
        }

        return IdempotencyAcquireResult.InProgress();
    }

    /// <summary>
    /// 按已存在记录的状态给出取得结果；过期的完成或不确定记录删除后返回 null，由调用方重试插入
    /// </summary>
    /// <remarks>过期与租约到期的时间比较在数据库条件中进行。</remarks>
    private async Task<IdempotencyAcquireResult?> ResolveExistingAsync(
        ISqlSugarClient client,
        SysIdempotencyRecord existing,
        string fingerprint,
        Guid ownerToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (existing.Status != SysIdempotencyRecord.StatusProcessing &&
            await client.Queryable<SysIdempotencyRecord>()
                .AnyAsync(record => record.BasicId == existing.BasicId && record.ExpiresTime <= now, cancellationToken))
        {
            await client.Deleteable<SysIdempotencyRecord>()
                .Where(record => record.BasicId == existing.BasicId &&
                                 record.Status == existing.Status &&
                                 record.ExpiresTime <= now)
                .ExecuteCommandAsync(cancellationToken);
            return null;
        }

        if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return IdempotencyAcquireResult.Conflict();
        }

        switch (existing.Status)
        {
            case SysIdempotencyRecord.StatusCompleted:
                return IdempotencyAcquireResult.Replay(new StoredResponse(existing.ResponseStatus ?? 200, existing.ResponseBody));
            case SysIdempotencyRecord.StatusProcessing when existing.IsTransactional:
                var leaseExpiresTime = now.Add(_options.ProcessingLease);
                var previousToken = existing.OwnerToken;
                var taken = await client.Updateable<SysIdempotencyRecord>()
                    .SetColumns(record => new SysIdempotencyRecord { OwnerToken = ownerToken, LeaseExpiresTime = leaseExpiresTime })
                    .Where(record => record.BasicId == existing.BasicId &&
                                     record.OwnerToken == previousToken &&
                                     record.Status == SysIdempotencyRecord.StatusProcessing &&
                                     record.LeaseExpiresTime <= now)
                    .ExecuteCommandAsync(cancellationToken);
                return taken == 1 ? IdempotencyAcquireResult.Acquired(ownerToken) : IdempotencyAcquireResult.InProgress();
            case SysIdempotencyRecord.StatusProcessing:
                return IdempotencyAcquireResult.InProgress();
            default:
                return IdempotencyAcquireResult.Indeterminate();
        }
    }

    private SysIdempotencyRecord CreateRecord(IdempotencyRecordKey key, string keyHash, string fingerprint, Guid ownerToken, bool isTransactional, DateTimeOffset now)
    {
        return new SysIdempotencyRecord(Guid.NewGuid())
        {
            KeyHash = keyHash,
            TenantId = key.TenantId,
            SubjectId = key.SubjectId,
            HttpMethod = key.Method,
            Endpoint = key.Endpoint.Length > EndpointMaxLength ? key.Endpoint[..EndpointMaxLength] : key.Endpoint,
            IdempotencyKey = key.Key,
            Fingerprint = fingerprint,
            Status = SysIdempotencyRecord.StatusProcessing,
            OwnerToken = ownerToken,
            IsTransactional = isTransactional,
            LeaseExpiresTime = now.Add(_options.ProcessingLease),
            CreatedTime = now
        };
    }
}
