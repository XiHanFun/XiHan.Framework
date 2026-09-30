// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using XiHan.Framework.Analyzers.ApiUsage;
using XiHan.Framework.Analyzers.Tests.Infrastructure;

namespace XiHan.Framework.Analyzers.Tests.ApiUsage;

/// <summary>
/// XHFA002 代码修复测试
/// </summary>
public class XiHanCancellationTokenForwardingCodeFixProviderTests
{
    private static readonly string WorkerPath = AnalyzerTestHost.FilePath("src", "Demo", "Worker.cs");

    /// <summary>
    /// 没有实参时补上命名实参
    /// </summary>
    [Fact]
    public async Task 修复以命名实参转发取消令牌()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }");

        var run = await RunAsync(code);

        Assert.Single(run.Actions);
        Assert.Contains("await InnerAsync(cancellationToken: cancellationToken);", run.FixedText);
        AssertCompiles(run.FixedText);
    }

    /// <summary>
    /// 已有实参时在末尾追加命名实参
    /// </summary>
    [Fact]
    public async Task 修复在已有实参后追加命名实参()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken ct) { await InnerWithValueAsync(1); }");

        var run = await RunAsync(code);

        Assert.Contains("await InnerWithValueAsync(1, token: ct);", run.FixedText);
        AssertCompiles(run.FixedText);
    }

    /// <summary>
    /// 多行实参列追加命名实参时尾随换行留在新实参之后
    /// </summary>
    [Fact]
    public async Task 修复多行实参列时保留尾随换行()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken ct) { await InnerWithValueAsync(\n            1\n        ); }");

        var run = await RunAsync(code);

        Assert.Contains("1, token: ct\n        )", run.FixedText.Replace("\r\n", "\n"));
        AssertCompiles(run.FixedText);
    }

    /// <summary>
    /// 参数名与令牌名是关键字时以逐字标识符转发且不抛异常
    /// </summary>
    [Fact]
    public async Task 关键字名称以逐字标识符转发()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "public class Worker",
            "{",
            "    public async Task RunAsync(CancellationToken @class) { await InnerAsync(); }",
            "    private static Task InnerAsync(CancellationToken @event = default) { return Task.CompletedTask; }",
            "}");

        var run = await RunAsync(code);

        Assert.Contains("(@event: @class)", run.FixedText);
        AssertCompiles(run.FixedText);
    }

    /// <summary>
    /// 条件访问中的调用补上命名实参后仍可编译
    /// </summary>
    [Fact]
    public async Task 修复条件访问中的调用()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken ct, Worker? worker) { await (worker?.InstAsync() ?? Task.CompletedTask); }");

        var run = await RunAsync(code);

        Assert.Contains("worker?.InstAsync(cancellationToken: ct)", run.FixedText);
        AssertCompiles(run.FixedText);
    }

    /// <summary>
    /// 扩展方法调用补上命名实参后仍可编译
    /// </summary>
    [Fact]
    public async Task 修复扩展方法调用()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken ct) { await \"text\".ExtAsync(); }");

        var run = await RunAsync(code);

        Assert.Contains("\"text\".ExtAsync(cancellationToken: ct)", run.FixedText);
        AssertCompiles(run.FixedText);
    }

    /// <summary>
    /// 修复器声明可处理 XHFA002 并支持批量修复
    /// </summary>
    [Fact]
    public void 修复器声明可修复的诊断并支持批量修复()
    {
        var provider = new XiHanCancellationTokenForwardingCodeFixProvider();

        Assert.Equal(["XHFA002"], provider.FixableDiagnosticIds);
        Assert.Same(WellKnownFixAllProviders.BatchFixer, provider.GetFixAllProvider());
    }

    private static void AssertCompiles(string text)
    {
        var compilation = AnalyzerTestHost.CreateCompilation(text, WorkerPath, TestContext.Current.CancellationToken);
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(item => item.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    private static Task<CodeFixRun> RunAsync(string code)
    {
        return AnalyzerTestHost.RunCodeFixAsync(
            new XiHanCancellationTokenForwardingAnalyzer(),
            new XiHanCancellationTokenForwardingCodeFixProvider(),
            code,
            WorkerPath,
            TestContext.Current.CancellationToken);
    }

    private static string Worker(string member)
    {
        return AnalyzerTestHost.Source(
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "public class Worker",
            "{",
            member,
            "    private static Task InnerAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "    private static Task InnerWithValueAsync(int value = 0, CancellationToken token = default) { return Task.CompletedTask; }",
            "    public Task InstAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "}",
            "public static class WorkerExtensions",
            "{",
            "    public static Task ExtAsync(this string value, CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "}");
    }
}
