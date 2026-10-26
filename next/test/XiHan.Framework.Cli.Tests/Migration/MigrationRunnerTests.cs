// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Cli.Migration;

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// 迁移执行器的通用机制（<c>spec/migration/README.md</c>）：两遍执行、按序改写与回写（BOM、换行）、跳过构建输出与生成文件、
/// 生成 / 删除 / 改名、报告、幂等与试运行。规则用测试规则集 <see cref="TestRuleSets.T5"/>。
/// </summary>
[Collection("Cli")]
public sealed class MigrationRunnerTests
{
    /// <summary>
    /// 跑一次测试规则集。
    /// </summary>
    private static MigrationReport Run(MigrationFixture fixture, bool dryRun = false)
        => new MigrationRunner(fixture.Root, TestRuleSets.T5.Version, TestRuleSets.T5.Fixers, dryRun).Run(CancellationToken.None);

    [Fact]
    public void Run_ScansOriginals_RewritesInOrder_KeepsEncoding_AndSkipsBuildOutput()
    {
        using var fixture = new MigrationFixture();

        var report = Run(fixture);

        Assert.Equal("t5", report.Version);
        Assert.False(report.DryRun);
        Assert.Equal(["T5-01", "T5-02", "T5-03"], report.Rules.Select(static rule => rule.Id));
        var rename = report.Rules[0];
        Assert.Equal(2, rename.Changed);
        Assert.Equal(1, rename.Markers);
        Assert.Equal(["Shop.Orders/Services/LegacyClock.cs", "Shop.Orders/Services/OrderService.cs"], rename.Files);
        var marker = Assert.Single(report.Markers);
        Assert.Equal(("T5-01", "Shop.Orders/Services/OrderService.cs", 7, RenameClockFixer.TimerMessage), (marker.Rule, marker.File, marker.Line, marker.Message));

        // 改写回写：类改名、文件随之改名、using 按字典序补在头注释之后
        Assert.False(File.Exists(fixture.PathOf("Services/LegacyClock.cs")));
        Assert.Equal("// 订单模块的时钟\nusing Shop.Time;\nusing System;\n\nnamespace Shop.Orders.Services;\n\npublic sealed class SystemClock\n{\n    public DateTime Now => DateTime.UtcNow;\n}\n", File.ReadAllText(fixture.PathOf("Services/SystemClock.cs")));

        // 原文件的 BOM 与 CRLF 保留，标记沿用成员缩进
        var bytes = File.ReadAllBytes(fixture.PathOf("Services/OrderService.cs"));
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), bytes[..3]);
        Assert.Equal(
            "using Shop.Time;\r\nusing System;\r\n\r\nnamespace Shop.Orders.Services;\r\n\r\npublic sealed class OrderService(SystemClock clock)\r\n{\r\n    // xihan-migrate(T5-01): " + RenameClockFixer.TimerMessage + "\r\n    public LegacyTimer Timer { get; } = new();\r\n\r\n    public DateTime Placed => clock.Now;\r\n}\r\n",
            Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));

        // 第一遍看到的是原文：T5-02 排在 T5-01 之后，清单里仍有两个用到 LegacyClock 的文件；bin 与 *.g.cs 不在其中
        Assert.Equal("Shop.Orders/Services/LegacyClock.cs\nShop.Orders/Services/OrderService.cs\n", File.ReadAllText(Path.Combine(fixture.Root, "MIGRATION.md")));
        Assert.Equal(MigrationFixture.UntouchedSource, File.ReadAllText(fixture.PathOf("bin/Debug/Skipped.cs")));
        Assert.Equal(MigrationFixture.UntouchedSource, File.ReadAllText(fixture.PathOf("Services/Proxy.g.cs")));

        // 生成与删除在回写之后执行
        Assert.Equal(["MIGRATION.md"], report.Rules[1].Files);
        Assert.Equal(["Shop.Orders/settings.json"], report.Rules[2].Files);
        Assert.Equal("{ \"retries\": 3 }\n", File.ReadAllText(fixture.PathOf("settings.json")));
        Assert.False(File.Exists(fixture.PathOf("settings.legacy.json")));
    }

    [Fact]
    public void Run_Again_ChangesNothing_AndStillReportsEveryRule()
    {
        using var fixture = new MigrationFixture();
        Run(fixture);
        var before = fixture.Snapshot();

        var again = Run(fixture);

        Assert.Equal(["T5-01", "T5-02", "T5-03"], again.Rules.Select(static rule => rule.Id));
        Assert.All(again.Rules, static rule => Assert.Equal((0, 0), (rule.Changed, rule.Markers)));
        Assert.Empty(again.Markers);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public void Run_DryRun_ReportsTheSameWithoutWriting()
    {
        using var fixture = new MigrationFixture();
        var before = fixture.Snapshot();

        var report = Run(fixture, dryRun: true);

        Assert.True(report.DryRun);
        Assert.Equal([(2, 1), (1, 0), (1, 0)], report.Rules.Select(static rule => (rule.Changed, rule.Markers)));
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public void Generate_SkipsContentAlreadyOnDisk()
    {
        using var fixture = new MigrationFixture();
        var session = new MigrationSession { Root = fixture.Root };
        var target = fixture.PathOf("settings.legacy.json");

        Assert.False(session.Generate("T5-03", target, "{ \"retries\": 3 }\n"));
        Assert.True(session.Generate("T5-03", target, "{ \"retries\": 4 }\n"));

        var finding = Assert.Single(session.Findings);
        Assert.Equal(("T5-03", "Shop.Orders/settings.legacy.json", 0, false), (finding.Rule, finding.File, finding.Line, finding.IsMarker));
        Assert.Same(session.StateOf<InventoryState>(), session.StateOf<InventoryState>());
    }
}
