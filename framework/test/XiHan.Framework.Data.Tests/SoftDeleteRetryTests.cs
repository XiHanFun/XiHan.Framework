// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Exceptions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 软删与恢复在持久化失败后的实体状态测试
/// </summary>
/// <remarks>
/// 跑在真实 SQLite 上，挂与生产同口径的软删过滤器与自动写过滤设置。
/// 断言的是「持久化失败后传入实体的删除状态、删除审计字段与行版本保持调用前的值，同一实体重试能真正落库」。
/// </remarks>
public sealed class SoftDeleteRetryTests : IDisposable
{
    private const string ConfigId = "Main";
    private const long DeleterId = 7;
    private const string DeleterName = "alice";

    private static readonly DateTimeOffset DeletedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan-soft-delete-retry-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _scope;
    private readonly SqlSugarSoftDeleteRepository<SoftNote, long> _repository;
    private bool _failNextUpdate;

    /// <summary>
    /// 建库并挂软删过滤器
    /// </summary>
    public SoftDeleteRetryTests()
    {
        _scope = new SqlSugarScope(
            new ConnectionConfig
            {
                ConfigId = ConfigId,
                ConnectionString = ConnectionString,
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                MoreSettings = new ConnMoreSettings
                {
                    IsAutoUpdateQueryFilter = true,
                    IsAutoDeleteQueryFilter = true
                }
            },
            provider =>
            {
                provider.QueryFilter.AddTableFilter<ISoftDelete>(entity => !entity.IsDeleted);
                provider.Aop.OnLogExecuting = (sql, _) =>
                {
                    if (_failNextUpdate && sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
                    {
                        _failNextUpdate = false;
                        throw new InvalidOperationException("模拟数据库错误");
                    }
                };
            });

        var client = _scope.GetConnectionScope(ConfigId);
        client.CodeFirst.InitTables<SoftNote>();

        _repository = new SqlSugarSoftDeleteRepository<SoftNote, long>(new FixedClientResolver(client));
    }

    private string ConnectionString => $"DataSource={_databasePath};Pooling=False";

    /// <summary>
    /// 软删被取消后实体状态不变，同一实体重试落库
    /// </summary>
    [Fact]
    public async Task SoftDeleteAsync_CancelledThenRetried_ShouldPersist()
    {
        SeedActive(1);
        var note = LoadRaw(1);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repository.SoftDeleteAsync(note, new CancellationToken(true)));

        Assert.False(note.IsDeleted);
        Assert.Null(note.DeletedTime);

        await _repository.SoftDeleteAsync(note);

        Assert.True(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 软删遇到行版本冲突后实体状态与行版本均不变
    /// </summary>
    [Fact]
    public async Task SoftDeleteAsync_VersionConflict_ShouldKeepEntityState()
    {
        SeedActive(1);
        var note = LoadRaw(1);
        var originalVersion = note.RowVersion;
        BumpRowVersion(1);

        _ = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _repository.SoftDeleteAsync(note));

        Assert.False(note.IsDeleted);
        Assert.Null(note.DeletedTime);
        Assert.Equal(originalVersion, note.RowVersion);
        Assert.False(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 软删遇到数据库错误后实体状态与行版本均不变，同一实体重试落库
    /// </summary>
    [Fact]
    public async Task SoftDeleteAsync_DatabaseErrorThenRetried_ShouldPersist()
    {
        SeedActive(1);
        var note = LoadRaw(1);
        var originalVersion = note.RowVersion;
        _failNextUpdate = true;

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.SoftDeleteAsync(note));

        Assert.False(note.IsDeleted);
        Assert.Equal(originalVersion, note.RowVersion);

        await _repository.SoftDeleteAsync(note);

        Assert.True(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 恢复被取消后删除状态与删除审计字段不变，同一实体重试落库
    /// </summary>
    [Fact]
    public async Task RestoreAsync_CancelledThenRetried_ShouldPersist()
    {
        SeedDeleted(1);
        var note = LoadRaw(1);
        var deletedTime = note.DeletedTime;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repository.RestoreAsync(note, new CancellationToken(true)));

        AssertStillDeleted(note, deletedTime);

        await _repository.RestoreAsync(note);

        Assert.False(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 批量软删被取消后整批实体状态不变，同批重试落库
    /// </summary>
    [Fact]
    public async Task SoftDeleteRangeAsync_CancelledThenRetried_ShouldPersist()
    {
        SeedActive(1);
        SeedActive(2);
        var notes = new[] { LoadRaw(1), LoadRaw(2) };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repository.SoftDeleteRangeAsync(notes, new CancellationToken(true)));

        Assert.All(notes, note =>
        {
            Assert.False(note.IsDeleted);
            Assert.Null(note.DeletedTime);
        });

        await _repository.SoftDeleteRangeAsync(notes);

        Assert.True(LoadRaw(1).IsDeleted);
        Assert.True(LoadRaw(2).IsDeleted);
    }

    /// <summary>
    /// 批量恢复被取消后整批删除状态与删除审计字段不变，同批重试落库
    /// </summary>
    [Fact]
    public async Task RestoreRangeAsync_CancelledThenRetried_ShouldPersist()
    {
        SeedDeleted(1);
        SeedDeleted(2);
        var notes = new[] { LoadRaw(1), LoadRaw(2) };
        var deletedTimes = notes.Select(note => note.DeletedTime).ToArray();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repository.RestoreRangeAsync(notes, new CancellationToken(true)));

        for (var index = 0; index < notes.Length; index++)
        {
            AssertStillDeleted(notes[index], deletedTimes[index]);
        }

        await _repository.RestoreRangeAsync(notes);

        Assert.False(LoadRaw(1).IsDeleted);
        Assert.False(LoadRaw(2).IsDeleted);
    }

    /// <summary>
    /// 软删成功后再次软删同一实体静默跳过
    /// </summary>
    [Fact]
    public async Task SoftDeleteAsync_AfterSuccess_RepeatShouldBeNoOp()
    {
        SeedActive(1);
        var note = LoadRaw(1);

        await _repository.SoftDeleteAsync(note);
        await _repository.SoftDeleteAsync(note);

        Assert.True(LoadRaw(1).IsDeleted);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _scope.Dispose();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static void AssertStillDeleted(SoftNote note, DateTimeOffset? expectedDeletedTime)
    {
        Assert.True(note.IsDeleted);
        Assert.NotNull(note.DeletedTime);
        Assert.Equal(expectedDeletedTime, note.DeletedTime);
        Assert.Equal(DeleterId, note.DeletedId);
        Assert.Equal(DeleterName, note.DeletedBy);
    }

    private void SeedActive(long id)
    {
        using var client = CreateRawClient();
        _ = client.Insertable(new SoftNote(id) { Text = $"note-{id}" }).ExecuteCommand();
    }

    private void SeedDeleted(long id)
    {
        using var client = CreateRawClient();
        _ = client.Insertable(new SoftNote(id)
        {
            Text = $"note-{id}",
            IsDeleted = true,
            DeletedTime = DeletedAt,
            DeletedId = DeleterId,
            DeletedBy = DeleterName
        }).ExecuteCommand();
    }

    private void BumpRowVersion(long id)
    {
        using var client = CreateRawClient();
        _ = client.Updateable<SoftNote>()
            .SetColumns(note => note.RowVersion == note.RowVersion + 1)
            .Where(note => note.BasicId == id)
            .ExecuteCommand();
    }

    private SoftNote LoadRaw(long id)
    {
        using var client = CreateRawClient();
        return client.Queryable<SoftNote>()
            .Where(note => note.BasicId == id)
            .First();
    }

    private SqlSugarClient CreateRawClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = ConnectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    /// <summary>测试用软删实体。</summary>
    [SugarTable(TableName = "Soft_Retry_Note")]
    public class SoftNote : SugarDeletionEntity<long>
    {
        /// <summary>构造函数。</summary>
        public SoftNote()
        {
        }

        /// <summary>构造函数。</summary>
        /// <param name="basicId">主键</param>
        public SoftNote(long basicId) : base(basicId)
        {
        }

        /// <summary>内容。</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>固定返回同一客户端的解析器替身。</summary>
    private sealed class FixedClientResolver(ISqlSugarClient client) : ISqlSugarClientResolver
    {
        /// <summary>
        /// 获取当前客户端
        /// </summary>
        public ISqlSugarClient GetCurrentClient() => client;

        /// <summary>
        /// 获取实体对应客户端
        /// </summary>
        /// <param name="entityType">实体类型</param>
        public ISqlSugarClient GetClientForEntity(Type entityType) => client;

        /// <summary>
        /// 按连接配置标识获取客户端
        /// </summary>
        /// <param name="configId">连接配置标识</param>
        public ISqlSugarClient GetClient(string configId) => client;

        /// <summary>
        /// 获取全部连接配置标识
        /// </summary>
        public IReadOnlyCollection<string> GetAllConfigIds() => [ConfigId];

        /// <summary>
        /// 获取当前布局的连接配置标识
        /// </summary>
        public IReadOnlyList<string> GetCurrentLayoutConfigIds() => [ConfigId];

        /// <summary>
        /// 获取全部客户端
        /// </summary>
        public IEnumerable<ISqlSugarClient> GetAllClients() => [client];

        /// <summary>
        /// 底层多库容器，本用例不涉及
        /// </summary>
        public ITenant AsTenant() => throw new NotSupportedException("用例不涉及多库切换。");
    }
}
