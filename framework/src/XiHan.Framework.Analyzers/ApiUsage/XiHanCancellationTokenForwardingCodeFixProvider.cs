// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;

namespace XiHan.Framework.Analyzers.ApiUsage;

/// <summary>
/// 为 XHFA002 提供「转发取消令牌」修复：在调用末尾追加命名实参
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XiHanCancellationTokenForwardingCodeFixProvider))]
[Shared]
public sealed class XiHanCancellationTokenForwardingCodeFixProvider : CodeFixProvider
{
    private const string Title = "转发取消令牌";

    /// <summary>
    /// 本修复器可处理的诊断编号，即取消令牌未转发规则的诊断编号
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [XiHanApiUsageRule.CancellationTokenNotForwardedId];

    /// <summary>
    /// 取得批量修复提供器，支持一次性修复文档、项目或解决方案范围内的全部同类诊断
    /// </summary>
    /// <returns>内置的批量修复提供器</returns>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    /// 为诊断注册「转发取消令牌」的代码修复动作
    /// </summary>
    /// <param name="context">代码修复上下文</param>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(XiHanCancellationTokenForwardingAnalyzer.ParameterNameKey, out var parameterName)
            || !diagnostic.Properties.TryGetValue(XiHanCancellationTokenForwardingAnalyzer.TokenNameKey, out var tokenName)
            || string.IsNullOrEmpty(parameterName)
            || string.IsNullOrEmpty(tokenName))
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?
            .FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (root is null || invocation is null || invocation.Span != diagnostic.Location.SourceSpan)
        {
            return;
        }

        var document = context.Document;
        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(invocation, AppendArgument(invocation, parameterName!, tokenName!)))),
                equivalenceKey: Title),
            diagnostic);
    }

    private static InvocationExpressionSyntax AppendArgument(
        InvocationExpressionSyntax invocation,
        string parameterName,
        string tokenName)
    {
        var argument = SyntaxFactory.Argument(
            SyntaxFactory.NameColon(
                Identifier(parameterName),
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)),
            default,
            Identifier(tokenName));

        var arguments = invocation.ArgumentList.Arguments;
        var separators = arguments.GetSeparators().ToList();
        var nodes = new List<ArgumentSyntax>(arguments);
        if (arguments.Count > 0)
        {
            var last = arguments[arguments.Count - 1];
            nodes[nodes.Count - 1] = last.WithoutTrailingTrivia();
            argument = argument.WithTrailingTrivia(last.GetTrailingTrivia());
            separators.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space));
        }

        nodes.Add(argument);
        var newArguments = SyntaxFactory.SeparatedList(nodes, separators);
        return invocation.WithArgumentList(invocation.ArgumentList.WithArguments(newArguments));
    }

    private static IdentifierNameSyntax Identifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
            ? SyntaxFactory.IdentifierName(name)
            : SyntaxFactory.IdentifierName(SyntaxFactory.VerbatimIdentifier(default, name, name, default));
    }
}
