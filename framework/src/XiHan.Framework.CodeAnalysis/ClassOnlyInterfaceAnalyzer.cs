#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ClassOnlyInterfaceAnalyzer
// Guid:3c2a0f6c-ea86-4409-bc4a-5ced12239bd3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/18 20:24:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace XiHan.Framework.CodeAnalysis;

/// <summary>
/// 只能被类实现的接口特性
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ClassOnlyInterfaceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "CUSTOM001",
        title: "接口继承限制",
        messageFormat: "接口 {0} 只能被类实现，不能被其他接口继承",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// 支持的诊断描述符
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InterfaceDeclaration);
    }

    /// <summary>
    /// 分析节点
    /// </summary>
    /// <param name="context"></param>
    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;

        foreach (var baseType in interfaceDeclaration.BaseList?.Types ?? default)
        {
            if (context.SemanticModel.GetSymbolInfo(baseType.Type).Symbol is INamedTypeSymbol symbol &&
                symbol.TypeKind == TypeKind.Interface &&
                symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "ClassOnlyInterfaceAttribute"))
            {
                var diagnostic = Diagnostic.Create(Rule, baseType.GetLocation(), symbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
