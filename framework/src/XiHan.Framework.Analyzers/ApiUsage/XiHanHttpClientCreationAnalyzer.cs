#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpClientCreationAnalyzer
// Guid:2e5f8b3c-6d9a-4b0e-9f2a-4c7d0e3f6a9b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/14 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Analyzers.ApiUsage;

/// <summary>
/// 检查直接 new HttpClient 用法的 Roslyn 分析器（XHFA001）。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XiHanHttpClientCreationAnalyzer : DiagnosticAnalyzer
{
    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [XiHanApiUsageRule.DirectHttpClientCreation];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        // 显式 new HttpClient(...) 与目标类型推断的 new(...) 都要覆盖
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var node = (ObjectCreationExpressionSyntax)context.Node;
        ReportIfHttpClient(context, node, node.GetLocation());
    }

    private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var node = (ImplicitObjectCreationExpressionSyntax)context.Node;
        ReportIfHttpClient(context, node, node.GetLocation());
    }

    private static void ReportIfHttpClient(SyntaxNodeAnalysisContext context, SyntaxNode node, Location location)
    {
        var createdType = context.SemanticModel.GetTypeInfo(node, context.CancellationToken).Type;
        if (createdType is null)
        {
            return;
        }

        if (createdType.ToDisplayString() == HttpClientMetadataName)
        {
            context.ReportDiagnostic(Diagnostic.Create(XiHanApiUsageRule.DirectHttpClientCreation, location));
        }
    }
}
