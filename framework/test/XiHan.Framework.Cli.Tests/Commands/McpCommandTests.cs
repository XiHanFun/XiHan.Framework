// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Cli.Migration;
using XiHan.Framework.Cli.Tests.Integration;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Tests.Commands;

/// <summary>
/// <c>xihan mcp</c> 端到端：经 <c>Protocols.Mcp</c> 的 stdio 传输提供三个只读 tool
/// </summary>
/// <remarks>
/// 与 <see cref="CliHostTests"/> 同4E00集合：执行命令会设置进程级的默认消息作用域
/// </remarks>
[Collection("Cli")]
public sealed class McpCommandTests
{
    /// <summary>
    /// 以给定的选项与输入行执行 <c>mcp</c>，返回退出码、stdout 各行与 stderr
    /// </summary>
    private static async Task<(int Exit, List<JsonDocument> Replies, string Error)> RunAsync(string[] options, params string[] messages)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await CliHost.RunAsync(["mcp", .. options], output, error, new RecordingLauncher(), MigrationRuleSets.All, new StringReader(string.Join('\n', messages)), CancellationToken.None);
        var replies = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => JsonDocument.Parse(l.TrimEnd('\r'))).ToList();
        return (exit, replies, error.ToString());
    }

    /// <summary>
    /// 4E00条 <c>tools/call</c> 请求
    /// </summary>
    private static string Call(int id, string tool, string arguments)
        => """{"jsonrpc":"2.0","id":""" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ""","method":"tools/call","params":{"name":""" + "\"" + tool + "\"" + ""","arguments":""" + arguments + "}}";

    /// <summary>
    /// 结果的 <c>structuredContent</c>
    /// </summary>
    private static JsonElement Structured(JsonDocument reply) => reply.RootElement.GetProperty("result").GetProperty("structuredContent");

    [Fact]
    public async Task Mcp_ServesThreeReadOnlyTools_ThroughTheProtocolAdapter()
    {
        using var project = new ProjectFixture(FrameworkInfo.Version);
        var (exit, replies, error) = await RunAsync(
            ["--locale", "zh-CN", "--path", project.Root],
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
            Call(3, "xihan_framework_info", """{"name":"Messaging.Kafka"}"""),
            Call(4, "xihan_framework_config", """{"query":"XiHan:Messaging:Kafka:ConsumerGroup"}"""),
            Call(5, "xihan_framework_search", """{"page":"0016"}"""),
            Call(6, "xihan_framework_info", """{"name":"no-such-thing"}"""),
            Call(7, "xihan_framework_search", """{"query":"kafka","limit":99}"""));

        Assert.Equal(CommandLineExitCodes.Success, exit);
        Assert.Equal(string.Empty, error);
        Assert.Equal(7, replies.Count);

        var initialized = replies[0].RootElement.GetProperty("result");
        Assert.Equal("xihan-framework", initialized.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal(FrameworkInfo.Version, initialized.GetProperty("serverInfo").GetProperty("version").GetString());
        Assert.Contains("xihan_framework_config", initialized.GetProperty("instructions").GetString(), StringComparison.Ordinal);

        // tool 名固定；标题与描述按会话区域取自本工具的消息目录；都只读、幂等
        var tools = replies[1].RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        Assert.Equal(["xihan_framework_config", "xihan_framework_info", "xihan_framework_search"], tools.Select(static t => t.GetProperty("name").GetString()));
        Assert.Equal("包、特性与能力", tools[1].GetProperty("title").GetString());
        Assert.All(tools, static t =>
        {
            Assert.True(t.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
            Assert.True(t.GetProperty("annotations").GetProperty("idempotentHint").GetBoolean());
            Assert.Equal("object", t.GetProperty("outputSchema").GetProperty("type").GetString());
        });

        var package = Structured(replies[2]).GetProperty("packages")[0];
        Assert.Equal("XiHan.Framework.Messaging.Kafka", package.GetProperty("id").GetString());
        Assert.Equal("stream", package.GetProperty("features")[0].GetProperty("bindings")[0].GetProperty("type").GetString());
        // 文本内容里的中文原样写出，不是 \uXXXX
        var text = replies[2].RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.DoesNotContain("\\u", text, StringComparison.Ordinal);
        Assert.Contains(text, static c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherLetter);

        var key = Structured(replies[3]).GetProperty("sections")[0].GetProperty("keys")[0];
        Assert.Equal("XiHan:Messaging:Kafka:ConsumerGroup", key.GetProperty("path").GetString());
        Assert.Equal("xihan", key.GetProperty("default").GetString());

        Assert.Equal("framework/spec/rfcs/0016-packaging.md", Structured(replies[4]).GetProperty("page").GetProperty("path").GetString());

        // 查不到与参数不合法都是 isError 结果，文本为「错误码: 文案」
        var missing = replies[5].RootElement.GetProperty("result");
        Assert.True(missing.GetProperty("isError").GetBoolean());
        Assert.StartsWith(CliErrors.KnowledgeNotFound + ": ", missing.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.True(replies[6].RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Mcp_ProjectOnAnotherFrameworkVersion_WritesTheNoticeToStderrAndInstructions()
    {
        using var project = new ProjectFixture("1.6.0");
        var (exit, replies, error) = await RunAsync(
            ["--locale", "en-US", "--path", project.ProjectDirectory],
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""");

        Assert.Equal(CommandLineExitCodes.Success, exit);
        Assert.Contains("dnx XiHan.Framework.Cli@1.6.0 --yes -- mcp", error, StringComparison.Ordinal);
        Assert.Contains("XiHan.Framework 1.6.0", replies[0].RootElement.GetProperty("result").GetProperty("instructions").GetString(), StringComparison.Ordinal);
    }
}
