// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Composition;

namespace XiHan.Framework.Analyzers.FileHeaders;

/// <summary>
/// 为缺失或不合规的曦寒标准版权文件头提供一键修复。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XiHanFileHeaderCodeFixProvider))]
[Shared]
public sealed class XiHanFileHeaderCodeFixProvider : CodeFixProvider
{
    private const string Title = "添加曦寒标准版权文件头";

    /// <summary>
    /// 本修复器可处理的诊断编号，即文件头规则的诊断编号
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [XiHanFileHeaderRule.DiagnosticId];

    /// <summary>
    /// 取得批量修复提供器，支持一次性修复文档、项目或解决方案范围内的全部同类诊断
    /// </summary>
    /// <returns>内置的批量修复提供器</returns>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    /// 为诊断注册「添加曦寒标准版权文件头」的代码修复动作
    /// </summary>
    /// <param name="context">代码修复上下文</param>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var document = context.Document;

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => AddOrReplaceHeaderAsync(document, cancellationToken),
                equivalenceKey: Title),
            diagnostic);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task<Document> AddOrReplaceHeaderAsync(Document document, CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var headerText = XiHanFileHeader.CreateHeader();

        var newSourceText = XiHanFileHeader.TryGetHeaderSpan(sourceText, out var headerSpan)
            ? sourceText.WithChanges(new TextChange(headerSpan, headerText))
            : sourceText.WithChanges(new TextChange(new TextSpan(0, 0), headerText));
        return document.WithText(newSourceText);
    }
}
