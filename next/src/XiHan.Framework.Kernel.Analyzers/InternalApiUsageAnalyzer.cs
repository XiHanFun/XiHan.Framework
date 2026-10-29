// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// XH0003。
/// </summary>
/// <remarks>
/// 在程序集之外使用标注为 <c>Stability.Internal</c> 的类型或成员。类型按名称出现处报告一次；成员只在自身标注 Internal 时报告，避免同一处重复。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InternalApiUsageAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rules.InternalApiUsage);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            // 编译里没有 ApiLevelAttribute，说明没有引用原语层，也就没有可检查的标注
            var attribute = start.Compilation.GetTypeByMetadataName(Rules.ApiLevelAttributeName);
            if (attribute is null)
            {
                return;
            }

            // 成员使用走操作树（调用、构造、成员引用），类型名出现走语法树；两条路径各报各的，互不重复
            var assembly = start.Compilation.Assembly;
            start.RegisterOperationAction(
                operationContext => AnalyzeOperation(operationContext, attribute, assembly),
                OperationKind.Invocation,
                OperationKind.ObjectCreation,
                OperationKind.PropertyReference,
                OperationKind.FieldReference,
                OperationKind.EventReference,
                OperationKind.MethodReference);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeTypeSyntax(nodeContext, attribute, assembly),
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName);
        });
    }

    /// <summary>
    /// 检查调用、构造与成员引用：目标成员来自其他程序集且自身标注 Internal 时报告。
    /// </summary>
    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol attribute, IAssemblySymbol assembly)
    {
        ISymbol? member = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IMemberReferenceOperation reference => reference.Member,
            _ => null,
        };

        // 程序集内部可以使用自己的 Internal API
        if (member is null || IsSameAssembly(member, assembly))
        {
            return;
        }

        // 以原始定义判定，泛型实例化后的成员仍能命中标注
        var original = member.OriginalDefinition;
        if (Rules.IsInternalStability(original, attribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.InternalApiUsage, context.Operation.Syntax.GetLocation(), original.ToDisplayString(Rules.DisplayFormat)));
        }
    }

    /// <summary>
    /// 检查标识符与泛型名解析到的类型。
    /// </summary>
    /// <remarks>
    /// 类型或其包含类型标注 Internal 且来自其他程序集时报告。
    /// </remarks>
    private static void AnalyzeTypeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol attribute, IAssemblySymbol assembly)
    {
        // var 是隐式类型关键字，不是对某个类型名的引用
        if (context.Node is IdentifierNameSyntax { IsVar: true })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not INamedTypeSymbol type || IsSameAssembly(type, assembly))
        {
            return;
        }

        var original = type.OriginalDefinition;
        if (Rules.IsInternalType(original, attribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.InternalApiUsage, context.Node.GetLocation(), original.ToDisplayString(Rules.DisplayFormat)));
        }
    }

    /// <summary>
    /// 符号是否属于当前编译的程序集。
    /// </summary>
    /// <remarks>
    /// 没有归属程序集的符号（数组、指针等）视为同程序集，不报告。
    /// </remarks>
    private static bool IsSameAssembly(ISymbol symbol, IAssemblySymbol assembly)
        => symbol.ContainingAssembly is null || SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, assembly);
}
