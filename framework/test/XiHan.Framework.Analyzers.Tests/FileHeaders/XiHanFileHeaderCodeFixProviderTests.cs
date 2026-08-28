// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Reflection;
using XiHan.Framework.Analyzers.FileHeaders;

namespace XiHan.Framework.Analyzers.Tests.FileHeaders;

/// <summary>
/// 曦寒标准版权文件头代码修复器测试
/// </summary>
/// <remarks>
/// 修复器是要在 IDE 里一键改用户源文件的，写错就是批量毁代码，所以断言口径比分析器更严：
/// 一是修复结果逐字断言，不用「包含」这种松口径；二是每条修复都做闭环——把修好的文本重新喂给分析器，
/// 必须一条诊断都不剩，否则「修了但还报」会让用户反复点修复、每点一次多插一个文件头。
/// 另外锁住批量修复提供器：缺了它，IDE 的「修复解决方案中所有此类问题」会直接不可用。
/// </remarks>
public class XiHanFileHeaderCodeFixProviderTests
{
    /// <summary>
    /// 单个用例的超时上限，防止修复链路意外死循环把 CI 挂住
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    private const string DiagnosticId = "XHFH001";

    /// <summary>
    /// 修复器只认领文件头这一条诊断
    /// </summary>
    /// <remarks>
    /// 多认领会让它被无关诊断触发，少认领则整条修复链路静默失效——两边都要钉住。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void FixableDiagnosticIds_ContainsOnlyFileHeaderRule()
    {
        var provider = new XiHanFileHeaderCodeFixProvider();

        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal(DiagnosticId, id);
    }

    /// <summary>
    /// 提供内置批量修复器，支持文档/项目/解决方案级一次性修复
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void GetFixAllProvider_ReturnsBatchFixer()
    {
        var provider = new XiHanFileHeaderCodeFixProvider();

        Assert.Same(WellKnownFixAllProviders.BatchFixer, provider.GetFixAllProvider());
    }

    /// <summary>
    /// 修复器按 C# 语言导出，且导出名与类型名一致
    /// </summary>
    /// <remarks>
    /// 语言写错 IDE 根本不会加载它；导出名是 MEF 组合的标识，改名等于换了一个修复器。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void ExportCodeFixProviderAttribute_DeclaresCSharpOnly()
    {
        var attribute = typeof(XiHanFileHeaderCodeFixProvider).GetCustomAttribute<ExportCodeFixProviderAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(new[] { LanguageNames.CSharp }, attribute!.Languages);
        Assert.Equal(nameof(XiHanFileHeaderCodeFixProvider), attribute.Name);
    }

    /// <summary>
    /// 完全没有文件头时在文件顶部插入标准头，原有内容一字不动
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_WhenHeaderMissing_InsertsStandardHeaderAtTop()
    {
        var code = AnalyzerTestHost.Source(
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "}");

        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Single(run.Diagnostics);
        Assert.Single(run.Actions);
        Assert.Equal(AnalyzerTestHost.StandardHeader + code, run.FixedText);
    }

    /// <summary>
    /// 文案不合规的旧文件头被整体替换，不是叠加
    /// </summary>
    /// <remarks>
    /// 这是最容易写错的一条：如果只做插入不做替换，修完文件顶部会出现两段版权头，
    /// 而且分析器仍然报（因为第一行还是旧文案），用户会陷入越点越糟的循环。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_WhenHeaderWordingDiffers_ReplacesInsteadOfPrepending()
    {
        var code = AnalyzerTestHost.Source(
            "// Copyright (c) 2024-Present Someone Else.",
            "// Licensed under the Apache License.",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "}");

        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        var expected = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "}");

        Assert.Equal(expected, run.FixedText);
        Assert.DoesNotContain("Someone Else", run.FixedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Apache", run.FixedText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 只缺授权行时同样整体替换成两行标准头
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_WhenLicenseLineMissing_RestoresBothLines()
    {
        var code = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            "",
            "namespace Demo;");

        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        var expected = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;");

        Assert.Equal(expected, run.FixedText);
    }

    /// <summary>
    /// 空文件也能修，修完只剩标准文件头
    /// </summary>
    /// <remarks>
    /// 空文件的诊断是零长度区间，修复器走的是「在 0 位置插入」这条路，容易在边界上炸。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_WhenFileIsEmpty_WritesStandardHeaderOnly()
    {
        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            string.Empty,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AnalyzerTestHost.StandardHeader, run.FixedText);
    }

    /// <summary>
    /// 修复后的文本重新过一遍分析器必须零诊断
    /// </summary>
    /// <remarks>
    /// 这是修复器最重要的契约：修一次就要修干净。分开的逐字断言只能保证「这次输出长这样」，
    /// 只有闭环回跑才能保证「分析器认账」，两者的口径万一将来出现分歧，这条会第一时间红。
    /// </remarks>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("namespace Demo;\n")]
    [InlineData("// Copyright (c) 2024-Present Someone Else.\n// Licensed under the Apache License.\n\nnamespace Demo;\n")]
    [InlineData("using System;\n\nnamespace Demo;\n")]
    [InlineData("")]
    public async Task Fix_ThenReanalyze_LeavesNoDiagnostic(string code)
    {
        var filePath = AnalyzerTestHost.FilePath("Sample.cs");

        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            code,
            filePath,
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(run.Actions);

        var afterFix = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            run.FixedText,
            filePath,
            TestContext.Current.CancellationToken);

        Assert.Empty(afterFix);
    }

    /// <summary>
    /// 文件头写在 using 之后时，标准头补到文件最顶端
    /// </summary>
    /// <remarks>
    /// 识别既有文件头的前提是「文本从 // Copyright 开头」，这里不满足，于是走插入分支。
    /// 后面那段位置不对的旧版权注释保持原样——修复器只负责让文件顶部合规，不做全文清理。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_WhenHeaderAppearsAfterUsing_PrependsStandardHeader()
    {
        var code = AnalyzerTestHost.Source(
            "using System;",
            "",
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;");

        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Equal(AnalyzerTestHost.StandardHeader + code, run.FixedText);
        Assert.StartsWith(AnalyzerTestHost.StandardHeader, run.FixedText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 合规文件不产生诊断，也就没有任何修复动作可注册
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_WhenHeaderIsStandard_RegistersNoAction()
    {
        var code = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;");

        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Actions);
        Assert.Equal(code, run.FixedText);
    }

    /// <summary>
    /// 修复动作对外显示的标题固定
    /// </summary>
    /// <remarks>
    /// 标题同时充当 equivalenceKey：批量修复靠它把同类动作归组，改了会让「全部修复」拆成多组。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Fix_ActionTitle_IsStable()
    {
        var run = await AnalyzerTestHost.RunCodeFixAsync(
            new XiHanFileHeaderAnalyzer(),
            new XiHanFileHeaderCodeFixProvider(),
            AnalyzerTestHost.Source("namespace Demo;"),
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        var action = Assert.Single(run.Actions);
        Assert.Equal("添加曦寒标准版权文件头", action.Title);
        Assert.Equal(action.Title, action.EquivalenceKey);
    }
}
