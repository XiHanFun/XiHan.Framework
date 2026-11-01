// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Persistence.Conformance;

/// <summary>
/// 运行时数据源的一致性套件（RFC-0005「运行时数据源」）。
/// </summary>
/// <remarks>
/// 每个 <c>persistence.relational@1</c> Provider 都继承它。支持运行时数据源的 Provider 给出一个连得上的定义与一个连不上的定义（带口令）；不支持的两个都返回 <see langword="null"/>，用例随之断言取用与测试连接都返回 <see cref="PersistenceErrors.NotSupported"/>，不返回空结果冒充。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class DataSourceConformance
{
    /// <summary>
    /// 宿主配置。
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// 创建被测 Provider 的特性。
    /// </summary>
    protected abstract IFeature CreateFeature();

    /// <summary>
    /// 附加服务注册。
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// 一个连得上的数据源定义。
    /// </summary>
    /// <remarks>
    /// 在连接串末尾加一个分号后仍须连得上（用例以此构造「定义变了」）。Provider 不支持运行时数据源时返回 <see langword="null"/>。
    /// </remarks>
    /// <param name="name">数据源名。</param>
    protected abstract DataSourceDefinition? CreateDefinition(string name);

    /// <summary>
    /// 一个连不上的数据源定义，连接串里带口令类的键。
    /// </summary>
    /// <remarks>
    /// 用例断言测试连接的错误里没有连接串与口令。Provider 不支持运行时数据源时返回 <see langword="null"/>。
    /// </remarks>
    /// <param name="name">数据源名。</param>
    protected abstract DataSourceDefinition? CreateUnreachableDefinition(string name);

    /// <summary>
    /// 用例 1：测试连接成功且不登记。
    /// </summary>
    /// <remarks>
    /// 不支持的 Provider 返回 <see cref="PersistenceErrors.NotSupported"/>。
    /// </remarks>
    [Fact]
    public async Task Test_Connects_WithoutRegistering_OrIsNotSupported()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var definition = CreateDefinition("conformance-test");
        if (definition is null)
        {
            var unsupported = await registry.TestAsync(new DataSourceDefinition("conformance-test", DatabaseTypes.Sqlite, "Data Source=conformance.db"), TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.False(registry.IsSupported);
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Error?.Code);
            return;
        }

        var tested = await registry.TestAsync(definition, TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.True(tested.IsSuccess, tested.ToString());
        Assert.Empty(registry.Names);
    }

    /// <summary>
    /// 用例 2：同定义复用、定义变了替换、批量移除逐项报告。
    /// </summary>
    /// <remarks>
    /// 不支持的 Provider 取用返回 <see cref="PersistenceErrors.NotSupported"/>。
    /// </remarks>
    [Fact]
    public async Task GetOrAdd_ReusesSameDefinition_ReplacesChanged_AndRemoveIsBatch()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var definition = CreateDefinition("conformance-a");
        if (definition is null)
        {
            var unsupported = await registry.GetOrAddAsync(new DataSourceDefinition("conformance-a", DatabaseTypes.Sqlite, "Data Source=conformance.db"), TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Error?.Code);
            return;
        }

        var first = (await registry.GetOrAddAsync(definition, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var same = (await registry.GetOrAddAsync(new DataSourceDefinition(definition.Name, definition.DatabaseType, definition.ConnectionString), TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var changed = (await registry.GetOrAddAsync(new DataSourceDefinition(definition.Name, definition.DatabaseType, definition.ConnectionString + ";"), TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var removed = await registry.RemoveAsync([definition.Name, "conformance-missing", definition.Name], TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lookup = await registry.GetAsync(definition.Name, TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Same(first, same);
        Assert.NotSame(first, changed);
        Assert.Equal(definition.Name, changed.Name);
        Assert.Equal([true, false, false], removed);
        Assert.Equal(PersistenceErrors.DataSourceNotFound, lookup.Error?.Code);
    }

    /// <summary>
    /// 用例 3：连不上的库测试连接失败且错误里没有机密。
    /// </summary>
    /// <remarks>
    /// 错误码 <see cref="PersistenceErrors.DataSourceUnavailable"/>，消息与参数里没有连接串与口令。
    /// </remarks>
    [Fact]
    public async Task Test_Unreachable_IsUnavailable_WithoutSecrets()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var definition = CreateUnreachableDefinition("conformance-down");
        if (definition is null)
        {
            Assert.False(registry.IsSupported);
            return;
        }

        var tested = await registry.TestAsync(definition, TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal(PersistenceErrors.DataSourceUnavailable, tested.Error?.Code);
        Assert.Equal("conformance-down", tested.Error!.Details!["name"]);
        // 脱敏后不变，说明错误里已经没有连接串与口令类键的值
        Assert.Equal(tested.Error.Message, definition.Redact(tested.Error.Message));
        foreach (var value in tested.Error.Details.Values.OfType<string>())
        {
            Assert.Equal(value, definition.Redact(value));
        }
    }

    /// <summary>
    /// 用例 4：每个已登记的数据源一条健康项，探测后连得上的是 Ready。
    /// </summary>
    [Fact]
    public async Task Health_EachRegisteredSourceIsReported()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var definition = CreateDefinition("conformance-health");
        if (definition is null)
        {
            Assert.Empty(registry.Check());
            return;
        }

        (await registry.GetOrAddAsync(definition, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        await registry.ProbeAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var status = Assert.Single(host.Services.GetServices<IHealthContributor>().SelectMany(static c => c.Check()), static s => s.Name == DataSourceRegistry.HealthPrefix + "conformance-health");

        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Equal(definition.DatabaseType, status.Data["databaseType"]);
        Assert.DoesNotContain(definition.ConnectionString, status.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 装配并启动宿主：被测特性与附加服务。
    /// </summary>
    protected Task<TestHost> StartAsync() => TestHost.StartAsync(
        builder => builder.UseFeature(CreateFeature()),
        Configuration,
        ConfigureServices);
}
