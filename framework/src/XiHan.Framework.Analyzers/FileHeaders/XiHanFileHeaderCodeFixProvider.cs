#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileHeaderCodeFixProvider
// Guid:65235b01-ee67-4817-a613-c4951d4f2f85
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/26 17:33:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace XiHan.Framework.Analyzers.FileHeaders;

/// <summary>
/// 为缺失或不合规的曦寒标准版权版本注释提供一键修复。
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
        var fileName = GetDocumentFileName(document);
        var metadata = XiHanFileHeader.ReadExistingMetadata(sourceText);
        var guid = metadata.Guid ?? Guid.NewGuid();
        var createTime = metadata.CreateTime ?? DateTime.Now;
        var headerText = XiHanFileHeader.CreateHeader(fileName, guid, createTime);

        var newSourceText = XiHanFileHeader.TryGetHeaderSpan(sourceText, out var headerSpan)
            ? sourceText.WithChanges(new TextChange(headerSpan, headerText))
            : sourceText.WithChanges(new TextChange(new TextSpan(0, 0), headerText));
        return document.WithText(newSourceText);
    }

    private static string GetDocumentFileName(Document document)
    {
        var filePath = document.FilePath;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        return Path.GetFileNameWithoutExtension(document.Name);
    }
}
