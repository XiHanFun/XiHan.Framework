// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Globalization;
using System.Reflection;
using XiHan.Framework.Analyzers.FileHeaders;
using XiHan.Framework.Analyzers.Tests.Infrastructure;

namespace XiHan.Framework.Analyzers.Tests.FileHeaders;

/// <summary>
/// 曦寒标准版权文件头分析器（XHFH001）测试
/// </summary>
/// <remarks>
/// 这条规则的契约有三层，缺一层都会在下游出事：
/// 一是诊断编号与类别——framework/.editorconfig 里写死了 dotnet_diagnostic.XHFH001.severity，编号一改抑制配置就静默失效；
/// 二是「什么算合规文件头」——两行文字逐字匹配，只对 BOM 与 CRLF 做归一化，其余任何差异都必须命中；
/// 三是跳过范围——obj/bin 产物、*.g.cs 一类生成代码、非 .cs 文件绝不能报，否则会把构建刷屏。
/// 诊断位置固定在文件首字符（空文件则是零长度区间），修复器依赖这个位置，所以位置也要断言。
/// </remarks>
public class XiHanFileHeaderAnalyzerTests
{
    /// <summary>
    /// 单个用例的超时上限，防止分析器意外死循环把 CI 挂住
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    private const string DiagnosticId = "XHFH001";

    /// <summary>
    /// 分析器只暴露一个描述符，且编号、类别、级别、文案逐字不漂移
    /// </summary>
    /// <remarks>
    /// 这些值会被 .editorconfig、IDE 抑制列表和 CI 日志正则依赖，属于对外协议而不是实现细节。
    /// </remarks>
    [Fact]
    public void SupportedDiagnostics_ExposesSingleFileHeaderDescriptor()
    {
        var analyzer = new XiHanFileHeaderAnalyzer();

        var descriptor = Assert.Single(analyzer.SupportedDiagnostics);

        Assert.Equal(DiagnosticId, descriptor.Id);
        Assert.Equal("XiHan.FileHeader", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.Equal("缺少曦寒标准版权文件头", descriptor.Title.ToString());
        Assert.Equal("文件 '{0}' 缺少或未正确声明曦寒标准版权文件头", descriptor.MessageFormat.ToString());
        Assert.Equal("C# 源文件必须以标准的两行版权与 MIT 授权声明开头.", descriptor.Description.ToString());
    }

    /// <summary>
    /// 分析器只注册到 C# 语言
    /// </summary>
    [Fact]
    public void DiagnosticAnalyzerAttribute_DeclaresCSharpOnly()
    {
        var attribute = typeof(XiHanFileHeaderAnalyzer).GetCustomAttribute<DiagnosticAnalyzerAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(new[] { LanguageNames.CSharp }, attribute!.Languages);
    }

    /// <summary>
    /// 文件以标准两行文件头开头时不报诊断
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenHeaderIsStandard_ReportsNothing()
    {
        var code = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 文件头用 CRLF 行尾同样算合规
    /// </summary>
    /// <remarks>
    /// Windows 检出的仓库大量是 CRLF，判定前会做行尾归一化；这条一旦回归，整个 Windows 侧全量刷警告。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenHeaderUsesCrLf_ReportsNothing()
    {
        var code = AnalyzerTestHost.CopyrightLine + "\r\n"
            + AnalyzerTestHost.LicenseLine + "\r\n"
            + "\r\n"
            + "namespace Demo;\r\n";

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("CrLfSample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 文本以 BOM 字符开头时先剥离再判定
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenTextStartsWithByteOrderMark_ReportsNothing()
    {
        var code = "﻿" + AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("BomSample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 整个文件只有标准文件头也算合规
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenFileOnlyContainsStandardHeader_ReportsNothing()
    {
        var code = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine);

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("HeaderOnly.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 完全没有文件头时在文件首字符报 XHFH001
    /// </summary>
    /// <remarks>
    /// 位置固定为 [0,1)：代码修复要靠这个位置把文件头插到最顶部，位置漂了修复就会插错地方。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenHeaderMissing_ReportsAtFirstCharacter()
    {
        var filePath = AnalyzerTestHost.FilePath("MissingHeader.cs");
        var code = AnalyzerTestHost.Source(
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            filePath,
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(0, diagnostic.Location.SourceSpan.Start);
        Assert.Equal(1, diagnostic.Location.SourceSpan.Length);
        Assert.Equal(filePath, diagnostic.Location.GetLineSpan().Path);
    }

    /// <summary>
    /// 诊断消息里带的是不含扩展名的文件名
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenHeaderMissing_MessageCarriesFileNameWithoutExtension()
    {
        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            AnalyzerTestHost.Source("namespace Demo;"),
            AnalyzerTestHost.FilePath("Nested", "OrderAppService.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(
            "文件 'OrderAppService' 缺少或未正确声明曦寒标准版权文件头",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 文件头文字与标准文案有任何差异都要命中
    /// </summary>
    /// <remarks>
    /// 逐字匹配是刻意的：大小写、年份、标点、缺行、少一个空格都算不合规，
    /// 否则「大致像版权头」的文件会混过去，仓库里就会长出好几种文件头。
    /// </remarks>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("// Copyright (c) 2021-Present XiHanFun and Contributors.", "// Licensed under the MIT License. See LICENSE in the project root for license information.")]
    [InlineData("// Copyright (c) 2021-Present XiHanFun and contributors", "// Licensed under the MIT License. See LICENSE in the project root for license information.")]
    [InlineData("// Copyright (c) 2024-Present XiHanFun and contributors.", "// Licensed under the MIT License. See LICENSE in the project root for license information.")]
    [InlineData("//Copyright (c) 2021-Present XiHanFun and contributors.", "// Licensed under the MIT License. See LICENSE in the project root for license information.")]
    [InlineData("// Copyright (c) 2021-Present XiHanFun and contributors.", "// Licensed under the MIT license. See LICENSE in the project root for license information.")]
    [InlineData("// Copyright (c) 2021-Present XiHanFun and contributors.", "// Licensed under the MIT License.")]
    public async Task Analyze_WhenHeaderWordingDiffers_ReportsDiagnostic(string firstLine, string secondLine)
    {
        var code = AnalyzerTestHost.Source(firstLine, secondLine, "", "namespace Demo;");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("WrongHeader.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// 只有版权行、缺授权行时要命中
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenLicenseLineMissing_ReportsDiagnostic()
    {
        var code = AnalyzerTestHost.Source(
            AnalyzerTestHost.CopyrightLine,
            "",
            "namespace Demo;");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("HalfHeader.cs"),
            TestContext.Current.CancellationToken);

        Assert.Single(diagnostics);
    }

    /// <summary>
    /// 文件头写在 using 之后时要命中
    /// </summary>
    /// <remarks>
    /// 规则要求的是「文件以文件头开头」，位置不对等同于没有；这是复制粘贴时最常见的错法。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenHeaderAppearsAfterUsing_ReportsDiagnostic()
    {
        var code = AnalyzerTestHost.Source(
            "using System;",
            "",
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("HeaderAfterUsing.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
        Assert.Equal(0, diagnostic.Location.SourceSpan.Start);
    }

    /// <summary>
    /// 文件头之前多一个空行也要命中
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenBlankLinePrecedesHeader_ReportsDiagnostic()
    {
        var code = AnalyzerTestHost.Source(
            "",
            AnalyzerTestHost.CopyrightLine,
            AnalyzerTestHost.LicenseLine,
            "",
            "namespace Demo;");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("LeadingBlankLine.cs"),
            TestContext.Current.CancellationToken);

        Assert.Single(diagnostics);
    }

    /// <summary>
    /// 空文件也要命中，且诊断区间长度为零
    /// </summary>
    /// <remarks>
    /// 空文件是跳过判定里唯一显式「不跳过」的分支：长度为 0 时直接落到合规性判定，
    /// 位置退化成零长度区间，代码修复据此把文件头插到偏移 0。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenFileIsEmpty_ReportsWithZeroLengthSpan()
    {
        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            string.Empty,
            AnalyzerTestHost.FilePath("Empty.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
        Assert.Equal(0, diagnostic.Location.SourceSpan.Start);
        Assert.Equal(0, diagnostic.Location.SourceSpan.Length);
    }

    /// <summary>
    /// 只有普通注释、没有版权声明的文件要命中
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenFileOnlyContainsUnrelatedComment_ReportsDiagnostic()
    {
        var code = AnalyzerTestHost.Source(
            "// 这是一段与版权无关的说明注释",
            "// 第二行说明");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("CommentOnly.cs"),
            TestContext.Current.CancellationToken);

        Assert.Single(diagnostics);
    }

    /// <summary>
    /// 落在 obj/bin 目录下的文件一律跳过
    /// </summary>
    /// <remarks>
    /// 目录判定大小写不敏感，Linux 上的 obj 与 Windows 上偶发的 Obj 都要覆盖到。
    /// </remarks>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("obj")]
    [InlineData("bin")]
    [InlineData("Obj")]
    [InlineData("BIN")]
    public async Task Analyze_WhenFileUnderBuildOutputDirectory_ReportsNothing(string directoryName)
    {
        var filePath = AnalyzerTestHost.FilePath(directoryName, "Debug", "net10.0", "Generated.cs");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            AnalyzerTestHost.Source("namespace Demo;"),
            filePath,
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 生成代码后缀的文件一律跳过
    /// </summary>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("Sample.g.cs")]
    [InlineData("Sample.generated.cs")]
    [InlineData("Sample.designer.cs")]
    [InlineData("Sample.Designer.cs")]
    public async Task Analyze_WhenFileNameLooksGenerated_ReportsNothing(string fileName)
    {
        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            AnalyzerTestHost.Source("namespace Demo;"),
            AnalyzerTestHost.FilePath(fileName),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 带 auto-generated 标记的文件一律跳过
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenFileMarkedAutoGenerated_ReportsNothing()
    {
        var code = AnalyzerTestHost.Source(
            "// <auto-generated/>",
            "namespace Demo;");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("AutoGenerated.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 扩展名不是 .cs 的文件一律跳过
    /// </summary>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("Sample.txt")]
    [InlineData("Sample.vb")]
    [InlineData("Sample.cshtml")]
    [InlineData("Sample")]
    public async Task Analyze_WhenFileExtensionIsNotCSharp_ReportsNothing(string fileName)
    {
        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            AnalyzerTestHost.Source("namespace Demo;"),
            AnalyzerTestHost.FilePath(fileName),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 语法树没有文件路径时跳过
    /// </summary>
    /// <remarks>
    /// 内存生成的语法树（脚本、源生成器中间产物）路径为空，没有落盘对象可修，报了也没法处理。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenFilePathIsEmpty_ReportsNothing()
    {
        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            AnalyzerTestHost.Source("namespace Demo;"),
            string.Empty,
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 多文件编译时只对不合规的文件报诊断
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenCompilationHasManyFiles_ReportsOnlyOffendingFile()
    {
        var compliantPath = AnalyzerTestHost.FilePath("Compliant.cs");
        var offendingPath = AnalyzerTestHost.FilePath("Offending.cs");

        var compilation = AnalyzerTestHost.CreateCompilation(
            [
                (AnalyzerTestHost.Source(
                    AnalyzerTestHost.CopyrightLine,
                    AnalyzerTestHost.LicenseLine,
                    "",
                    "namespace Demo;",
                    "",
                    "public class Compliant",
                    "{",
                    "}"), compliantPath),
                (AnalyzerTestHost.Source(
                    "namespace Demo;",
                    "",
                    "public class Offending",
                    "{",
                    "}"), offendingPath)
            ],
            TestContext.Current.CancellationToken);

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanFileHeaderAnalyzer(),
            compilation,
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(offendingPath, diagnostic.Location.GetLineSpan().Path);
    }
}
