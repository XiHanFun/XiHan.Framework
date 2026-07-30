// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace XiHan.Framework.Analyzers.FileHeaders;

/// <summary>
/// 检查 C# 文件是否包含曦寒标准版权文件头的 Roslyn 分析器。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XiHanFileHeaderAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [XiHanFileHeaderRule.Descriptor];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        var sourceText = context.Tree.GetText(context.CancellationToken);
        if (XiHanFileHeader.ShouldSkip(context.Tree.FilePath, sourceText))
        {
            return;
        }

        if (XiHanFileHeader.IsValid(sourceText))
        {
            return;
        }

        var diagnosticSpan = new TextSpan(0, sourceText.Length > 0 ? 1 : 0);
        var location = Location.Create(context.Tree, diagnosticSpan);
        var fileName = XiHanFileHeader.GetFileName(context.Tree.FilePath);
        context.ReportDiagnostic(Diagnostic.Create(XiHanFileHeaderRule.Descriptor, location, fileName));
    }
}
