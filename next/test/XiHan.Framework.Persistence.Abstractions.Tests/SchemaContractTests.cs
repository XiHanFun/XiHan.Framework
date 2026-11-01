// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 库结构读取与实体目录的中立契约。
/// </summary>
/// <remarks>
/// 列表查询的取出条数、登记实体的去重、实体映射推出的协议与索引、不支持时的明确错误码。
/// </remarks>
public sealed class SchemaContractTests
{
    [Fact]
    public void TableListQuery_TakeAboveTheLimit_IsTakeExceeded_AndNonPositiveIsAProgrammingError()
    {
        var exceeded = Assert.ThrowsAny<FrameworkException>(static () => new TableListQuery { Take = 1001 });

        Assert.Equal(PersistenceErrors.TakeExceeded, exceeded.Code);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new TableListQuery { Take = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new TableListQuery { Skip = -1 });
        Assert.Equal(1000, new TableListQuery().Take);
    }

    [Fact]
    public void Registrations_RecordEachEntityOnce_InFirstRegistrationOrder()
    {
        var services = new ServiceCollection();
        services.AddRepository<AuditedWidget, Guid>().AddRepository<Widget, Guid>().AddRepository<AuditedWidget, Guid>();

        var entities = services.GetRepositoryRegistrations().Entities;

        Assert.Equal([typeof(AuditedWidget), typeof(Widget)], entities.Select(static e => e.EntityType));
        Assert.All(entities, static e => Assert.Equal(typeof(Guid), e.KeyType));
    }

    [Fact]
    public void EntityMapping_InfersProtocolsAndKeys_FromTheEntityType()
    {
        var audited = new EntityMapping(new RegisteredEntity(typeof(AuditedWidget), typeof(Guid)), "audited_widget", isSplit: false, [new EntityColumnMapping("Id", "id", typeof(Guid), IsKey: true, IsNullable: false), new EntityColumnMapping("TenantId", "tenant_id", typeof(string), IsKey: false, IsNullable: false)]);
        var plain = new EntityMapping(new RegisteredEntity(typeof(Widget), typeof(Guid)), "widget", isSplit: false, []);

        Assert.Equal(EntityProtocols.CreationTime | EntityProtocols.ModificationTime | EntityProtocols.ConcurrencyStamp | EntityProtocols.SoftDeletable | EntityProtocols.MultiTenant, audited.Protocols);
        Assert.Equal("id", Assert.Single(audited.KeyColumns).Column);
        Assert.Equal(EntityProtocols.None, plain.Protocols);
        Assert.Empty(plain.Indexes);
    }

    [Fact]
    public async Task UnsupportedReader_ReturnsNotSupported_ForListAndRead()
    {
        await using var provider = new ServiceCollection().AddUnsupportedSchemaReader().BuildServiceProvider();
        var reader = provider.GetRequiredService<ISchemaReader>();

        var listed = await reader.ListTablesAsync(null, new TableListQuery(), TestContext.Current.CancellationToken);
        var read = await reader.GetTablesAsync(null, [], TestContext.Current.CancellationToken);
        var single = await reader.GetTableAsync(null, "orders", TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.NotSupported, listed.Error?.Code);
        Assert.Equal(PersistenceMessages.SchemaNotSupported, listed.Error!.MessageKey);
        Assert.Equal(PersistenceErrors.NotSupported, read.Error?.Code);
        Assert.Equal(PersistenceErrors.NotSupported, single.Error?.Code);
    }
}
