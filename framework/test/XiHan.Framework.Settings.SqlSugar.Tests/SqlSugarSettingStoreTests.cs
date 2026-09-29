// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Settings.SqlSugar.Entities;
using XiHan.Framework.Settings.SqlSugar.Stores;

namespace XiHan.Framework.Settings.SqlSugar.Tests;

/// <summary>
/// 设置存储测试
/// </summary>
public class SqlSugarSettingStoreTests : IDisposable
{
    private readonly string _databaseFile;
    private readonly SqlSugarClient _client;
    private readonly SqlSugarSettingStore _store;

    /// <summary>
    /// 构造函数，建出临时 SQLite 库与被测存储
    /// </summary>
    public SqlSugarSettingStoreTests()
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_settings_{Guid.NewGuid():N}.db");

        _client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        _client.CodeFirst.InitTables(typeof(SysSetting));

        _store = new SqlSugarSettingStore(
            new StubClientResolver(_client),
            IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload());
    }

    /// <summary>
    /// 释放临时库
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();

        if (File.Exists(_databaseFile))
        {
            File.Delete(_databaseFile);
        }
    }

    /// <summary>
    /// 写入后能读回相同值
    /// </summary>
    [Fact]
    public async Task 写入后能读回相同值()
    {
        await _store.SetAsync("App.PageSize", "20", "G", null);

        var value = await _store.GetOrNullAsync("App.PageSize", "G", null);

        Assert.Equal("20", value);
    }

    /// <summary>
    /// 不同提供者键互不覆盖
    /// </summary>
    [Fact]
    public async Task 不同提供者键互不覆盖()
    {
        await _store.SetAsync("App.PageSize", "10", "U", "user-1");
        await _store.SetAsync("App.PageSize", "30", "U", "user-2");

        Assert.Equal("10", await _store.GetOrNullAsync("App.PageSize", "U", "user-1"));
        Assert.Equal("30", await _store.GetOrNullAsync("App.PageSize", "U", "user-2"));
    }

    /// <summary>
    /// 提供者键为 null 时能精确匹配，不会被非 null 的键命中
    /// </summary>
    [Fact]
    public async Task 提供者键为Null时能精确匹配()
    {
        await _store.SetAsync("App.PageSize", "20", "G", null);

        Assert.Equal("20", await _store.GetOrNullAsync("App.PageSize", "G", null));
        Assert.Null(await _store.GetOrNullAsync("App.PageSize", "G", "somekey"));
    }

    /// <summary>
    /// 提供者键为 null 时反复写入只保留一行，验证归一化生效
    /// </summary>
    [Fact]
    public async Task 提供者键为Null时反复写入只保留一行()
    {
        await _store.SetAsync("App.PageSize", "20", "G", null);
        await _store.SetAsync("App.PageSize", "30", "G", null);

        var rows = await _client.Queryable<SysSetting>()
            .Where(item => item.SettingName == "App.PageSize" && item.ProviderName == "G")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("30", rows[0].SettingValue);
    }

    /// <summary>
    /// 未命中返回 null
    /// </summary>
    [Fact]
    public async Task 未命中返回Null()
    {
        var value = await _store.GetOrNullAsync("Not.Exists", "G", null);

        Assert.Null(value);
    }

    /// <summary>
    /// 连续两次写入覆盖同一设置且不抛异常
    /// </summary>
    [Fact]
    public async Task 连续两次写入覆盖同一设置()
    {
        await _store.SetAsync("App.PageSize", "20", "G", null);
        await _store.SetAsync("App.PageSize", "50", "G", null);

        var value = await _store.GetOrNullAsync("App.PageSize", "G", null);

        Assert.Equal("50", value);
    }

    /// <summary>
    /// 删除后读回 null
    /// </summary>
    [Fact]
    public async Task 删除后读回Null()
    {
        await _store.SetAsync("App.PageSize", "20", "G", null);
        await _store.DeleteAsync("App.PageSize", "G", null);

        var value = await _store.GetOrNullAsync("App.PageSize", "G", null);

        Assert.Null(value);
    }

    /// <summary>
    /// 批量读取对未命中的名称也返回条目，顺序与输入一致
    /// </summary>
    [Fact]
    public async Task 批量读取对未命中的名称也返回条目()
    {
        await _store.SetAsync("App.PageSize", "20", "G", null);

        var values = await _store.GetAllAsync(["App.PageSize", "App.Theme", "App.Locale"], "G", null);

        Assert.Equal(3, values.Count);
        Assert.Equal("App.PageSize", values[0].Name);
        Assert.Equal("20", values[0].Value);
        Assert.Equal("App.Theme", values[1].Name);
        Assert.Null(values[1].Value);
        Assert.Equal("App.Locale", values[2].Name);
        Assert.Null(values[2].Value);
    }

    /// <summary>
    /// 批量读取对空数组直接返回空列表
    /// </summary>
    [Fact]
    public async Task 批量读取对空数组返回空列表()
    {
        var values = await _store.GetAllAsync([], "G", null);

        Assert.Empty(values);
    }

    /// <summary>
    /// 写入命中已被其他写入者创建的行时更新而不是重复插入
    /// </summary>
    [Fact]
    public async Task 写入命中已存在的行时更新而不是重复插入()
    {
        var idGenerator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload();

        await _client.Insertable(new SysSetting(idGenerator.NextId())
        {
            SettingName = "App.NewSetting",
            ProviderName = "G",
            ProviderKey = string.Empty,
            SettingValue = "first"
        }).ExecuteCommandAsync();

        await _store.SetAsync("App.NewSetting", "second", "G", null);

        var rows = await _client.Queryable<SysSetting>()
            .Where(item => item.SettingName == "App.NewSetting")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("second", rows[0].SettingValue);
    }

    /// <summary>
    /// 插入前被竞争对手抢先创建同一设置时，回退为更新且只剩一行
    /// </summary>
    [Fact]
    public async Task 插入前被竞争对手抢先创建同一设置时回退为更新()
    {
        var idGenerator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload();

        using var competingClient = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        var racingStore = new RacingSqlSugarSettingStore(
            new StubClientResolver(_client),
            idGenerator,
            competingClient,
            () => new SysSetting(idGenerator.NextId())
            {
                SettingName = "App.Racing",
                ProviderName = "G",
                ProviderKey = string.Empty,
                SettingValue = "first"
            });

        await racingStore.SetAsync("App.Racing", "second", "G", null);

        var rows = await _client.Queryable<SysSetting>()
            .Where(item => item.SettingName == "App.Racing")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("second", rows[0].SettingValue);
    }

    /// <summary>
    /// 插入失败后重查也失败时，异常中保留原始插入异常
    /// </summary>
    [Fact]
    public async Task 插入失败后重查也失败时保留原始插入异常()
    {
        var insertFailed = false;
        _client.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                insertFailed = true;
                throw new InvalidOperationException("insert-boom");
            }

            if (insertFailed && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("requery-boom");
            }
        };

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => _store.SetAsync("App.Failing", "value", "G", null));

        var messages = Flatten(exception).Select(item => item.Message).ToList();
        Assert.Contains(messages, message => message.Contains("insert-boom", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("requery-boom", StringComparison.Ordinal));
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is not null)
        {
            foreach (var inner in Flatten(exception.InnerException))
            {
                yield return inner;
            }
        }
    }
}

/// <summary>
/// 测试用 Store 子类，在插入前的钩子里通过另一个客户端抢先插入同一个键，模拟“先查阶段都判定不存在、写入阶段才分出先后”的竞态
/// </summary>
internal sealed class RacingSqlSugarSettingStore : SqlSugarSettingStore
{
    private readonly ISqlSugarClient _competingClient;
    private readonly Func<SysSetting> _competingEntityFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="competingClient">代表竞争对手的另一个客户端，指向同一个数据库文件</param>
    /// <param name="competingEntityFactory">竞争对手抢先插入的行</param>
    public RacingSqlSugarSettingStore(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator,
        ISqlSugarClient competingClient,
        Func<SysSetting> competingEntityFactory)
        : base(clientResolver, idGenerator)
    {
        _competingClient = competingClient;
        _competingEntityFactory = competingEntityFactory;
    }

    /// <summary>
    /// 在基类确认目标行不存在、正式插入之前，抢先用另一个客户端插入同一个键
    /// </summary>
    /// <param name="entity">即将插入的实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected override async Task OnBeforeInsertAsync(SysSetting entity, CancellationToken cancellationToken)
    {
        await _competingClient.Insertable(_competingEntityFactory()).ExecuteCommandAsync();
    }
}

/// <summary>
/// 测试用客户端解析器，固定返回同一个客户端
/// </summary>
internal sealed class StubClientResolver : ISqlSugarClientResolver
{
    private readonly ISqlSugarClient _client;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">固定返回的客户端</param>
    public StubClientResolver(ISqlSugarClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 获取当前客户端
    /// </summary>
    /// <returns>固定客户端</returns>
    public ISqlSugarClient GetCurrentClient()
    {
        return _client;
    }

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>固定客户端</returns>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        return _client;
    }

    /// <summary>
    /// 按连接配置标识获取客户端
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>固定客户端</returns>
    public ISqlSugarClient GetClient(string configId)
    {
        return _client;
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns>固定标识集合</returns>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取当前布局的全部连接配置标识
    /// </summary>
    /// <returns>固定标识集合</returns>
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取所有库的客户端
    /// </summary>
    /// <returns>固定客户端集合</returns>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [_client];
    }

    /// <summary>
    /// 获取当前工作单元已登记的连接配置标识
    /// </summary>
    /// <returns>空集合，测试桩不模拟工作单元登记</returns>
    public IReadOnlyList<string> GetEnlistedConfigIds()
    {
        return [];
    }

    /// <summary>
    /// 获取底层多租户接口
    /// </summary>
    /// <returns>不返回，始终抛出</returns>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}
