// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Persistence.Conformance;

/// <summary>
/// 库结构读取与实体目录的一致性套件。
/// </summary>
/// <remarks>
/// 见 RFC-0005「库结构读取与实体目录」。每个 <c>persistence.relational@1</c> Provider 都继承它；宿主登记套件的三种实体（<see cref="ConformanceOrder"/> 登记两次），读结构的 Provider 要在启动时建出它们的表（<c>InitTables</c> / <c>EnsureCreated</c>）。不读结构的 Provider 把 <see cref="ReadsSchema"/> 设为 <see langword="false"/>，用例随之断言读取返回 <see cref="PersistenceErrors.NotSupported"/>，不返回空结果冒充。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class SchemaConformance
{
    /// <summary>
    /// 宿主配置。
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// Provider 是否读库结构。
    /// </summary>
    protected abstract bool ReadsSchema { get; }

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
    /// 指向应用自己库的运行时数据源定义。
    /// </summary>
    /// <remarks>
    /// 用例经它读同一个库，结果应与缺省连接一致；不支持运行时数据源时返回 <see langword="null"/>。
    /// </remarks>
    /// <param name="name">数据源名。</param>
    protected abstract DataSourceDefinition? CreateDefinition(string name);

    /// <summary>
    /// 用例 1：实体目录列出每个登记的实体。
    /// </summary>
    /// <remarks>
    /// 同一实体登记两次只一条；标识类型、协议、主键列与普通列都对；不映射的属性（领域事件）不列。
    /// </remarks>
    [Fact]
    public async Task EntityCatalog_ListsEveryRegisteredEntity_Once()
    {
        await using var host = await StartAsync().ConfigureAwait(false);

        var mappings = await host.Services.GetRequiredService<IEntityCatalog>().ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal([typeof(ConformanceOrder), typeof(ConformanceNote), typeof(ConformanceItem)], mappings.Select(static m => m.EntityType));
        var order = mappings[0];
        Assert.Equal(typeof(Guid), order.KeyType);
        Assert.Equal(EntityProtocols.AggregateRoot | EntityProtocols.CreationTime | EntityProtocols.ModificationTime | EntityProtocols.ConcurrencyStamp | EntityProtocols.SoftDeletable, order.Protocols);
        Assert.Equal(EntityProtocols.MultiTenant, mappings[1].Protocols);
        Assert.Equal(typeof(int), mappings[2].KeyType);
        Assert.Equal(EntityProtocols.None, mappings[2].Protocols);
        Assert.All(mappings, static m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Table));
            Assert.False(m.IsSplit);
            var key = Assert.Single(m.KeyColumns);
            Assert.Equal(nameof(ConformanceOrder.Id), key.Property);
            Assert.False(key.IsNullable);
        });
        var name = Assert.Single(order.Columns, static c => c.Property == nameof(ConformanceOrder.Name));
        Assert.Equal(typeof(string), name.Type);
        Assert.False(name.IsKey);
        Assert.True(Assert.Single(order.Columns, static c => c.Property == nameof(ConformanceOrder.ModifiedAt)).IsNullable);
        Assert.DoesNotContain(order.Columns, static c => c.Property == nameof(ConformanceOrder.DomainEvents));
    }

    /// <summary>
    /// 用例 2：列表含每张实体表，关键字与分页照常。
    /// </summary>
    /// <remarks>
    /// 不读结构的 Provider 返回 <see cref="PersistenceErrors.NotSupported"/>。
    /// </remarks>
    [Fact]
    public async Task ListTables_ContainsEveryEntityTable_OrIsNotSupported()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var reader = host.Services.GetRequiredService<ISchemaReader>();
        if (!ReadsSchema)
        {
            var unsupported = await reader.ListTablesAsync(null, new TableListQuery(), TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Error?.Code);
            return;
        }

        var mappings = await host.Services.GetRequiredService<IEntityCatalog>().ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var page = (await reader.ListTablesAsync(null, new TableListQuery(), TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var orderTable = mappings[0].Table;
        var filtered = (await reader.ListTablesAsync(null, new TableListQuery { Keyword = orderTable.ToUpperInvariant() }, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var second = (await reader.ListTablesAsync(null, new TableListQuery { Skip = 1, Take = 1 }, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();

        Assert.All(mappings, m => Assert.Contains(page.Items, t => string.Equals(t.Name, m.Table, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(page.Items.Count, page.Total);
        Assert.Equal([.. page.Items.Select(static t => t.Name).Order(StringComparer.OrdinalIgnoreCase)], page.Items.Select(static t => t.Name), StringComparer.OrdinalIgnoreCase);
        Assert.Contains(filtered.Items, t => string.Equals(t.Name, orderTable, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(filtered.Items.Count, filtered.Total);
        Assert.Equal(page.Total, second.Total);
        Assert.Equal(page.Items[1].Name, Assert.Single(second.Items).Name);
        Assert.All(page.Items, static t => Assert.Equal([t.Name], t.PhysicalTables));
    }

    /// <summary>
    /// 用例 5：表列表的分页越界以校验结果返回，边界值照常。
    /// </summary>
    /// <remarks>
    /// <c>Skip</c> = -1、<c>Take</c> = 0 → <see cref="PersistenceErrors.ListPageInvalid"/>，<c>Take</c> = 1001 → <see cref="PersistenceErrors.TakeExceeded"/>，都在构造查询时不抛出、读取器不连库；经运行时数据源时也不登记它。<c>Skip</c> = 0、<c>Take</c> = 1 与 1000 照常。不读结构的 Provider 仍返回 <see cref="PersistenceErrors.NotSupported"/>。
    /// </remarks>
    [Fact]
    public async Task ListTables_PageOutOfRange_IsAValidationResult()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var reader = host.Services.GetRequiredService<ISchemaReader>();
        if (!ReadsSchema)
        {
            var unsupported = await reader.ListTablesAsync(null, new TableListQuery { Skip = -1 }, TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Error?.Code);
            return;
        }

        foreach (var (query, code) in new[]
        {
            (new TableListQuery { Skip = -1 }, PersistenceErrors.ListPageInvalid),
            (new TableListQuery { Take = 0 }, PersistenceErrors.ListPageInvalid),
            (new TableListQuery { Take = 1001 }, PersistenceErrors.TakeExceeded),
        })
        {
            var rejected = await reader.ListTablesAsync(null, query, TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(code, rejected.Error?.Code);
            Assert.Equal("VALIDATION", rejected.Error!.Kind);
        }

        // 经运行时数据源：先核对分页，不登记数据源
        if (CreateDefinition("conformance-page") is { } definition)
        {
            var named = await reader.ListTablesAsync(definition, new TableListQuery { Skip = -1 }, TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.ListPageInvalid, named.Error?.Code);
            Assert.DoesNotContain(definition.Name, host.Services.GetRequiredService<DataSourceRegistry>().Names);
        }

        var all = (await reader.ListTablesAsync(null, new TableListQuery { Skip = 0, Take = 1000 }, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var first = (await reader.ListTablesAsync(null, new TableListQuery { Skip = 0, Take = 1 }, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(all.Total, first.Total);
        Assert.Equal(all.Items[0].Name, Assert.Single(first.Items).Name);
    }

    /// <summary>
    /// 用例 3：按表名批量读结构。
    /// </summary>
    /// <remarks>
    /// 主键列不可空；不存在的表该项 <see cref="PersistenceErrors.TableNotFound"/>；超过上限整批 <see cref="PersistenceErrors.TakeExceeded"/>；空集合返回空列表。不读结构的 Provider 返回 <see cref="PersistenceErrors.NotSupported"/>。
    /// </remarks>
    [Fact]
    public async Task GetTables_ReadsColumns_ReportsMissingPerItem_AndCapsTheBatch()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var reader = host.Services.GetRequiredService<ISchemaReader>();
        if (!ReadsSchema)
        {
            var unsupported = await reader.GetTablesAsync(null, ["anything"], TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Error?.Code);
            return;
        }

        var order = (await host.Services.GetRequiredService<IEntityCatalog>().ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))[0];
        var batch = (await reader.GetTablesAsync(null, [order.Table, "conformance_no_such_table", order.Table], TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var tooMany = await reader.GetTablesAsync(null, [.. Enumerable.Repeat(order.Table, SchemaLimits.MaxTablesPerRead + 1)], TestContext.Current.CancellationToken).ConfigureAwait(false);
        var empty = (await reader.GetTablesAsync(null, [], TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var single = await reader.GetTableAsync(null, "conformance_no_such_table", TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal(3, batch.Count);
        var schema = batch[0].ValueOrThrow();
        Assert.Equal(PersistenceErrors.TableNotFound, batch[1].Error?.Code);
        Assert.Same(schema, batch[2].ValueOrThrow());
        var keyColumn = Assert.Single(order.KeyColumns).Column;
        var key = Assert.Single(schema.Columns, c => string.Equals(c.Name, keyColumn, StringComparison.OrdinalIgnoreCase));
        Assert.True(key.IsPrimaryKey);
        Assert.False(key.IsNullable);
        Assert.Contains(schema.Columns, static c => string.Equals(c.Name, nameof(ConformanceOrder.Name), StringComparison.OrdinalIgnoreCase) && !c.IsPrimaryKey);
        Assert.Equal(Enumerable.Range(0, schema.Columns.Count), schema.Columns.Select(static c => c.Ordinal));
        Assert.Equal(PersistenceErrors.TakeExceeded, tooMany.Error?.Code);
        Assert.Empty(empty);
        Assert.Equal(PersistenceErrors.TableNotFound, single.Error?.Code);
    }

    /// <summary>
    /// 用例 4：经运行时数据源读同一个库，结果与缺省连接一致。
    /// </summary>
    /// <remarks>
    /// 不读结构或不支持运行时数据源的 Provider 跳过比对，只断言明确的错误。
    /// </remarks>
    [Fact]
    public async Task NamedDataSource_ReadsTheSameSchema()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var reader = host.Services.GetRequiredService<ISchemaReader>();
        var definition = CreateDefinition("conformance-schema");
        if (!ReadsSchema || definition is null)
        {
            var probe = definition ?? new DataSourceDefinition("conformance-schema", DatabaseTypes.Sqlite, "Data Source=conformance.db");
            var unsupported = await reader.ListTablesAsync(probe, new TableListQuery(), TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Error?.Code);
            return;
        }

        var order = (await host.Services.GetRequiredService<IEntityCatalog>().ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))[0];
        var local = (await reader.GetTableAsync(null, order.Table, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
        var named = (await reader.GetTableAsync(definition, order.Table, TestContext.Current.CancellationToken).ConfigureAwait(false)).ValueOrThrow();

        Assert.Equal(local.Table.Name, named.Table.Name);
        Assert.Equal(local.Table.Comment, named.Table.Comment);
        Assert.Equal(local.Table.PhysicalTables, named.Table.PhysicalTables);
        Assert.Equal(local.Columns, named.Columns);
        Assert.Contains(definition.Name, host.Services.GetRequiredService<DataSourceRegistry>().Names);
    }

    /// <summary>
    /// 装配并启动宿主：被测特性、三种套件实体（订单登记两次）与附加服务。
    /// </summary>
    protected Task<TestHost> StartAsync() => TestHost.StartAsync(
        builder => builder.UseFeature(CreateFeature()),
        Configuration,
        services =>
        {
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
            services.AddRepository<ConformanceItem, int>();
            services.AddRepository<ConformanceOrder, Guid>();
            ConfigureServices(services);
        });
}
