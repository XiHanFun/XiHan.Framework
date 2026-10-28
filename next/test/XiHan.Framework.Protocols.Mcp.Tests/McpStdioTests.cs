// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp.Server;
using XiHan.Framework.Protocols.Mcp.Transports;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Protocols.Mcp.Tests;

/// <summary>
/// stdio 传输：每行一条消息、通知不答复、输入结束即返回；<c>mcp</c> 命令注册进命令行宿主。
/// </summary>
public sealed class McpStdioTests
{
    /// <summary>
    /// 在通用宿主里启动：Minimal 组合 + 内存安全（alice 可写订单）+ MCP + 订单特性 + 命令行宿主。
    /// </summary>
    private static async Task<IHost> StartAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
            new("XiHan:Security:InMemory:Principals:alice:Roles", "editor"),
            new("XiHan:Security:InMemory:Grants:role:editor", "orders.read,orders.write"),
        ]);
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<McpProtocolFeature>().UseFeature<OrdersFeature>());
        builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersFeature)));
        builder.Services.AddXiHanCommandLine();
        builder.Services.AddSingleton<OriginLog>();
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    [Fact]
    public async Task Transport_AnswersOneLinePerRequest_AndSkipsNotifications()
    {
        using var host = await StartAsync();
        try
        {
            var input = new StringReader(string.Join('\n',
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""",
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
                string.Empty,
                """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
                """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"orders_get","arguments":{"id":"7","page":1}}}""",
                """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"x"}}}""",
                "not json"));
            var output = new StringWriter();

            await McpStdioTransport.RunAsync(host.Services.GetRequiredService<McpServer>(), host.Services, input, output, CancellationToken.None);

            var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => l.TrimEnd('\r')).ToList();
            Assert.Equal(5, lines.Count);
            Assert.All(lines, static line => Assert.DoesNotContain('\n', line));
            using var initialized = JsonDocument.Parse(lines[0]);
            Assert.Equal(McpServer.ProtocolVersion, initialized.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
            using var listed = JsonDocument.Parse(lines[1]);
            Assert.Contains(listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray(), static t => t.GetProperty("name").GetString() == "orders_get");
            using var called = JsonDocument.Parse(lines[2]);
            Assert.Equal("7", called.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("id").GetString());
            // tools/call 经完整管道：匿名调用需要权限的操作是 isError 结果
            using var denied = JsonDocument.Parse(lines[3]);
            Assert.True(denied.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            using var malformed = JsonDocument.Parse(lines[4]);
            Assert.Equal(-32700, malformed.RootElement.GetProperty("error").GetProperty("code").GetInt32());

            // stdio 传输没有网络对端：来源只有协议与 tool 名（被拒的 orders_create 没有执行到操作）
            Assert.Equal([new InvocationOrigin { Protocol = InvocationProtocols.Mcp, Target = "orders_get" }], host.Services.GetRequiredService<OriginLog>().Origins);

            // mcp 命令已注册进命令行宿主
            Assert.Contains(host.Services.GetServices<ICommand>(), static c => c.Name == "mcp" && c.DescriptionKey == McpMessages.StdioDescription);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
