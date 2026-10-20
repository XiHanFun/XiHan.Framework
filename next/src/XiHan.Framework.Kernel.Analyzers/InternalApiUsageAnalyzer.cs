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
/// XH0003：在程序集之外使用标注为 <c>Stability.Internal</c> 的类型或成员。
/// 类型按名称出现处报告一次；成员只在自身标注 Internal 时报告，避免同一处重复。
/// </summary>
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
            var attribute = start.Compilation.GetTypeByMetadataName(Rules.ApiLevelAttributeName);
            if (attribute is null)
            {
                return;
            }

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

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol attribute, IAssemblySymbol assembly)
    {
        ISymbol? member = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IMemberReferenceOperation reference => reference.Member,
            _ => null,
        };

        if (member is null || IsSameAssembly(member, assembly))
        {
            return;
        }

        var original = member.OriginalDefinition;
        if (Rules.IsInternalStability(original, attribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.InternalApiUsage, context.Operation.Syntax.GetLocation(), original.ToDisplayString(Rules.DisplayFormat)));
        }
    }

    private static void AnalyzeTypeSyntax(SyntaxNodeAnalysisContext context, INamedTypeSymbol attribute, IAssemblySymbol assembly)
    {
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

    private static bool IsSameAssembly(ISymbol symbol, IAssemblySymbol assembly)
        => symbol.ContainingAssembly is null || SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, assembly);
}
