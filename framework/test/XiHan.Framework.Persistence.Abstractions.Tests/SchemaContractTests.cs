// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 库结构读取与实体目录的中立契约
/// </summary>
/// <remarks>
/// 列表查询的取出条数、登记实体的去重、实体映射推出的协议与索引、不支持时的明确错误码
/// </remarks>
public sealed class SchemaContractTests
{
    [Theory]
    [InlineData(-1, 1, PersistenceErrors.ListPageInvalid)]
    [InlineData(0, 0, PersistenceErrors.ListPageInvalid)]
    [InlineData(-5, 1001, PersistenceErrors.ListPageInvalid)]
    [InlineData(0, 1001, PersistenceErrors.TakeExceeded)]
    [InlineData(0, 1, null)]
    [InlineData(0, 1000, null)]
    [InlineData(7, 20, null)]
    public void TableListQuery_PageOutOfRange_IsAValidationResult_NotAConstructionError(int skip, int take, string? code)
    {
        // 构造不抛出：查询常由请求绑定，越界由读结构的 Provider 以 400 的校验结果返回
        var query = new TableListQuery { Skip = skip, Take = take };

        var validated = query.Validate();

        Assert.Equal(code, validated.Error?.Code);
        if (code == PersistenceErrors.ListPageInvalid)
        {
            Assert.Equal(PersistenceMessages.ListPageInvalid, validated.Error!.MessageKey);
            Assert.Equal((skip, take), ((int)validated.Error.Details!["skip"]!, (int)validated.Error.Details["take"]!));
        }
        else if (code == PersistenceErrors.TakeExceeded)
        {
            Assert.Equal((take, 1000), ((int)validated.Error!.Details!["take"]!, (int)validated.Error.Details["max"]!));
        }

        Assert.Equal(code is null ? null : "VALIDATION", validated.Error?.Kind);
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
