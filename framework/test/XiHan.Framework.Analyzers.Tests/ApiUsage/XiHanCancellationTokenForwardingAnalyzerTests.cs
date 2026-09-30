// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Globalization;
using XiHan.Framework.Analyzers.ApiUsage;
using XiHan.Framework.Analyzers.Tests.Infrastructure;

namespace XiHan.Framework.Analyzers.Tests.ApiUsage;

/// <summary>
/// XHFA002 取消令牌转发规则测试
/// </summary>
public class XiHanCancellationTokenForwardingAnalyzerTests
{
    private const string DiagnosticId = "XHFA002";

    private static readonly string WorkerPath = AnalyzerTestHost.FilePath("src", "Demo", "Worker.cs");

    /// <summary>
    /// 公开异步方法省略可选取消令牌时报告，位置、级别与消息正确
    /// </summary>
    [Fact]
    public async Task 公开异步方法省略可选取消令牌时报告()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }");

        var diagnostic = Assert.Single(await AnalyzeAsync(code));

        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal("InnerAsync()", code.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        Assert.Equal(WorkerPath, diagnostic.Location.GetLineSpan().Path);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("InnerAsync", message);
        Assert.Contains("cancellationToken", message);
    }

    /// <summary>
    /// 显式传入令牌、None 或 default 都不报告
    /// </summary>
    [Theory]
    [InlineData("InnerAsync(cancellationToken)")]
    [InlineData("InnerAsync(CancellationToken.None)")]
    [InlineData("InnerAsync(default)")]
    public async Task 显式传入取消令牌不报告(string call)
    {
        var code = Worker($"    public async Task RunAsync(CancellationToken cancellationToken) {{ await {call}; }}");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 外层方法没有取消令牌参数时不报告
    /// </summary>
    [Fact]
    public async Task 外层方法没有取消令牌参数不报告()
    {
        var code = Worker("    public async Task RunAsync() { await InnerAsync(); }");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 非公开的方法不报告
    /// </summary>
    [Theory]
    [InlineData("internal")]
    [InlineData("private")]
    [InlineData("private protected")]
    public async Task 非公开方法不报告(string modifier)
    {
        var code = Worker($"    {modifier} async Task RunAsync(CancellationToken cancellationToken) {{ await InnerAsync(); }}");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 受保护的方法属于对外 API，同样报告
    /// </summary>
    [Fact]
    public async Task 受保护方法报告()
    {
        var code = Worker("    protected async Task RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }");

        Assert.Single(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 非公开类型中的公开方法不报告
    /// </summary>
    [Fact]
    public async Task 非公开类型中的方法不报告()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "internal class Worker",
            "{",
            "    public async Task RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }",
            "    private static Task InnerAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "}");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 返回类型不是可等待类型时不报告
    /// </summary>
    [Fact]
    public async Task 非异步返回类型不报告()
    {
        var code = Worker("    public void Run(CancellationToken cancellationToken) { InnerAsync(); }");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// Lambda 与本地函数内的调用不报告
    /// </summary>
    [Fact]
    public async Task Lambda与本地函数内的调用不报告()
    {
        var code = Worker(
            "    public async Task RunAsync(CancellationToken cancellationToken)",
            "    {",
            "        Func<Task> callback = () => InnerAsync();",
            "        await callback();",
            "        await LocalAsync();",
            "        Task LocalAsync() { return InnerAsync(); }",
            "    }");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 被调方法没有取消令牌参数时不报告
    /// </summary>
    [Fact]
    public async Task 被调方法没有取消令牌参数不报告()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken cancellationToken) { await PlainAsync(); }");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 令牌排在其他可选参数之后被省略时同样报告
    /// </summary>
    [Fact]
    public async Task 其他可选参数之后省略令牌时报告()
    {
        var code = Worker("    public async Task RunAsync(CancellationToken cancellationToken) { await InnerWithValueAsync(1); }");

        var diagnostic = Assert.Single(await AnalyzeAsync(code));

        Assert.Contains("InnerWithValueAsync", diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 对外可见接口的显式实现视为公开 API
    /// </summary>
    [Fact]
    public async Task 显式接口实现报告()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "public interface IWorker { Task RunAsync(CancellationToken cancellationToken); }",
            "public class Worker : IWorker",
            "{",
            "    Task IWorker.RunAsync(CancellationToken cancellationToken) { return InnerAsync(); }",
            "    private static Task InnerAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "}");

        Assert.Single(await AnalyzeAsync(code));
    }

    /// <summary>
    /// ValueTask 与 IAsyncEnumerable 返回类型同样检查
    /// </summary>
    [Fact]
    public async Task ValueTask与异步流返回类型报告()
    {
        var code = Worker(
            "    public async ValueTask RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }",
            "    public async IAsyncEnumerable<int> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)",
            "    {",
            "        await InnerAsync();",
            "        yield return 1;",
            "    }");

        Assert.Equal(2, (await AnalyzeAsync(code)).Length);
    }

    /// <summary>
    /// 生成代码不报告
    /// </summary>
    [Fact]
    public async Task 生成代码不报告()
    {
        var code = "// <auto-generated/>\n" + Worker("    public async Task RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// pragma 抑制后只剩范围外的那一处未被抑制的诊断
    /// </summary>
    [Fact]
    public async Task Pragma抑制后只剩范围外的诊断()
    {
        var code = Worker(
            "    public async Task RunAsync(CancellationToken cancellationToken)",
            "    {",
            "#pragma warning disable XHFA002",
            "        await InnerAsync();",
            "#pragma warning restore XHFA002",
            "        await InnerAsync();",
            "    }");

        var unsuppressed = (await AnalyzeAsync(code)).Where(item => !item.IsSuppressed).ToList();

        Assert.Single(unsuppressed);
    }

    /// <summary>
    /// 集合初始化器隐式调用的 Add 不报告
    /// </summary>
    [Fact]
    public async Task 集合初始化器的隐式调用不报告()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Collections;",
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "public class Coll : IEnumerable",
            "{",
            "    public void Add(int value, CancellationToken token = default) { }",
            "    public IEnumerator GetEnumerator() { return null!; }",
            "}",
            "public class Worker",
            "{",
            "    public async Task RunAsync(CancellationToken cancellationToken)",
            "    {",
            "        var coll = new Coll { 1 };",
            "        await Task.CompletedTask;",
            "    }",
            "}");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 内部接口的显式实现不对外可见，不报告
    /// </summary>
    [Fact]
    public async Task 内部接口的显式实现不报告()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "internal interface IWorker { Task RunAsync(CancellationToken cancellationToken); }",
            "public class Worker : IWorker",
            "{",
            "    Task IWorker.RunAsync(CancellationToken cancellationToken) { return InnerAsync(); }",
            "    private static Task InnerAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "}");

        Assert.Empty(await AnalyzeAsync(code));
    }

    /// <summary>
    /// 内部类型中嵌套的公开类型不对外可见，不报告
    /// </summary>
    [Fact]
    public async Task 内部类型中嵌套的公开类型不报告()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "internal class Outer",
            "{",
            "    public class Worker",
            "    {",
            "        public async Task RunAsync(CancellationToken cancellationToken) { await InnerAsync(); }",
            "        private static Task InnerAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }",
            "    }",
            "}");

        Assert.Empty(await AnalyzeAsync(code));
    }

    private static string Worker(params string[] members)
    {
        var lines = new List<string>
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Runtime.CompilerServices;",
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "namespace Demo;",
            "public class Worker",
            "{"
        };
        lines.AddRange(members);
        lines.Add("    private static Task InnerAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }");
        lines.Add("    private static Task InnerWithValueAsync(int value = 0, CancellationToken token = default) { return Task.CompletedTask; }");
        lines.Add("    private static Task PlainAsync() { return Task.CompletedTask; }");
        lines.Add("}");
        return AnalyzerTestHost.Source([.. lines]);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string code)
    {
        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanCancellationTokenForwardingAnalyzer(),
            code,
            WorkerPath,
            TestContext.Current.CancellationToken);

        return [.. diagnostics.Where(item => item.Id == DiagnosticId)];
    }
}
