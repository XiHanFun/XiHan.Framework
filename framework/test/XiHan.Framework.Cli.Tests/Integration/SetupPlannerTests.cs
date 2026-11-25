// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Cli.Integration;

namespace XiHan.Framework.Cli.Tests.Integration;

/// <summary>
/// setup 的动作规划、合并写入与核对
/// </summary>
[Collection("Cli")]
public sealed class SetupPlannerTests
{
    /// <summary>
    /// 测试用的技能原文
    /// </summary>
    private const string Skill = "---\nname: xihan-framework\n---\n\n# skill\n";

    [Fact]
    public void Plan_EachClient_WritesItsOwnConfigAndSkillLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "root");

        var claude = SetupPlanner.Plan("claude", "both", root, "5.0.0", windows: true, Skill);
        Assert.Equal([SetupActionKind.Json, SetupActionKind.File], claude.Select(static a => a.Kind));
        Assert.Equal(Path.Combine(root, ".mcp.json"), claude[0].Path);
        Assert.Equal("mcpServers", claude[0].Key);
        // Windows 上的 Claude Code 经 cmd /c 才找得到 dnx
        Assert.Equal("""{"command":"cmd","args":["/c","dnx","XiHan.Framework.Cli@5.0.0","--yes","--","mcp"]}""", claude[0].Entry!.ToJsonString());
        Assert.Equal(Path.Combine(root, ".claude", "skills", "xihan-framework", "SKILL.md"), claude[1].Path);
        Assert.Equal(Skill, claude[1].Content);

        var claudeOnLinux = SetupPlanner.Plan("claude", "mcp", root, "5.0.0", windows: false, Skill);
        Assert.Equal("""{"command":"dnx","args":["XiHan.Framework.Cli@5.0.0","--yes","--","mcp"]}""", Assert.Single(claudeOnLinux).Entry!.ToJsonString());

        var cursor = SetupPlanner.Plan("cursor", "both", root, "5.0.0", windows: true, Skill);
        Assert.Equal(Path.Combine(root, ".cursor", "mcp.json"), cursor[0].Path);
        Assert.Equal("""{"command":"dnx","args":["XiHan.Framework.Cli@5.0.0","--yes","--","mcp"]}""", cursor[0].Entry!.ToJsonString());
        Assert.Equal(Path.Combine(root, ".agents", "skills", "xihan-framework", "SKILL.md"), cursor[1].Path);

        // VS Code 的服务表键是 servers，每项带传输类型
        var vscode = Assert.Single(SetupPlanner.Plan("vscode", "mcp", root, "5.0.0", windows: false, Skill));
        Assert.Equal(Path.Combine(root, ".vscode", "mcp.json"), vscode.Path);
        Assert.Equal("servers", vscode.Key);
        Assert.Equal("""{"type":"stdio","command":"dnx","args":["XiHan.Framework.Cli@5.0.0","--yes","--","mcp"]}""", vscode.Entry!.ToJsonString());

        // Codex 只有用户级配置：只打印片段
        var codex = Assert.Single(SetupPlanner.Plan("codex", "mcp", root, "5.0.0", windows: false, Skill));
        Assert.Equal(SetupActionKind.Print, codex.Kind);
        Assert.Equal("[mcp_servers.xihan-framework]\ncommand = \"dnx\"\nargs = [\"XiHan.Framework.Cli@5.0.0\", \"--yes\", \"--\", \"mcp\"]", codex.Content);

        var skillOnly = Assert.Single(SetupPlanner.Plan("codex", "skill", root, "5.0.0", windows: false, Skill));
        Assert.Equal(SetupActionKind.File, skillOnly.Kind);
    }

    [Fact]
    public void Apply_MergesOnlyItsOwnEntry_AndIsIdempotent()
    {
        using var project = new ProjectFixture(null);
        var path = Path.Combine(project.Root, ".mcp.json");
        File.WriteAllText(path, """{ "mcpServers": { "other": { "command": "node", "args": ["x.js"] }, "xihan-framework": { "command": "old" } }, "keep": true }""");
        var action = SetupPlanner.Plan("claude", "mcp", project.Root, "5.0.0", windows: false, Skill)[0];

        Assert.True(SetupPlanner.Apply(action).IsSuccess);
        var first = File.ReadAllText(path);
        Assert.True(SetupPlanner.Apply(action).IsSuccess);

        Assert.Equal(first, File.ReadAllText(path));
        Assert.DoesNotContain('\r', first);
        var config = JsonNode.Parse(first)!.AsObject();
        Assert.True(config["keep"]!.GetValue<bool>());
        Assert.Equal("node", config["mcpServers"]!["other"]!["command"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(action.Entry, config["mcpServers"]!["xihan-framework"]));
        Assert.Null(SetupPlanner.Check(action));
    }

    [Fact]
    public void ReadConfig_ThatIsNotValidUtf8_IsRejected_AndApplyLeavesTheFileAsItIs()
    {
        // 不是合法 UTF-8 的文件不是合法 JSON（RFC 8259 §8.1、RFC-0001「JSON 文本」，ADR-0312）：报 CLI_VALIDATION_006、一个文件都不写；
        // 修之前按 U+FFFD 读出、合并后写回，把用户文件里的那个字节悄悄改成了 EF BF BD
        using var project = new ProjectFixture(null);
        var action = SetupPlanner.Plan("claude", "mcp", project.Root, "5.0.0", windows: false, Skill)[0];
        byte[] bytes = [.. "{ \"mcpServers\": { \"other\": { \"command\": \"x"u8, 0xC3, .. "\" } } }"u8];
        File.WriteAllBytes(action.Path, bytes);

        var read = SetupPlanner.ReadConfig(action.Path);

        Assert.Equal(CliErrors.SetupConfigInvalid, read.Error!.Code);
        Assert.Equal(CliMessages.SetupConfigInvalid, read.Error.MessageKey);
        Assert.Equal("the file is not valid UTF-8", read.Error.Details!["reason"]);
        Assert.True(SetupPlanner.Apply(action).IsFailure);
        Assert.Equal(bytes, File.ReadAllBytes(action.Path));

        // 带 UTF-8 BOM 的文件照旧读得出
        File.WriteAllBytes(action.Path, [0xEF, 0xBB, 0xBF, .. """{ "mcpServers": {} }"""u8]);
        Assert.True(SetupPlanner.ReadConfig(action.Path).ValueOrThrow().ContainsKey("mcpServers"));
    }

    [Fact]
    public void ReadConfig_CommentsOrNonObject_AreRejected_AndCheckReportsEachProblem()
    {
        using var project = new ProjectFixture(null);
        var path = Path.Combine(project.Root, "mcp.json");
        File.WriteAllText(path, "// comment\n{ }");
        Assert.Equal(CliErrors.SetupConfigInvalid, SetupPlanner.ReadConfig(path).Error!.Code);
        File.WriteAllText(path, "[]");
        Assert.Equal(CliMessages.SetupConfigNotObject, SetupPlanner.ReadConfig(path).Error!.MessageKey);
        File.WriteAllText(path, "  ");
        Assert.Empty(SetupPlanner.ReadConfig(path).ValueOrThrow());

        var actions = SetupPlanner.Plan("cursor", "both", project.Root, "5.0.0", windows: false, Skill);
        Assert.All(actions, static a => Assert.NotNull(SetupPlanner.Check(a)));
        Directory.CreateDirectory(Path.Combine(project.Root, ".cursor"));
        File.WriteAllText(actions[0].Path, """{ "mcpServers": {} }""");
        Assert.Contains("xihan-framework", SetupPlanner.Check(actions[0]), StringComparison.Ordinal);
        File.WriteAllText(actions[0].Path, """{ "mcpServers": { "xihan-framework": { "command": "dnx", "args": [] } } }""");
        Assert.NotNull(SetupPlanner.Check(actions[0]));
        Assert.True(SetupPlanner.Apply(actions[1]).IsSuccess);
        Assert.Null(SetupPlanner.Check(actions[1]));
        File.WriteAllText(actions[1].Path, "edited");
        Assert.NotNull(SetupPlanner.Check(actions[1]));
    }
}
