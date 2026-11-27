// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 配置节 <c>XiHan:Messaging:DataSchemas</c> 在消息 Provider 装配时登记进数据模式表
/// </summary>
/// <remarks>
/// RFC-0009「框架事件的数据模式」第 1 条（ADR-0332）：与代码登记的进同一张表，同一个 <c>type</c> 值相同只记一次，值不同不论谁先登记都启动失败，配置不悄悄顶掉代码登记的
/// </remarks>
public sealed class DataSchemaConfigurationTests
{
    /// <summary>
    /// 一项配置：<c>orders.placed</c> 的数据模式
    /// </summary>
    private static readonly KeyValuePair<string, string?>[] _configured =
    [
        new("XiHan:Messaging:DataSchemas:0:Type", "orders.placed"),
        new("XiHan:Messaging:DataSchemas:0:DataSchema", "https://example.com/schemas/order-placed.json"),
    ];

    [Fact]
    public async Task ConfiguredDataSchemas_AreRegistered_NextToTheOnesRegisteredInCode()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            _configured,
            static services => services.AddMessageDataSchema("orders.shipped", "urn:example:order-shipped").AddMessageDataSchema("orders.placed", "https://example.com/schemas/order-placed.json"));

        var schemas = host.Services.GetRequiredService<MessageDataSchemas>();
        Assert.Equal(2, schemas.Count);
        Assert.Equal("https://example.com/schemas/order-placed.json", schemas.Find("orders.placed").ValueOrThrow());
        Assert.Equal("urn:example:order-shipped", schemas.Find("orders.shipped").ValueOrThrow());
    }

    [Fact]
    public async Task WithoutConfigurationOrCode_ThereIsNoRegistry()
    {
        // 没有登记任何模式时容器里没有这张表，三类信封与以前相同（ADR-0323）
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());

        Assert.Null(host.Services.GetService<MessageDataSchemas>());
    }

    [Fact]
    public async Task AConfiguredValue_ThatDiffersFromTheCodeRegistration_FailsStartup()
    {
        // 代码先登记：Provider 装配时读到配置里的另一个值，启动失败
        var thrown = await Assert.ThrowsAsync<ArgumentException>(() => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            _configured,
            static services => services.AddMessageDataSchema("orders.placed", "urn:example:other")));

        Assert.Contains("'XiHan:Messaging:DataSchemas'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("'orders.placed'", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACodeRegistration_ThatDiffersFromTheConfiguredValue_Fails()
    {
        // 配置先登记（Provider 已装配）：代码再登记另一个值同样失败，不取后登记的
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, new ConfigurationBuilder().AddInMemoryCollection(_configured).Build());
        builder.UseFeature<InMemoryMessagingFeature>();
        builder.Build();

        Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema("orders.placed", "urn:example:other"));
        services.AddMessageDataSchema("orders.placed", "https://example.com/schemas/order-placed.json");
        Assert.Equal("https://example.com/schemas/order-placed.json", Assert.Single(services, static d => d.ServiceType == typeof(MessageDataSchemas)).ImplementationInstance is MessageDataSchemas schemas ? schemas.Find("orders.placed").ValueOrThrow() : null);
    }

    [Fact]
    public async Task AnInvalidEntry_FailsStartup_NamingTheConfigurationKey()
    {
        var thrown = await Assert.ThrowsAsync<ArgumentException>(() => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            [new("XiHan:Messaging:DataSchemas:0:Type", "orders.placed")]));

        Assert.Contains("'XiHan:Messaging:DataSchemas:0:DataSchema'", thrown.Message, StringComparison.Ordinal);
    }
}
