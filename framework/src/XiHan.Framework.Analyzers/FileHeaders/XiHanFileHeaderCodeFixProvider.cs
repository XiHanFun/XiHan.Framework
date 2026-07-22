// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace XiHan.Framework.Analyzers.FileHeaders;

/// <summary>
/// 为缺失或不合规的曦寒标准版权文件头提供一键修复。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XiHanFileHeaderCodeFixProvider))]
[Shared]
public sealed class XiHanFileHeaderCodeFixProvider : CodeFixProvider
{
    private const string Title = "添加曦寒标准版权文件头";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [XiHanFileHeaderRule.DiagnosticId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
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
