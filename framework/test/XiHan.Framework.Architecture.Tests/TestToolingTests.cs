// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 测试源码只经统一入口定位工作区与监听 Span、指标
/// </summary>
/// <remarks>
/// ADR-0289 把测试项目里 31 处向上找 <c>XiHan.Framework.slnx</c> 的代码收成 <c>FrameworkWorkspace</c>（ADR-0300 挪进 <c>XiHan.Framework.Testing.Workspace</c>），6 处自己挂的监听器收成 <c>Testing</c> 包的 <c>SpanCapture</c> 与 <c>MetricCapture</c>。这里扫 <c>test</c> 下的全部 C# 源码，防止副本再长回来：出现解决方案文件名、构造 <c>ActivityListener</c> 或 <c>MeterListener</c>（含目标类型的 <c>new()</c>）的文件必须在 <see cref="_rules"/> 各自的允许列表里（ADR-0300）
/// </remarks>
public sealed partial class TestToolingTests
{
    /// <summary>
    /// 每条规则与允许出现它的文件
    /// </summary>
    /// <remarks>
    /// 路径相对工作区根。允许的只有统一入口本身：工作区根的查找在 <c>FrameworkWorkspace</c>；两种监听器都只在 <c>src</c> 下的 <c>Testing</c> 包里，不在扫描范围内，所以 <c>test</c> 下一个也不允许
    /// </remarks>
    private static readonly ToolingRule[] _rules =
    [
        new("XiHan.Framework.slnx", "names the solution file to find the workspace root; use 'FrameworkWorkspace.Root' of 'XiHan.Framework.Testing.Workspace' instead", SolutionFileName(), ["test/XiHan.Framework.Testing.Workspace/FrameworkWorkspace.cs"]),
        new("ActivityListener", "creates an 'ActivityListener'; use 'SpanCapture' of 'XiHan.Framework.Testing' instead", ActivityListenerConstruction(), []),
        new("MeterListener", "creates a 'MeterListener'; use 'MetricCapture' of 'XiHan.Framework.Testing' instead", MeterListenerConstruction(), []),
    ];

    [Fact]
    public void TestSources_UseTheSharedWorkspaceAndTelemetryEntries()
    {
        // 本文件的说明与规则里也写着这些名字，不算
        var self = Path.GetFullPath(Path.Combine(Workspace.TestRoot, "XiHan.Framework.Architecture.Tests", "TestToolingTests.cs"));
        var sources = Workspace.SourceFiles(Workspace.TestRoot)
            .Where(file => !string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase))
            .Select(static file => (Path: Workspace.Relative(file), Text: File.ReadAllText(file)))
            .ToList();
        var failures = new List<string>();

        foreach (var rule in _rules)
        {
            var hits = sources.Where(source => rule.Pattern.IsMatch(source.Text)).Select(static source => source.Path).ToHashSet(StringComparer.Ordinal);
            failures.AddRange(hits.Where(path => !rule.Allowed.Contains(path, StringComparer.Ordinal)).Order(StringComparer.Ordinal).Select(path => $"'{path}' {rule.Advice}."));

            // 允许列表只放还在用的入口：入口改了名或挪了地方，列表跟着改，不留一条放行任何文件的空位
            failures.AddRange(rule.Allowed.Where(path => !hits.Contains(path)).Select(path => $"'{path}' is in the allow list of '{rule.Name}' but no longer matches it; remove it from the list."));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Rules_CatchTheHandWrittenForms_AndLeaveTheSharedEntriesAlone()
    {
        string[] caught =
        [
            "var solution = Path.Combine(directory.FullName, \"XiHan.Framework.slnx\");",
            "using var listener = new ActivityListener { ShouldListenTo = static _ => true };",
            "ActivitySource.AddActivityListener(new System.Diagnostics.ActivityListener());",
            "private readonly ActivityListener _listener = new() { ShouldListenTo = static _ => true };",
            "var listener = new MeterListener();",
            "MeterListener? listener = new();",
            "public MeterListener Listener { get; } = new();",
        ];
        string[] passed =
        [
            "var root = FrameworkWorkspace.Root;",
            "using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);",
            "using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);",
            "ActivitySource.AddActivityListener(listener);",
        ];

        var missed = caught.Where(static code => !_rules.Any(rule => rule.Pattern.IsMatch(code))).ToList();
        var flagged = passed.Where(static code => _rules.Any(rule => rule.Pattern.IsMatch(code))).ToList();

        Assert.True(missed.Count == 0, "Hand-written forms that no rule catches: " + string.Join(" | ", missed));
        Assert.True(flagged.Count == 0, "Uses of the shared entries that a rule flags: " + string.Join(" | ", flagged));
    }

    /// <summary>
    /// 解决方案文件名
    /// </summary>
    [GeneratedRegex(@"XiHan\.Framework\.slnx")]
    private static partial Regex SolutionFileName();

    /// <summary>
    /// 构造 <c>ActivityListener</c>：<c>new ActivityListener</c> 或目标类型的 <c>new()</c>
    /// </summary>
    [GeneratedRegex(@"\bnew\s+(?:[\w.:]+\.)?ActivityListener\b|\bActivityListener\??\s+\w+\s*(?:\{[^{}]*\}\s*)?=\s*new\s*\(")]
    private static partial Regex ActivityListenerConstruction();

    /// <summary>
    /// 构造 <c>MeterListener</c>：<c>new MeterListener</c> 或目标类型的 <c>new()</c>
    /// </summary>
    [GeneratedRegex(@"\bnew\s+(?:[\w.:]+\.)?MeterListener\b|\bMeterListener\??\s+\w+\s*(?:\{[^{}]*\}\s*)?=\s*new\s*\(")]
    private static partial Regex MeterListenerConstruction();

    /// <summary>
    /// 一条规则：名字、改法、匹配的写法与允许出现它的文件
    /// </summary>
    /// <param name="Name">规则的名字，即它拦的名字</param>
    /// <param name="Advice">失败时接在文件名后面的说明：写了什么、该用哪个入口</param>
    /// <param name="Pattern">匹配的写法</param>
    /// <param name="Allowed">允许出现它的文件，路径相对工作区根</param>
    private sealed record ToolingRule(string Name, string Advice, Regex Pattern, string[] Allowed);
}
