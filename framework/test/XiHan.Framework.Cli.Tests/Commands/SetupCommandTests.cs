// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Cli.Migration;
using XiHan.Framework.Cli.Tests.Integration;
using XiHan.Framework.Hosting.CommandLine;

namespace XiHan.Framework.Cli.Tests.Commands;

/// <summary>
/// <c>xihan setup</c> 端到端：写入、幂等、试运行、核对与失败的退出码
/// </summary>
/// <remarks>
/// 与 <see cref="CliHostTests"/> 同一集合：执行命令会设置进程级的默认消息作用域
/// </remarks>
[Collection("Cli")]
public sealed class SetupCommandTests
{
    /// <summary>
    /// 执行一条命令行，返回退出码与两路输出（统一 LF）
    /// </summary>
    private static async Task<(int Exit, string Output, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await CliHost.RunAsync(args, output, error, new RecordingLauncher(), MigrationRuleSets.All, CancellationToken.None);
        return (exit, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// 读 stderr 第一行的错误码
    /// </summary>
    private static string CodeOf(string error)
    {
        using var document = JsonDocument.Parse(error.Split('\n')[0]);
        return document.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Setup_WritesConfigAndSkill_IsIdempotent_AndCheckPasses()
    {
        using var project = new ProjectFixture("5.0.0-preview.1");
        var config = Path.Combine(project.Root, ".cursor", "mcp.json");
        var skill = Path.Combine(project.Root, ".agents", "skills", "xihan-framework", "SKILL.md");

        // 从子目录运行：向上找到 git 根，版本取项目的中央包管理
        var (exit, output, error) = await RunAsync("setup", "--client", "cursor", "--path", project.ProjectDirectory, "--locale", "en-US");

        Assert.Equal(CommandLineExitCodes.Success, exit);
        Assert.Equal(string.Empty, error);
        Assert.Contains($"Wrote 'xihan-framework' into 'mcpServers' of {config}:", output, StringComparison.Ordinal);
        Assert.Contains($"Wrote {skill}.", output, StringComparison.Ordinal);
        var entry = JsonNode.Parse(File.ReadAllText(config))!["mcpServers"]!["xihan-framework"]!;
        Assert.Equal("XiHan.Framework.Cli@5.0.0-preview.1", entry["args"]![0]!.GetValue<string>());
        Assert.Equal(FrameworkKnowledge.ReadSkill(), File.ReadAllText(skill));

        var written = File.ReadAllText(config);
        Assert.Equal(CommandLineExitCodes.Success, (await RunAsync("setup", "--client", "cursor", "--path", project.Root)).Exit);
        Assert.Equal(written, File.ReadAllText(config));

        var (check, checkOutput, _) = await RunAsync("setup", "--client", "cursor", "--check", "--path", project.Root, "--locale", "en-US");
        Assert.Equal(CommandLineExitCodes.Success, check);
        Assert.Equal("The cursor configuration for XiHan.Framework.Cli@5.0.0-preview.1 is ready.\n", checkOutput);
    }

    [Fact]
    public async Task Setup_DryRun_PrintsTheSnippet_WithoutWriting_AndPinOverridesTheVersion()
    {
        using var project = new ProjectFixture("5.0.0-preview.1");

        var (exit, output, _) = await RunAsync("setup", "--client", "vscode", "--mode", "mcp", "--pin", "5.1.0", "--dry-run", "--path", project.Root, "--locale", "en-US");

        Assert.Equal(CommandLineExitCodes.Success, exit);
        Assert.Contains("Would write 'xihan-framework' into 'servers' of ", output, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"stdio\"", output, StringComparison.Ordinal);
        Assert.Contains("\"XiHan.Framework.Cli@5.1.0\"", output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(project.Root, ".vscode")));
        Assert.False(Directory.Exists(Path.Combine(project.Root, ".agents")));

        // Codex：只打印用户级配置的片段
        var (codex, codexOutput, _) = await RunAsync("setup", "--client", "codex", "--mode", "mcp", "--path", project.Root, "--locale", "en-US");
        Assert.Equal(CommandLineExitCodes.Success, codex);
        Assert.Contains("Add the following to ~/.codex/config.toml:\n[mcp_servers.xihan-framework]\n", codexOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_Failures_MapToCodesAndExitCodes_AndLeaveFilesUntouched()
    {
        using var project = new ProjectFixture(null);

        var (client, _, clientError) = await RunAsync("setup", "--client", "emacs", "--path", project.Root);
        Assert.Equal(CommandLineExitCodes.Usage, client);
        Assert.Equal(CliErrors.SetupOptionInvalid, CodeOf(clientError));

        var (mode, _, modeError) = await RunAsync("setup", "--client", "claude", "--mode", "all", "--path", project.Root);
        Assert.Equal(CommandLineExitCodes.Usage, mode);
        Assert.Equal(CliErrors.SetupOptionInvalid, CodeOf(modeError));

        var (root, _, rootError) = await RunAsync("setup", "--client", "claude", "--path", Path.Combine(project.Root, "missing"));
        Assert.Equal(CommandLineExitCodes.NotFound, root);
        Assert.Equal(CliErrors.SetupRootNotFound, CodeOf(rootError));

        var (check, _, checkError) = await RunAsync("setup", "--client", "claude", "--check", "--path", project.Root);
        Assert.Equal(CommandLineExitCodes.Conflict, check);
        Assert.Equal(CliErrors.SetupNotReady, CodeOf(checkError));

        // 配置文件带注释（JSONC）读不了：一个文件都不写，技能也不写
        Directory.CreateDirectory(Path.Combine(project.Root, ".vscode"));
        var config = Path.Combine(project.Root, ".vscode", "mcp.json");
        File.WriteAllText(config, "// mine\n{ \"servers\": {} }");
        var (invalid, _, invalidError) = await RunAsync("setup", "--client", "vscode", "--path", project.Root);
        Assert.Equal(CommandLineExitCodes.Validation, invalid);
        Assert.Equal(CliErrors.SetupConfigInvalid, CodeOf(invalidError));
        Assert.Equal("// mine\n{ \"servers\": {} }", File.ReadAllText(config));
        Assert.False(Directory.Exists(Path.Combine(project.Root, ".agents")));
    }
}
