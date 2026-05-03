#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileHeaderAnalyzer
// Guid:3fbf9a7d-70d6-4636-a3e7-06e687a77e0d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/26 17:33:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace XiHan.Framework.Analyzers.FileHeaders;

/// <summary>
/// 检查 C# 文件是否包含曦寒标准版权版本注释的 Roslyn 分析器。
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

        if (XiHanFileHeader.IsValid(sourceText, context.Tree.FilePath))
        {
            return;
        }

        var diagnosticSpan = new TextSpan(0, sourceText.Length > 0 ? 1 : 0);
        var location = Location.Create(context.Tree, diagnosticSpan);
        var fileName = XiHanFileHeader.GetFileName(context.Tree.FilePath);
        context.ReportDiagnostic(Diagnostic.Create(XiHanFileHeaderRule.Descriptor, location, fileName));
    }
}
