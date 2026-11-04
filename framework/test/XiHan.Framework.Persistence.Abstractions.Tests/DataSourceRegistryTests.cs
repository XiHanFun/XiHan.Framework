// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 运行时数据源注册表的复用、替换、失效、容量与健康
/// </summary>
/// <remarks>
/// <see cref="DataSourceRegistry"/>：按定义复用与替换、批量移除、容量、测试连接、健康项、不支持时的明确错误与连接串脱敏。用记录调用的替身 Provider，不连真实数据库；探测由用例直接调 <see cref="DataSourceRegistry.ProbeAsync"/>，不等定时器
/// </remarks>
public sealed class DataSourceRegistryTests
{
    /// <summary>
    /// 替身连接串里的口令
    /// </summary>
    private const string Secret = "S3cret-Value-9";

    [Fact]
    public async Task GetOrAdd_SameDefinition_ReturnsSameSource_ChangedDefinition_ReplacesAndDisposesOld()
    {
        var provider = new FakeProvider();
        await using var registry = Create(provider);

        var first = (await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken)).ValueOrThrow();
        var again = (await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken)).ValueOrThrow();
        var changed = (await registry.GetOrAddAsync(Definition("orders", "Host=other"), TestContext.Current.CancellationToken)).ValueOrThrow();

        Assert.Same(first, again);
        Assert.NotSame(first, changed);
        Assert.True(((FakeSource)first).Disposed);
        Assert.False(((FakeSource)changed).Disposed);
        Assert.Equal(2, provider.Created);
        Assert.Equal(["orders"], registry.Names);
        Assert.Same(changed, (await registry.GetAsync("orders", TestContext.Current.CancellationToken)).ValueOrThrow());
    }

    [Fact]
    public async Task GetOrAdd_UnknownDatabaseType_IsValidation_AndKeepsExistingSource()
    {
        await using var registry = Create(new FakeProvider());
        var existing = (await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken)).ValueOrThrow();

        var result = await registry.GetOrAddAsync(new DataSourceDefinition("orders", "nosuchdb", $"Host=x;Password={Secret}"), TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceInvalid, result.Error!.Code);
        Assert.Equal(PersistenceMessages.DataSourceTypeUnknown, result.Error.MessageKey);
        Assert.Same(existing, (await registry.GetAsync("orders", TestContext.Current.CancellationToken)).ValueOrThrow());
    }

    [Fact]
    public async Task GetOrAdd_ProviderRejectsConnectionString_IsValidation_WithRedactedReason()
    {
        var provider = new FakeProvider { RejectWith = $"Keyword not supported in 'Host=x;Password={Secret}': {Secret}" };
        await using var registry = Create(provider);

        var result = await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceInvalid, result.Error!.Code);
        Assert.Equal(PersistenceMessages.DataSourceInvalid, result.Error.MessageKey);
        AssertNoSecret(result.Error);
        Assert.Empty(registry.Names);
    }

    [Fact]
    public async Task GetOrAdd_BeyondCapacity_IsUnavailable_ButReplacingAnExistingNameIsAllowed()
    {
        await using var registry = Create(new FakeProvider(), new DataSourceOptions { MaxCount = 2 });
        (await registry.GetOrAddAsync(Definition("a"), TestContext.Current.CancellationToken)).ValueOrThrow();
        (await registry.GetOrAddAsync(Definition("b"), TestContext.Current.CancellationToken)).ValueOrThrow();

        var full = await registry.GetOrAddAsync(Definition("c"), TestContext.Current.CancellationToken);
        var replaced = await registry.GetOrAddAsync(Definition("b", "Host=changed"), TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceCapacityExceeded, full.Error!.Code);
        Assert.Equal(2, full.Error.Details!["capacity"]);
        Assert.True(replaced.IsSuccess, replaced.ToString());
        Assert.Equal(["a", "b"], registry.Names);
    }

    [Fact]
    public async Task Remove_IsBatch_ReportsEachName_AndDisposes()
    {
        await using var registry = Create(new FakeProvider());
        var orders = (await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken)).ValueOrThrow();
        (await registry.GetOrAddAsync(Definition("reports"), TestContext.Current.CancellationToken)).ValueOrThrow();

        var removed = await registry.RemoveAsync(["orders", "missing", "orders"], TestContext.Current.CancellationToken);
        var empty = await registry.RemoveAsync([], TestContext.Current.CancellationToken);
        var lookup = await registry.GetAsync("orders", TestContext.Current.CancellationToken);

        Assert.Equal([true, false, false], removed);
        Assert.Empty(empty);
        Assert.True(((FakeSource)orders).Disposed);
        Assert.Equal(PersistenceErrors.DataSourceNotFound, lookup.Error!.Code);
        Assert.Equal(["reports"], registry.Names);
        Assert.DoesNotContain(registry.Check(), static s => s.Name == DataSourceRegistry.HealthPrefix + "orders");
    }

    [Fact]
    public async Task Test_ProbesATemporarySource_WithoutRegisteringIt()
    {
        var provider = new FakeProvider();
        await using var registry = Create(provider);

        var ok = await registry.TestAsync(Definition("orders"), TestContext.Current.CancellationToken);
        provider.FailWith = $"password authentication failed (Password={Secret})";
        var failed = await registry.TestAsync(Definition("orders"), TestContext.Current.CancellationToken);

        Assert.True(ok.IsSuccess, ok.ToString());
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, failed.Error!.Code);
        AssertNoSecret(failed.Error);
        Assert.Empty(registry.Names);
        Assert.All(provider.Sources, static s => Assert.True(s.Disposed));
    }

    [Fact]
    public async Task Test_ProbeThatThrows_IsUnavailable_WithRedactedReason()
    {
        var provider = new FakeProvider { ThrowOnProbe = $"socket closed while sending Password={Secret}" };
        await using var registry = Create(provider);

        var failed = await registry.TestAsync(Definition("orders"), TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceUnavailable, failed.Error!.Code);
        AssertNoSecret(failed.Error);
    }

    [Fact]
    public async Task Health_StartsAsStarted_ThenReadyOrDegraded_WithoutConnectionString()
    {
        var provider = new FakeProvider();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        await using var registry = new DataSourceRegistry([provider], new DataSourceOptions(), clock);
        provider.ProbeGate = new TaskCompletionSource();
        (await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken)).ValueOrThrow();

        // 登记后立即探的那一次被挡住：此时尚未探过
        var started = Assert.Single(registry.Check());
        Assert.Equal(DataSourceRegistry.HealthPrefix + "orders", started.Name);
        Assert.Equal(LifecycleState.Started, started.State);
        Assert.Equal("fake", started.Data["databaseType"]);
        Assert.Equal("fake-provider", started.Data["provider"]);
        provider.ProbeGate.SetResult();
        provider.ProbeGate = null;

        await registry.ProbeAsync(TestContext.Current.CancellationToken);
        var ready = Assert.Single(registry.Check());
        provider.FailWith = $"connection refused; Password={Secret}";
        await registry.ProbeAsync(TestContext.Current.CancellationToken);
        var degraded = Assert.Single(registry.Check());

        Assert.Equal(LifecycleState.Ready, ready.State);
        Assert.Equal(clock.GetUtcNow(), ready.CheckedAt);
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, degraded.Error!.Code);
        AssertNoSecret(degraded.Error);
        Assert.DoesNotContain(Secret, degraded.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutProvider_EveryUseIsNotSupported()
    {
        await using var registry = new DataSourceRegistry([], new DataSourceOptions(), TimeProvider.System);

        var added = await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken);
        var tested = await registry.TestAsync(Definition("orders"), TestContext.Current.CancellationToken);
        var found = await registry.GetAsync("orders", TestContext.Current.CancellationToken);
        var removed = await registry.RemoveAsync(["orders"], TestContext.Current.CancellationToken);

        Assert.False(registry.IsSupported);
        Assert.Equal(PersistenceErrors.NotSupported, added.Error!.Code);
        Assert.Equal(PersistenceErrors.NotSupported, tested.Error!.Code);
        Assert.Equal(PersistenceErrors.NotSupported, found.Error!.Code);
        Assert.Equal([false], removed);
        Assert.Empty(registry.Check());
    }

    [Fact]
    public async Task Dispose_DisposesEverySource()
    {
        var registry = Create(new FakeProvider());
        var orders = (FakeSource)(await registry.GetOrAddAsync(Definition("orders"), TestContext.Current.CancellationToken)).ValueOrThrow();

        await registry.DisposeAsync();

        Assert.True(orders.Disposed);
        Assert.Empty(registry.Names);
    }

    [Fact]
    public async Task ServiceRegistration_RegistersOneRegistry_WithOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new($"{DataSourceOptions.ConfigurationSection}:MaxCount", "3")]).Build();
        var services = new ServiceCollection().AddSingleton(TimeProvider.System);
        services.AddDataSourceRegistry();
        services.AddDataSourceProvider<FakeProvider>(configuration);
        services.AddDataSourceProvider<FakeProvider>(configuration);
        await using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<DataSourceRegistry>();

        Assert.True(registry.IsSupported);
        Assert.Single(provider.GetServices<IDataSourceProvider>());
        Assert.Equal(3, provider.GetRequiredService<DataSourceOptions>().MaxCount);
        Assert.Contains(provider.GetServices<IHealthContributor>(), c => ReferenceEquals(c, registry));
        Assert.Single(services, static d => d.ServiceType == typeof(DataSourceRegistry));
    }

    /// <summary>
    /// 以替身 Provider 建注册表
    /// </summary>
    private static DataSourceRegistry Create(FakeProvider provider, DataSourceOptions? options = null)
        => new([provider], options ?? new DataSourceOptions(), TimeProvider.System);

    /// <summary>
    /// 替身数据库类型的定义，连接串带口令
    /// </summary>
    private static DataSourceDefinition Definition(string name, string host = "Host=db") => new(name, "fake", $"{host};Password={Secret}");

    /// <summary>
    /// 错误的消息与参数里都没有口令
    /// </summary>
    private static void AssertNoSecret(Error error)
    {
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        Assert.All(error.Details!.Values, static v => Assert.DoesNotContain(Secret, v?.ToString() ?? string.Empty, StringComparison.Ordinal));
    }

    /// <summary>
    /// 替身数据源：记录是否被释放
    /// </summary>
    private sealed class FakeSource(DataSourceDefinition definition) : IDataSource, IDisposable
    {
        /// <inheritdoc />
        public string Name { get; } = definition.Name;

        /// <inheritdoc />
        public string DatabaseType { get; } = definition.DatabaseType;

        /// <summary>
        /// 定义（脱敏用）
        /// </summary>
        public DataSourceDefinition Definition { get; } = definition;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// 替身 Provider：只认数据库类型 <c>fake</c>，按开关拒绝连接串、探测失败或抛出
    /// </summary>
    private sealed class FakeProvider : IDataSourceProvider
    {
        /// <summary>
        /// 建过的数据源
        /// </summary>
        public List<FakeSource> Sources { get; } = [];

        /// <summary>
        /// 建过几次
        /// </summary>
        public int Created => Sources.Count;

        /// <summary>
        /// 非空时建实例以这个消息抛出 <see cref="ArgumentException"/>
        /// </summary>
        public string? RejectWith { get; init; }

        /// <summary>
        /// 非空时探测以这个原因失败
        /// </summary>
        public string? FailWith { get; set; }

        /// <summary>
        /// 非空时探测以这个消息抛出异常
        /// </summary>
        public string? ThrowOnProbe { get; init; }

        /// <summary>
        /// 非空时探测等它完成
        /// </summary>
        public TaskCompletionSource? ProbeGate { get; set; }

        /// <inheritdoc />
        public string Name => "fake-provider";

        /// <inheritdoc />
        public bool Supports(string databaseType) => string.Equals(databaseType, "fake", StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public ValueTask<IDataSource> CreateAsync(DataSourceDefinition definition, CancellationToken cancellationToken)
        {
            if (RejectWith is not null)
            {
                throw new ArgumentException(RejectWith);
            }

            var source = new FakeSource(definition);
            Sources.Add(source);
            return ValueTask.FromResult<IDataSource>(source);
        }

        /// <inheritdoc />
        public async ValueTask<Result> ProbeAsync(IDataSource source, CancellationToken cancellationToken)
        {
            if (ProbeGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            var fake = (FakeSource)source;
            if (ThrowOnProbe is not null)
            {
                throw new IOException(ThrowOnProbe);
            }

            return FailWith is null
                ? Result.Success()
                : Error.Unavailable(PersistenceErrors.DataSourceUnavailable, PersistenceMessages.DataSourceUnavailable, MessageArguments.Of(("name", fake.Name), ("reason", fake.Definition.Redact(FailWith))));
        }
    }
}
