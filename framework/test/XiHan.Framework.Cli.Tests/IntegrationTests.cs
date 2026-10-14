// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using XiHan.Framework.Cli.Integration;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Cli.Mcp;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 命令行、setup 与 MCP 服务端
/// </summary>
public sealed class IntegrationTests : IDisposable
{
    private static readonly FrameworkKnowledge Knowledge = FrameworkKnowledge.Load();

    private readonly string _root = Directory.CreateTempSubdirectory("xihan-framework-cli-").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private void UseFrameworkVersion(string version)
    {
        File.WriteAllText(Path.Combine(_root, "App.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="XiHan.Framework.Web.Api" Version="{version}" />
              </ItemGroup>
            </Project>
            """);
    }

    private async Task<(int Code, string Out, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await CliApp.RunAsync(args, new CliEnvironment(output, error, _root), Knowledge, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Cli_PrintsVersionHelpAndJson()
    {
        Assert.Equal(Knowledge.Version, (await RunAsync("--version")).Out.Trim());
        Assert.Contains("用法：xihan-framework", (await RunAsync()).Out, StringComparison.Ordinal);
        var (code, json, _) = await RunAsync("info", "Caching", "--format", "json");
        Assert.Equal(0, code);
        Assert.Equal("XiHan.Framework.Caching", JsonNode.Parse(json)!["package"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cli_ReturnsOneWhenNotFoundAndTwoOnUsageErrors()
    {
        var missing = await RunAsync("info", "Cachng");
        Assert.Equal(1, missing.Code);
        Assert.Contains("XiHan.Framework.Caching", missing.Error, StringComparison.Ordinal);
        Assert.Equal(2, (await RunAsync("nope")).Code);
        Assert.Equal(2, (await RunAsync("info")).Code);
        Assert.Equal(2, (await RunAsync("list", "--bogus")).Code);
        Assert.Equal(2, (await RunAsync("list", "--format", "xml")).Code);
    }

    [Fact]
    public async Task Cli_WarnsWhenProjectVersionDiffers()
    {
        UseFrameworkVersion("1.0.0");
        var (code, _, error) = await RunAsync("info", "Caching");
        Assert.Equal(0, code);
        Assert.Contains("dnx XiHan.Framework.Cli@1.0.0", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_WritesPinnedConfigAndChecksIt()
    {
        UseFrameworkVersion("1.0.0");
        Assert.Equal(1, (await RunAsync("setup", "--client", "cursor", "--mode", "mcp", "--check")).Code);
        var dry = await RunAsync("setup", "--client", "cursor", "--mode", "mcp", "--dry-run");
        Assert.Contains("XiHan.Framework.Cli@1.0.0", dry.Out, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_root, ".cursor", "mcp.json")));
        Assert.Equal(0, (await RunAsync("setup", "--client", "cursor", "--mode", "mcp")).Code);
        Assert.Equal(0, (await RunAsync("setup", "--client", "cursor", "--mode", "mcp", "--check")).Code);
    }

    [Fact]
    public void SetupPlanner_MergesWithoutTouchingOtherServers()
    {
        var path = Path.Combine(_root, ".mcp.json");
        File.WriteAllText(path, """{"mcpServers":{"other":{"command":"x"}},"extra":true}""");
        var action = Assert.Single(SetupPlanner.Plan("claude", "mcp", _root, "4.6.2", windows: false, Knowledge.Skill));
        Assert.Contains("没有 xihan-framework", SetupPlanner.Check(action), StringComparison.Ordinal);
        SetupPlanner.Apply(action);
        var config = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.True(config["extra"]!.GetValue<bool>());
        Assert.Equal("x", config["mcpServers"]!["other"]!["command"]!.GetValue<string>());
        Assert.Equal("dnx", config["mcpServers"]!["xihan-framework"]!["command"]!.GetValue<string>());
        Assert.Null(SetupPlanner.Check(action));
    }

    [Fact]
    public void SetupPlanner_ShapesPerClient()
    {
        var (command, args) = SetupPlanner.ServerCommand("claude", "4.6.2", windows: true);
        Assert.Equal("cmd", command);
        Assert.Equal(["/c", "dnx", "XiHan.Framework.Cli@4.6.2", "--yes", "--", "mcp"], args);
        Assert.Equal("dnx", SetupPlanner.ServerCommand("vscode", "4.6.2", windows: true).Command);
        var vscode = Assert.Single(SetupPlanner.Plan("vscode", "mcp", _root, "4.6.2", windows: false, Knowledge.Skill));
        Assert.Equal("servers", vscode.Key);
        Assert.Equal("stdio", vscode.Entry!["type"]!.GetValue<string>());
        var codex = Assert.Single(SetupPlanner.Plan("codex", "mcp", _root, "4.6.2", windows: false, Knowledge.Skill));
        Assert.Equal("print", codex.Kind);
        Assert.StartsWith("[mcp_servers.xihan-framework]", codex.Content, StringComparison.Ordinal);
        var skills = SetupPlanner.Plan("cursor", "both", _root, "4.6.2", windows: false, Knowledge.Skill);
        Assert.EndsWith(Path.Combine(".agents", "skills", "xihan-framework", "SKILL.md"), skills[1].Path, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupPlanner_LeavesUnparsableConfigUntouched()
    {
        var path = Path.Combine(_root, ".mcp.json");
        File.WriteAllText(path, "{ // 注释\n}");
        var action = Assert.Single(SetupPlanner.Plan("claude", "mcp", _root, "4.6.2", windows: false, Knowledge.Skill));
        Assert.Throws<InvalidOperationException>(() => SetupPlanner.Apply(action));
        Assert.Equal("{ // 注释\n}", File.ReadAllText(path));
    }

    [Fact]
    public async Task McpServer_ServesToolsAndPromptsOverStreams()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var toServer = new Pipe();
        var toClient = new Pipe();
        var serverTransport = new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream(), McpHost.ServerName);
        var serverTask = McpHost.RunAsync(serverTransport, McpHost.CreateOptions(Knowledge, "说明"), cancellationToken);

        await using (var client = await McpClient.CreateAsync(new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()), cancellationToken: cancellationToken))
        {
            Assert.Equal(McpHost.ServerName, client.ServerInfo.Name);
            Assert.Equal("说明", client.ServerInstructions);

            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            Assert.Equal(
                ["xihan_framework_changelog", "xihan_framework_config", "xihan_framework_doc", "xihan_framework_info", "xihan_framework_list", "xihan_framework_search"],
                tools.Select(t => t.Name).Order(StringComparer.Ordinal));
            var info = tools.Single(t => t.Name == "xihan_framework_info");
            Assert.True(info.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.Contains("package", info.JsonSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));

            var found = await client.CallToolAsync("xihan_framework_info", new Dictionary<string, object?> { ["package"] = "Caching" }, cancellationToken: cancellationToken);
            Assert.NotEqual(true, found.IsError);
            Assert.Contains("XiHanCachingModule", Assert.IsType<TextContentBlock>(found.Content[0]).Text, StringComparison.Ordinal);

            var missing = await client.CallToolAsync("xihan_framework_info", new Dictionary<string, object?> { ["package"] = "Cachng" }, cancellationToken: cancellationToken);
            Assert.True(missing.IsError);
            Assert.Contains("XiHan.Framework.Caching", Assert.IsType<TextContentBlock>(missing.Content[0]).Text, StringComparison.Ordinal);

            var listed = await client.CallToolAsync("xihan_framework_config", new Dictionary<string, object?>(), cancellationToken: cancellationToken);
            Assert.Contains("XiHan:Caching:Redis", Assert.IsType<TextContentBlock>(listed.Content[0]).Text, StringComparison.Ordinal);

            var prompts = await client.ListPromptsAsync(cancellationToken: cancellationToken);
            Assert.Equal(["xihan-framework-expert", "xihan-framework-module"], prompts.Select(p => p.Name).Order(StringComparer.Ordinal));
            var expert = await client.GetPromptAsync("xihan-framework-expert", cancellationToken: cancellationToken);
            var text = Assert.IsType<TextContentBlock>(Assert.Single(expert.Messages).Content).Text;
            Assert.Contains("# 在项目里使用 XiHan.Framework", text, StringComparison.Ordinal);
            Assert.DoesNotContain("name: xihan-framework", text, StringComparison.Ordinal);
        }

        await toServer.Writer.CompleteAsync();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
