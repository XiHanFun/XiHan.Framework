// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;
using XiHan.Framework.Analyzers.ApiUsage;

namespace XiHan.Framework.Analyzers.Tests.ApiUsage;

/// <summary>
/// 直接创建 HttpClient 分析器（XHFA001）测试
/// </summary>
/// <remarks>
/// 这条规则靠语义模型判定，不是靠语法文本匹配，所以用例必须跑在带完整元数据引用的真实编译上，
/// 否则 HttpClient 会退化成错误类型、规则全线假阴性——测试宿主已用受信任程序集列表兜住这一点。
/// 判定口径是「构造出来的类型全名恰好等于 System.Net.Http.HttpClient」，因此三件事都要锁：
/// 显式 new 与目标类型推断的 new() 都要命中；HttpClientHandler 这类近似名字不能误伤；
/// 级别是 Info 而非 Warning——框架 HTTP 模块内部有意直建，级别一旦被提到 Warning 就会把构建刷屏。
/// </remarks>
public class XiHanHttpClientCreationAnalyzerTests
{
    /// <summary>
    /// 单个用例的超时上限，防止分析器意外死循环把 CI 挂住
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    private const string DiagnosticId = "XHFA001";

    /// <summary>
    /// 分析器只暴露一个描述符，且编号、类别、级别、开关状态逐字不漂移
    /// </summary>
    /// <remarks>
    /// 这些值会被 .editorconfig 抑制配置与 IDE 规则列表依赖，属于对外协议而不是实现细节。
    /// 级别单独强调：Info 是刻意选的，改成 Warning 会让框架内部合法的直建用法变成噪音。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void SupportedDiagnostics_ExposesSingleHttpClientDescriptor()
    {
        var analyzer = new XiHanHttpClientCreationAnalyzer();

        var descriptor = Assert.Single(analyzer.SupportedDiagnostics);
        Assert.Equal(DiagnosticId, descriptor.Id);
        Assert.Equal("XiHan.ApiUsage", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
    }

    /// <summary>
    /// 分析器只登记 C# 语言
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void DiagnosticAnalyzerAttribute_DeclaresCSharpOnly()
    {
        var attribute = typeof(XiHanHttpClientCreationAnalyzer).GetCustomAttribute<DiagnosticAnalyzerAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(new[] { LanguageNames.CSharp }, attribute!.Languages);
    }

    /// <summary>
    /// 显式 new HttpClient() 命中，且诊断区间正好覆盖整个创建表达式
    /// </summary>
    /// <remarks>
    /// 区间要断言：IDE 的波浪线与「快速操作」定位都依赖它，只报到标识符或整行都会让体验退化。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenClientCreatedDirectly_ReportsOverWholeExpression()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public HttpClient Create() => new HttpClient();",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        var span = diagnostic.Location.SourceSpan;
        Assert.Equal("new HttpClient()", code.Substring(span.Start, span.Length));
    }

    /// <summary>
    /// 带构造参数的 new HttpClient(handler) 同样命中
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenClientCreatedWithHandler_ReportsDiagnostic()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public HttpClient Create() => new HttpClient(new HttpClientHandler());",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
        Assert.StartsWith("new HttpClient(", code[diagnostic.Location.SourceSpan.Start..], StringComparison.Ordinal);
    }

    /// <summary>
    /// 目标类型推断的 new() 也命中
    /// </summary>
    /// <remarks>
    /// 这是最容易漏的写法：语法节点是 ImplicitObjectCreationExpression，文本里根本没有 HttpClient 字样，
    /// 纯文本匹配的规则一定漏报，只有走语义模型才拦得住。分析器为此单独注册了一条动作，必须有用例守着。
    /// </remarks>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("        HttpClient client = new();")]
    [InlineData("        HttpClient client = new(new HttpClientHandler());")]
    public async Task Analyze_WhenClientCreatedWithTargetTypedNew_ReportsDiagnostic(string creationLine)
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public void Run()",
            "    {",
            creationLine,
            "        _ = client;",
            "    }",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
        Assert.StartsWith("new(", code[diagnostic.Location.SourceSpan.Start..], StringComparison.Ordinal);
    }

    /// <summary>
    /// 名字相近但不是 HttpClient 的类型不误伤
    /// </summary>
    /// <remarks>
    /// HttpClientHandler / HttpRequestMessage 都以 Http 开头，正是纯文本匹配最容易误报的地方。
    /// </remarks>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData("new HttpClientHandler()")]
    [InlineData("new HttpRequestMessage()")]
    [InlineData("new SocketsHttpHandler()")]
    public async Task Analyze_WhenOtherHttpTypeCreated_ReportsNothing(string creation)
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public object Create() => " + creation + ";",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 经 IHttpClientFactory 获取实例不报诊断
    /// </summary>
    /// <remarks>
    /// 这正是规则想引导到的正确写法，一旦这条误报，整条规则就没法开在仓库里。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenClientComesFromFactory_ReportsNothing()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    private readonly IHttpClientFactory _factory;",
            "",
            "    public Sample(IHttpClientFactory factory) => _factory = factory;",
            "",
            "    public HttpClient Create() => _factory.CreateClient(\"demo\");",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 同一文件里多处直建逐处报，位置各自独立
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenMultipleCreations_ReportsEachOccurrence()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public HttpClient First() => new HttpClient();",
            "",
            "    public HttpClient Second() => new HttpClient();",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, item => Assert.Equal(DiagnosticId, item.Id));
        Assert.True(
            diagnostics[0].Location.SourceSpan.Start < diagnostics[1].Location.SourceSpan.Start,
            "测试宿主按起始位置升序返回诊断，两处命中应当位置不同且有序。");
    }

    /// <summary>
    /// 没有任何创建表达式时不报
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenNoObjectCreation_ReportsNothing()
    {
        var code = AnalyzerTestHost.Source(
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public int Value => 1;",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 生成代码里的直建同样上报
    /// </summary>
    /// <remarks>
    /// 这条与文件头规则的取舍相反，是刻意的：文件头规则显式跳过 *.g.cs，
    /// 而本规则在 Initialize 里配了 GeneratedCodeAnalysisFlags.Analyze | ReportDiagnostics，
    /// 因为代码生成器产出的直建同样会耗尽 socket，不该被豁免。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenFileLooksGenerated_StillReports()
    {
        var code = AnalyzerTestHost.Source(
            "// <auto-generated/>",
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class Sample",
            "{",
            "    public HttpClient Create() => new HttpClient();",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.g.cs"),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// 创建 HttpClient 派生类不命中
    /// </summary>
    /// <remarks>
    /// 判定用的是类型全名严格相等，派生类因此落在规则之外。这里把现状钉住，
    /// 是为了让后续若决定改成「含派生类」时，这条用例能立刻红给维护者看，而不是悄悄改变语义。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Analyze_WhenDerivedClientCreated_ReportsNothing()
    {
        var code = AnalyzerTestHost.Source(
            "using System.Net.Http;",
            "",
            "namespace Demo;",
            "",
            "public class TracingHttpClient : HttpClient",
            "{",
            "}",
            "",
            "public class Sample",
            "{",
            "    public HttpClient Create() => new TracingHttpClient();",
            "}");

        var diagnostics = await AnalyzerTestHost.RunAnalyzerAsync(
            new XiHanHttpClientCreationAnalyzer(),
            code,
            AnalyzerTestHost.FilePath("Sample.cs"),
            TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }
}
