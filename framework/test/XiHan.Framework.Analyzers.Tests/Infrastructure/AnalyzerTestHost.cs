// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace XiHan.Framework.Analyzers.Tests;

/// <summary>
/// Roslyn 分析器与代码修复的内存驱动器
/// </summary>
/// <remarks>
/// 被测项目是 netstandard2.0 的分析器程序集，它的公共契约不是「属性默认值」而是「在真实编译上报出什么诊断」，
/// 因此必须现场构造 <see cref="CSharpCompilation"/> 把分析器跑起来，才能验证诊断编号、级别与位置。
/// 这里统一封装编译构造、诊断收集与代码修复应用三件事。
/// 所有源码片段的换行一律用 LF：被测的文件头判定会把 CRLF 归一化后再比对，
/// 而代码修复写回的文件头是硬编码 LF 的，只有固定行尾才能对修复结果做逐字断言。
/// 用例只用到「路径字符串」参与判定（扩展名、obj/bin 目录、生成代码后缀），全程不落盘，也就不需要临时目录清理。
/// </remarks>
internal static class AnalyzerTestHost
{
    /// <summary>
    /// 曦寒标准文件头第一行
    /// </summary>
    internal const string CopyrightLine = "// Copyright (c) 2021-Present XiHanFun and contributors.";

    /// <summary>
    /// 曦寒标准文件头第二行
    /// </summary>
    internal const string LicenseLine = "// Licensed under the MIT License. See LICENSE in the project root for license information.";

    /// <summary>
    /// 代码修复写回的标准文件头：两行声明 + 一个空行，行尾固定 LF
    /// </summary>
    internal const string StandardHeader = CopyrightLine + "\n" + LicenseLine + "\n" + "\n";

    /// <summary>
    /// 虚拟源码根目录，仅用于拼路径字符串，不会真的创建目录
    /// </summary>
    internal static readonly string RootDirectory = $"{Path.DirectorySeparatorChar}XiHanTests{Path.DirectorySeparatorChar}Analyzers";

    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyReferences = new(BuildReferences);

    /// <summary>
    /// 编译所用的元数据引用集合
    /// </summary>
    /// <remarks>
    /// 取当前运行时的受信任程序集列表，保证语义模型能把 new HttpClient() 解析成 System.Net.Http.HttpClient；
    /// 引用不全时符号会退化成错误类型，导致 API 用法规则的用例出现假阴性。
    /// </remarks>
    internal static ImmutableArray<MetadataReference> References => LazyReferences.Value;

    /// <summary>
    /// 按 LF 拼接源码行，并补一个行尾换行
    /// </summary>
    /// <param name="lines">源码行</param>
    /// <returns>拼接后的源码文本</returns>
    internal static string Source(params string[] lines)
    {
        return lines.Length == 0 ? string.Empty : string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// 在虚拟根目录下拼出文件路径
    /// </summary>
    /// <param name="segments">路径片段</param>
    /// <returns>使用当前平台分隔符的完整路径</returns>
    internal static string FilePath(params string[] segments)
    {
        var parts = new string[segments.Length + 1];
        parts[0] = RootDirectory;
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return Path.Combine(parts);
    }

    /// <summary>
    /// 构造只含一个源文件的编译
    /// </summary>
    /// <param name="code">源码文本</param>
    /// <param name="filePath">源文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编译对象</returns>
    internal static CSharpCompilation CreateCompilation(string code, string filePath, CancellationToken cancellationToken)
    {
        return CreateCompilation([(code, filePath)], cancellationToken);
    }

    /// <summary>
    /// 构造含多个源文件的编译
    /// </summary>
    /// <param name="sources">源码与路径的组合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编译对象</returns>
    internal static CSharpCompilation CreateCompilation(IReadOnlyList<(string Code, string FilePath)> sources, CancellationToken cancellationToken)
    {
        var trees = new SyntaxTree[sources.Count];
        for (var index = 0; index < sources.Count; index++)
        {
            var sourceText = SourceText.From(sources[index].Code, Encoding.UTF8, SourceHashAlgorithm.Sha256);
            trees[index] = CSharpSyntaxTree.ParseText(sourceText, CSharpParseOptions.Default, sources[index].FilePath, cancellationToken);
        }

        return CSharpCompilation.Create(
            "XiHan.Framework.Analyzers.TestCompilation",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// 在单文件编译上运行分析器
    /// </summary>
    /// <param name="analyzer">被测分析器</param>
    /// <param name="code">源码文本</param>
    /// <param name="filePath">源文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按起始位置升序排列的诊断</returns>
    internal static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        DiagnosticAnalyzer analyzer,
        string code,
        string filePath,
        CancellationToken cancellationToken)
    {
        var compilation = CreateCompilation(code, filePath, cancellationToken);
        return await RunAnalyzerAsync(analyzer, compilation, cancellationToken);
    }

    /// <summary>
    /// 在已有编译上运行分析器
    /// </summary>
    /// <param name="analyzer">被测分析器</param>
    /// <param name="compilation">编译对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按起始位置升序排列的诊断</returns>
    internal static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        DiagnosticAnalyzer analyzer,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer),
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty));

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);

        // AD0001 是「分析器自身抛异常」的兜底诊断，必须显式炸出来，
        // 否则崩溃会被后面的 Assert.Empty 当成「规则没命中」而静默通过。
        var crashed = diagnostics.Where(item => item.Id == "AD0001").ToArray();
        if (crashed.Length > 0)
        {
            Assert.Fail("分析器执行期间抛出异常：" + string.Join(" | ", crashed.Select(item => item.GetMessage(CultureInfo.InvariantCulture))));
        }

        return [.. diagnostics.OrderBy(item => item.Location.SourceSpan.Start)];
    }

    /// <summary>
    /// 跑完整链路：分析器出诊断，修复器注册动作，再把第一个动作应用到文档
    /// </summary>
    /// <param name="analyzer">被测分析器</param>
    /// <param name="provider">被测代码修复器</param>
    /// <param name="code">源码文本</param>
    /// <param name="filePath">源文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>诊断、注册到的动作与修复后的文本</returns>
    internal static async Task<CodeFixRun> RunCodeFixAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string code,
        string filePath,
        CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();

        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var documentName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(documentName))
        {
            documentName = "Unnamed.cs";
        }

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "XiHanAnalyzerTests", "XiHanAnalyzerTests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(projectId, References)
            .AddDocument(
                documentId,
                documentName,
                SourceText.From(code, Encoding.UTF8, SourceHashAlgorithm.Sha256),
                Array.Empty<string>(),
                filePath,
                false);

        var applied = false;
        try
        {
            applied = workspace.TryApplyChanges(solution);
        }
        catch (Exception)
        {
            // AdhocWorkspace 正常支持全部变更类型；万一宿主环境拒绝，
            // 退回未落工作区的解决方案快照即可，修复链路只依赖 Document 文本本身。
            applied = false;
        }

        var effectiveSolution = applied ? workspace.CurrentSolution : solution;
        var document = effectiveSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("测试文档未能加入解决方案。");

        var compilation = await document.Project.GetCompilationAsync(cancellationToken)
            ?? throw new InvalidOperationException("测试项目未能生成编译对象。");

        var diagnostics = await RunAnalyzerAsync(analyzer, compilation, cancellationToken);

        var actions = new List<CodeAction>();
        foreach (var diagnostic in diagnostics)
        {
            if (!provider.FixableDiagnosticIds.Contains(diagnostic.Id))
            {
                continue;
            }

            var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
            await provider.RegisterCodeFixesAsync(context);
        }

        if (actions.Count == 0)
        {
            return new CodeFixRun(diagnostics, [], code);
        }

        var operations = await actions[0].GetOperationsAsync(cancellationToken);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("修复后的解决方案中找不到目标文档。");
        var fixedText = await fixedDocument.GetTextAsync(cancellationToken);

        return new CodeFixRun(diagnostics, [.. actions], fixedText.ToString());
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted && trusted.Length > 0)
        {
            foreach (var candidate in trusted.Split(Path.PathSeparator))
            {
                TryAdd(candidate);
            }
        }

        // 兜底：拿不到受信任程序集列表时，至少保证 object 与 HttpClient 所在程序集可用
        TryAdd(typeof(object).Assembly.Location);
        TryAdd(typeof(HttpClient).Assembly.Location);

        return builder.ToImmutable();

        void TryAdd(string candidate)
        {
            if (string.IsNullOrEmpty(candidate) || !candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!File.Exists(candidate) || !seen.Add(Path.GetFileName(candidate)))
            {
                return;
            }

            try
            {
                builder.Add(MetadataReference.CreateFromFile(candidate));
            }
            catch (Exception)
            {
                // 名单里混进非托管文件或损坏程序集时跳过，不影响其余引用
            }
        }
    }
}

/// <summary>
/// 一次代码修复运行的结果
/// </summary>
/// <param name="Diagnostics">分析器产出的诊断</param>
/// <param name="Actions">修复器注册的代码动作</param>
/// <param name="FixedText">应用第一个动作后的文档文本；没有可用动作时为原文</param>
internal sealed record CodeFixRun(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<CodeAction> Actions,
    string FixedText);
