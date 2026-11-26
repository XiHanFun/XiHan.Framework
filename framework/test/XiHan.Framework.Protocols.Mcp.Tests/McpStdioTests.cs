// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Parsing;
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
/// stdio 传输
/// </summary>
/// <remarks>
/// 每行一条消息、通知不答复、输入结束即返回；<c>mcp</c> 命令注册进命令行宿主
/// </remarks>
public sealed class McpStdioTests
{
    /// <summary>
    /// 在通用宿主里启动
    /// </summary>
    /// <remarks>
    /// Minimal 组合 + 内存安全（alice 可写订单）+ MCP + 订单特性 + 命令行宿主
    /// </remarks>
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
        builder.Services.AddSingleton<CallLog>();
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    /// <summary>
    /// 把字节交给传输运行到输入结束
    /// </summary>
    /// <remarks>
    /// 返回 stdout 的各行
    /// </remarks>
    private static async Task<List<string>> RunTransportAsync(IHost host, byte[] input)
    {
        var output = new StringWriter();
        using var stream = new MemoryStream(input);
        await McpStdioTransport.RunAsync(host.Services.GetRequiredService<McpServer>(), host.Services, stream, output, CancellationToken.None);
        return output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => l.TrimEnd('\r')).ToList();
    }

    [Fact]
    public async Task Transport_SkipsTheUtf8ByteOrderMark_AndNeverSwitchesEncodingOnAnotherMark()
    {
        using var host = await StartAsync();
        try
        {
            // 开头的 UTF-8 BOM 不算内容；UTF-16 的 BOM 不让传输改用 UTF-16，0xFF 0xFE 不是合法的 UTF-8，这一行答复解析错误
            var marked = await RunTransportAsync(host, [0xEF, 0xBB, 0xBF, .. """{"jsonrpc":"2.0","id":1,"method":"ping"}"""u8]);
            var foreign = await RunTransportAsync(host, [0xFF, 0xFE, .. """{"jsonrpc":"2.0","id":1,"method":"ping"}"""u8]);

            using var pong = JsonDocument.Parse(Assert.Single(marked));
            Assert.Equal(1, pong.RootElement.GetProperty("id").GetInt32());
            using var malformed = JsonDocument.Parse(Assert.Single(foreign));
            Assert.Equal(-32700, malformed.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            await Assert.ThrowsAsync<ArgumentNullException>(() => McpStdioTransport.RunAsync(host.Services.GetRequiredService<McpServer>(), host.Services, null!, new StringWriter(), CancellationToken.None));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Transport_AnswersOneLinePerRequest_AndSkipsNotifications()
    {
        using var host = await StartAsync();
        try
        {
            var lines = await RunTransportAsync(host, Encoding.UTF8.GetBytes(string.Join('\n',
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""",
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
                string.Empty,
                """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
                """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"orders_get","arguments":{"id":"7","page":1}}}""",
                """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"x"}}}""",
                "not json")));

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

    [Fact]
    public async Task Transport_AnswersALineWithAnEncodedSurrogate_WithAParseError()
    {
        // 一行按字节读进来再严格解码（protocol/mcp.md，ADR-0320）：编码成 UTF-8 的代理项与不是 JSON 同样答复解析错误，下一行照常处理；
        // ADR-0312 时传输收文本读取器，只拦得住读取器交来的孤立代理项，mcp 命令的读取器早已把这样的字节换成 U+FFFD，tools/call 照常执行
        using var host = await StartAsync();
        try
        {
            var lines = await RunTransportAsync(host, [.. "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"orders_get\",\"arguments\":{\"id\":\"7"u8, 0xED, 0xA0, 0x80, .. "\",\"page\":1}}}\n"u8, .. """{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""u8]);

            Assert.Equal(2, lines.Count);
            using var malformed = JsonDocument.Parse(lines[0]);
            Assert.Equal(-32700, malformed.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            Assert.Equal(JsonValueKind.Null, malformed.RootElement.GetProperty("id").ValueKind);
            using var listed = JsonDocument.Parse(lines[1]);
            Assert.Equal(2, listed.RootElement.GetProperty("id").GetInt32());
            Assert.Empty(host.Services.GetRequiredService<OriginLog>().Origins);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 以给定的命令行参数执行 <c>mcp</c> 命令，输入是逐行拼起来的消息按 UTF-8 编码的字节
    /// </summary>
    /// <remarks>
    /// 与 MCP 客户端写进 stdin 的一样是字节，解码由命令自己完成。返回退出码、stdout 的各行与 stderr
    /// </remarks>
    private static Task<(int ExitCode, List<string> Lines, string Error)> RunCommandAsync(IHost host, string[] args, params string[] messages)
        => RunCommandAsync(host, args, Encoding.UTF8.GetBytes(string.Join('\n', messages)));

    /// <summary>
    /// 以给定的命令行参数执行 <c>mcp</c> 命令，输入是原样的字节
    /// </summary>
    /// <remarks>
    /// 字节可以不是合法的 UTF-8，用来核对命令自己的解码。返回退出码、stdout 的各行与 stderr
    /// </remarks>
    private static async Task<(int ExitCode, List<string> Lines, string Error)> RunCommandAsync(IHost host, string[] args, byte[] input)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        await using var scope = host.Services.CreateAsyncScope();
        var command = new McpStdioCommand(() => new MemoryStream(input));
        var exitCode = await command.ExecuteAsync(new CommandContext(CommandLineParser.Parse(["mcp", .. args]), scope.ServiceProvider, output, error, CancellationToken.None));
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => l.TrimEnd('\r')).ToList();
        return (exitCode, lines, error.ToString());
    }

    [Fact]
    public async Task Command_AnswersALineWithInvalidUtf8_WithAParseError_AndReadsTheNextLine()
    {
        // stdin 按字节分行再严格解码（protocol/mcp.md「生命周期与传输」，ADR-0320）：编码成 UTF-8 的代理项、单独的 0xFF、截断的序列与过长编码都让这一行不是 JSON，
        // 答复解析错误、继续下一行；修之前非法的字节被换成 U+FFFD，tools/call 照常执行
        using var host = await StartAsync();
        try
        {
            byte[][] invalid = [[0xED, 0xA0, 0x80], [0xFF], [0xC3], [0xC0, 0xAF]];
            foreach (var bytes in invalid)
            {
                byte[] input = [.. "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"orders_get\",\"arguments\":{\"id\":\"7"u8, .. bytes, .. "\",\"page\":1}}}\r\n"u8, .. """{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""u8];

                var (exitCode, lines, error) = await RunCommandAsync(host, [], input);

                Assert.Equal(CommandLineExitCodes.Success, exitCode);
                Assert.Empty(error);
                Assert.Equal(2, lines.Count);
                using var malformed = JsonDocument.Parse(lines[0]);
                Assert.Equal(-32700, malformed.RootElement.GetProperty("error").GetProperty("code").GetInt32());
                Assert.Equal(McpErrors.InvalidMessage, malformed.RootElement.GetProperty("error").GetProperty("data").GetProperty("code").GetString());
                Assert.Equal(JsonValueKind.Null, malformed.RootElement.GetProperty("id").ValueKind);
                using var listed = JsonDocument.Parse(lines[1]);
                Assert.Equal(2, listed.RootElement.GetProperty("id").GetInt32());
            }

            Assert.Empty(host.Services.GetRequiredService<OriginLog>().Origins);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Command_SplitsLinesAtLfCrAndCrLf_AndSkipsBlankLines()
    {
        // 行尾与以前的读取器相同：LF、CR 与 CR LF 都结束一行；只有空白的行（含 U+00A0 这样的 Unicode 空白）跳过，不答复
        using var host = await StartAsync();
        try
        {
            byte[] input = [.. """{"jsonrpc":"2.0","id":1,"method":"ping"}"""u8, (byte)'\r', .. """{"jsonrpc":"2.0","id":2,"method":"ping"}"""u8, (byte)'\r', (byte)'\n', 0xC2, 0xA0, (byte)'\t', (byte)'\n', (byte)'\n', .. """{"jsonrpc":"2.0","id":3,"method":"ping"}"""u8];

            var (exitCode, lines, _) = await RunCommandAsync(host, [], input);

            Assert.Equal(CommandLineExitCodes.Success, exitCode);
            Assert.Equal([1, 2, 3], lines.Select(static line =>
            {
                using var reply = JsonDocument.Parse(line);
                return reply.RootElement.GetProperty("id").GetInt32();
            }));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Command_CarriesCredentialTenantLocaleAndTimeout_IntoEveryToolCall()
    {
        using var host = await StartAsync();
        try
        {
            var (exitCode, lines, error) = await RunCommandAsync(
                host,
                ["--authorization", "ApiKey dev-alice-key", "--tenant", "acme", "--locale", "zh-cn", "--correlation", "c-1", "--timeout", "30"],
                """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""",
                """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"x","page":1}}}""",
                """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"orders_get","arguments":{"id":"7","page":1}}}""");

            Assert.Equal(CommandLineExitCodes.Success, exitCode);
            Assert.Empty(error);
            Assert.Equal(3, lines.Count);

            // tools/list 的标题按 --locale 翻译
            using var listed = JsonDocument.Parse(lines[0]);
            var get = listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Single(static t => t.GetProperty("name").GetString() == "orders_get");
            Assert.Equal("读取订单", get.GetProperty("title").GetString());

            // 需要 orders.write 的 tool：凭据来自 --authorization，在这次调用的 Authenticate 阶段认证后放行
            using var created = JsonDocument.Parse(lines[1]);
            Assert.False(created.RootElement.GetProperty("result").TryGetProperty("isError", out _), lines[1]);
            Assert.Equal("orders.create", created.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("operation").GetString());

            // 每个 tools/call 都带会话的主体、租户、区域、关联与时限
            var calls = host.Services.GetRequiredService<CallLog>().Calls;
            Assert.Equal(2, calls.Count);
            Assert.All(calls, static call => Assert.Equal(("alice", "acme", "zh-CN", "c-1", true), call));
            Assert.Equal(
                [new InvocationOrigin { Protocol = InvocationProtocols.Mcp, Target = "orders_create" }, new InvocationOrigin { Protocol = InvocationProtocols.Mcp, Target = "orders_get" }],
                host.Services.GetRequiredService<OriginLog>().Origins);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Command_DecodesStdinAsUtf8_AndAnswersInAscii()
    {
        using var host = await StartAsync();
        try
        {
            // MCP 客户端按 UTF-8 写 stdin（开头可能带 BOM）：不能按控制台输入代码页（Windows 上的 936、437 等）解码，否则中文参数成乱码
            var (exitCode, lines, error) = await RunCommandAsync(
                host,
                [],
                "﻿" + """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"orders_get","arguments":{"id":"订单-7","name":"张三","page":1}}}""");

            Assert.Equal(CommandLineExitCodes.Success, exitCode);
            Assert.Empty(error);
            var line = Assert.Single(lines);
            using var called = JsonDocument.Parse(line);
            var reply = default(JsonElement);
            Assert.True(called.RootElement.TryGetProperty("result", out var result) && result.TryGetProperty("structuredContent", out reply), line);
            Assert.Equal(("订单-7", "张三"), (reply.GetProperty("id").GetString(), reply.GetProperty("name").GetString()));

            // 答复只含 ASCII（非 ASCII 字符按 JSON 转义）：按任何兼容 ASCII 的控制台输出代码页写出都是同样的字节，即合法的 UTF-8
            Assert.True(line.All(char.IsAscii), line);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Command_InvalidCredential_FailsEachProtectedCall_AndInvalidOptionsNeverStartTheSession()
    {
        using var host = await StartAsync();
        try
        {
            // 凭据格式对但无效：stdio 没有外层认证，需要认证的 tools/call 是 isError，会话继续，不需要认证的照常执行
            var (exitCode, lines, _) = await RunCommandAsync(
                host,
                ["--authorization", "ApiKey wrong-key", "--locale", "zh-CN"],
                """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"orders_create","arguments":{"name":"x"}}}""",
                """{"jsonrpc":"2.0","id":2,"method":"ping"}""");
            Assert.Equal(CommandLineExitCodes.Success, exitCode);
            Assert.Equal(2, lines.Count);
            using var denied = JsonDocument.Parse(lines[0]);
            Assert.True(denied.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.StartsWith("SECURITY_DENIED_003: ", denied.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
            Assert.Empty(host.Services.GetRequiredService<CallLog>().Calls);

            // 选项不合法：不进入会话、不读输入，stderr 一行用法错误，退出码 64
            foreach (var args in new[] { new[] { "--authorization", "garbage" }, ["--timeout", "0"] })
            {
                var (code, output, error) = await RunCommandAsync(host, args, """{"jsonrpc":"2.0","id":1,"method":"ping"}""");
                Assert.Equal(CommandLineExitCodes.Usage, code);
                Assert.Empty(output);
                using var failure = JsonDocument.Parse(error);
                Assert.Equal(CommandLineErrors.UsageInvalid, failure.RootElement.GetProperty("code").GetString());
            }
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Command_InvalidOptions_AreReportedInTheLocaleOfTheLocaleOption()
    {
        // 会话选项不合法时没有发起调用：stderr 的用法错误同样按 --locale 翻译（protocol/commandline.md「操作映射」，ADR-0325）；修之前总按默认区域
        using var host = await StartAsync();
        try
        {
            var localizer = host.Services.GetRequiredService<IMessageLocalizer>();

            var (code, output, error) = await RunCommandAsync(host, ["--timeout", "0", "--locale", "zh-CN"], """{"jsonrpc":"2.0","id":1,"method":"ping"}""");

            Assert.Equal(CommandLineExitCodes.Usage, code);
            Assert.Empty(output);
            using var failure = JsonDocument.Parse(error);
            Assert.Equal(localizer.Localize(CommandLineMessages.InvokeTimeoutInvalid, "zh-CN", null), failure.RootElement.GetProperty("detail").GetString());
            Assert.NotEqual(localizer.Localize(CommandLineMessages.InvokeTimeoutInvalid, "en-US", null), failure.RootElement.GetProperty("detail").GetString());
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
