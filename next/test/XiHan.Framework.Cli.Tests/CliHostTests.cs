// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 工具的命令分派：pack 端到端、失败的退出码与 stderr、转发命令、help 与 version、migrate 的入参校验与试运行。
/// 与 <see cref="MigrationTests"/> 同一集合：执行命令会设置进程级的默认消息作用域，标记文案不能在两组测试之间变。
/// </summary>
[Collection("Cli")]
public sealed class CliHostTests
{
    /// <summary>
    /// 执行一条命令行，返回退出码与两路输出。
    /// </summary>
    private static async Task<(int Exit, string Output, string Error)> RunAsync(RecordingLauncher? launcher, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await CliHost.RunAsync(args, output, error, launcher ?? new RecordingLauncher(), CancellationToken.None);
        // 写入器按平台换行；断言统一按 LF
        return (exit, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// 读 stderr 第一行里的一个字段（错误码或文案）。
    /// </summary>
    private static string FieldOf(string error, string name)
    {
        using var document = JsonDocument.Parse(error.Split('\n')[0]);
        return document.RootElement.GetProperty(name).GetString()!;
    }

    /// <summary>
    /// 读 stderr 第一行的错误码。
    /// </summary>
    private static string CodeOf(string error) => FieldOf(error, "code");

    [Fact]
    public async Task Pack_WritesEveryArtifact_AndListsThem()
    {
        using var fixture = new ManifestFixture();
        var (exit, output, error) = await RunAsync(null, "pack", "--manifest", fixture.ManifestPath, "--locale", "en-US");

        Assert.Equal(CommandLineExitCodes.Success, exit);
        Assert.Empty(error);
        Assert.Contains("Dockerfile\n", output, StringComparison.Ordinal);
        Assert.Contains("kustomize/deployment.yaml\n", output, StringComparison.Ordinal);
        Assert.Contains("compose.yaml\n", output, StringComparison.Ordinal);
        Assert.Contains("aspire/XiHanAppHost.g.cs\n", output, StringComparison.Ordinal);
        Assert.Contains($"12 files written to '{fixture.OutputDirectory}'", output, StringComparison.Ordinal);
        Assert.Contains("kind: Deployment", fixture.Output("kustomize/deployment.yaml"), StringComparison.Ordinal);

        // --only 与 --output：只生成 Compose，写到指定目录
        var custom = Path.Combine(fixture.Directory, "out");
        var (onlyExit, onlyOutput, _) = await RunAsync(null, "pack", "--manifest", fixture.ManifestPath, "--output", custom, "--only", "compose");
        Assert.Equal(CommandLineExitCodes.Success, onlyExit);
        Assert.Equal("compose.yaml", onlyOutput.Split('\n')[0]);
        Assert.True(File.Exists(Path.Combine(custom, "compose.yaml")));
        Assert.False(File.Exists(Path.Combine(custom, "Dockerfile")));
        Assert.Contains("context: ..", File.ReadAllText(Path.Combine(custom, "compose.yaml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pack_Failures_MapToExitCodes()
    {
        var missing = Path.Combine(Path.GetTempPath(), "xihan-missing-" + Guid.NewGuid().ToString("N"), "xihan.app.json");
        var (notFound, _, notFoundError) = await RunAsync(null, "pack", "--manifest", missing);
        Assert.Equal(CommandLineExitCodes.NotFound, notFound);
        Assert.Equal(CliErrors.ManifestNotFound, CodeOf(notFoundError));

        using var fixture = new ManifestFixture("""{ "name": "Bad Name", "project": "Orders.csproj" }""");
        var (invalid, _, invalidError) = await RunAsync(null, "pack", "--manifest", fixture.ManifestPath, "--locale", "zh-CN");
        Assert.Equal(CommandLineExitCodes.Validation, invalid);
        Assert.Equal(CliErrors.ManifestInvalid, CodeOf(invalidError));
        Assert.Contains("不合法", FieldOf(invalidError, "detail"), StringComparison.Ordinal);

        using var valid = new ManifestFixture();
        var (usage, _, usageError) = await RunAsync(null, "pack", "--manifest", valid.ManifestPath, "--only", "helm");
        Assert.Equal(CommandLineExitCodes.Usage, usage);
        Assert.Equal(CliErrors.ManifestInvalid, CodeOf(usageError));
    }

    [Fact]
    public async Task Forwarding_RunsTheApplicationCommandLineHost()
    {
        var launcher = new RecordingLauncher { ExitCode = 3 };
        var (exit, output, _) = await RunAsync(launcher, "graph", "--project", "src/Orders", "--configuration", "Debug", "--locale", "en-US");

        Assert.Equal(3, exit);
        Assert.Equal("dotnet", launcher.FileName);
        Assert.Equal(["run", "--project", "src/Orders", "--configuration", "Debug", "--", "graph", "--locale", "en-US"], launcher.Arguments);
        Assert.Contains("forwarded", output, StringComparison.Ordinal);

        // 简写 orders create … → invoke orders.create …；默认 Release
        var shorthand = new RecordingLauncher();
        await RunAsync(shorthand, "orders", "create", "--name", "widget");
        Assert.Equal(["run", "--configuration", "Release", "--", "invoke", "orders.create", "--name", "widget"], shorthand.Arguments);

        var unavailable = new RecordingLauncher { Unavailable = true };
        var (failed, _, error) = await RunAsync(unavailable, "pipeline");
        Assert.Equal(CommandLineExitCodes.Unavailable, failed);
        Assert.Equal(CliErrors.DotnetUnavailable, CodeOf(error));
    }

    [Fact]
    public async Task Help_Version_AndUsageErrors()
    {
        var (help, helpOutput, _) = await RunAsync(null, "help", "--locale", "en-US");
        Assert.Equal(CommandLineExitCodes.Success, help);
        foreach (var command in new[] { "pack", "migrate", "graph", "pipeline", "operations", "invoke", "version", "help" })
        {
            Assert.Contains(command, helpOutput, StringComparison.Ordinal);
        }

        Assert.Contains("Generate deployment artifacts", helpOutput, StringComparison.Ordinal);

        var (version, versionOutput, _) = await RunAsync(null, "version");
        Assert.Equal(CommandLineExitCodes.Success, version);
        Assert.Contains(FrameworkInfo.Version, versionOutput, StringComparison.Ordinal);

        var (none, _, noneError) = await RunAsync(null);
        Assert.Equal(CommandLineExitCodes.Usage, none);
        Assert.Contains("pack", noneError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrate_ValidatesInput_PrintsTable_AndDryRunWritesReportOnly()
    {
        using var fixture = new MigrationFixture();

        var (badVersion, _, badVersionError) = await RunAsync(null, "migrate", "v3", "--path", fixture.Root);
        Assert.Equal(CommandLineExitCodes.Usage, badVersion);
        Assert.Equal(CliErrors.MigrateVersionUnknown, CodeOf(badVersionError));

        var (missing, _, missingError) = await RunAsync(null, "migrate", "v4", "--path", Path.Combine(fixture.Root, "nowhere"));
        Assert.Equal(CommandLineExitCodes.NotFound, missing);
        Assert.Equal(CliErrors.MigratePathNotFound, CodeOf(missingError));

        var (badFixer, _, badFixerError) = await RunAsync(null, "migrate", "v4", "--path", fixture.Root, "--fixers", "V4-01,V4-99");
        Assert.Equal(CommandLineExitCodes.Usage, badFixer);
        Assert.Equal(CliErrors.MigrateFixerUnknown, CodeOf(badFixerError));

        var reportPath = Path.Combine(fixture.Root, "report.json");
        var (dryRun, output, _) = await RunAsync(null, "migrate", "v4", "--path", fixture.Project, "--dry-run", "--report", reportPath, "--locale", "en-US");
        Assert.Equal(CommandLineExitCodes.Success, dryRun);
        Assert.Contains("Rule", output, StringComparison.Ordinal);
        Assert.Contains("V4-01", output, StringComparison.Ordinal);
        Assert.Contains("Dry run", output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.Project, "XiHanShopOrdersModule.cs")));
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal("v4", report.RootElement.GetProperty("version").GetString());
        Assert.True(report.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Equal("V4-01", report.RootElement.GetProperty("rules")[0].GetProperty("id").GetString());
    }
}
