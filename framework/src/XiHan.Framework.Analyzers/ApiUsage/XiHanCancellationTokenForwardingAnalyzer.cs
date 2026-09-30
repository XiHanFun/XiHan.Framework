// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace XiHan.Framework.Analyzers.ApiUsage;

/// <summary>
/// 检查对外可见的异步方法未把取消令牌转发给被调方法的 Roslyn 分析器（XHFA002）
/// </summary>
/// <remarks>
/// 只检查对外可见、返回 Task / ValueTask / IAsyncEnumerable 且自身带取消令牌参数的方法；
/// 被调方法的取消令牌参数取了默认值（调用处省略）时报告，显式传入的任何值都不报告。
/// Lambda、匿名方法与本地函数内的调用不检查；生成代码不检查。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XiHanCancellationTokenForwardingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// 诊断属性键：被省略的取消令牌参数名
    /// </summary>
    public const string ParameterNameKey = "ParameterName";

    /// <summary>
    /// 诊断属性键：外层方法可转发的取消令牌参数名
    /// </summary>
    public const string TokenNameKey = "TokenName";

    private static readonly string[] AwaitableMetadataNames =
    [
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.Task`1",
        "System.Threading.Tasks.ValueTask",
        "System.Threading.Tasks.ValueTask`1",
        "System.Collections.Generic.IAsyncEnumerable`1"
    ];

    /// <summary>
    /// 本分析器支持的诊断描述符，即取消令牌未转发规则描述符
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [XiHanApiUsageRule.CancellationTokenNotForwarded];

    /// <summary>
    /// 初始化分析器，开启并发执行并在编译开始时注册调用操作的分析动作
    /// </summary>
    /// <param name="context">分析上下文</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var tokenType = context.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        if (tokenType is null)
        {
            return;
        }

        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var metadataName in AwaitableMetadataNames)
        {
            var type = context.Compilation.GetTypeByMetadataName(metadataName);
            if (type is not null)
            {
                builder.Add(type);
            }
        }

        var awaitableTypes = builder.ToImmutable();
        context.RegisterOperationAction(
            operationContext => AnalyzeInvocation(operationContext, tokenType, awaitableTypes),
            OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol tokenType,
        ImmutableHashSet<INamedTypeSymbol> awaitableTypes)
    {
        if (context.ContainingSymbol is not IMethodSymbol method || !IsPublicAsyncApi(method, awaitableTypes))
        {
            return;
        }

        var availableToken = FindTokenParameter(method, tokenType);
        if (availableToken is null)
        {
            return;
        }

        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.IsImplicit || IsInsideNestedFunction(invocation))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.ArgumentKind != ArgumentKind.DefaultValue
                || argument.Parameter is not { } parameter
                || !SymbolEqualityComparer.Default.Equals(parameter.Type, tokenType))
            {
                continue;
            }

            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(ParameterNameKey, parameter.Name)
                .Add(TokenNameKey, availableToken.Name);

            context.ReportDiagnostic(Diagnostic.Create(
                XiHanApiUsageRule.CancellationTokenNotForwarded,
                invocation.Syntax.GetLocation(),
                properties,
                invocation.TargetMethod.Name,
                availableToken.Name));
            return;
        }
    }

    private static bool IsPublicAsyncApi(IMethodSymbol method, ImmutableHashSet<INamedTypeSymbol> awaitableTypes)
    {
        if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation))
        {
            return false;
        }

        if (method.ReturnType is not INamedTypeSymbol returnType || !awaitableTypes.Contains(returnType.OriginalDefinition))
        {
            return false;
        }

        if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
        {
            return IsExternallyVisible(method.ContainingType)
                && method.ExplicitInterfaceImplementations.Any(IsExternallyVisible);
        }

        return IsExternallyVisible(method);
    }

    private static bool IsExternallyVisible(ISymbol symbol)
    {
        for (var current = symbol; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    private static IParameterSymbol? FindTokenParameter(IMethodSymbol method, INamedTypeSymbol tokenType)
    {
        foreach (var parameter in method.Parameters)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, tokenType))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool IsInsideNestedFunction(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return true;
            }
        }

        return false;
    }
}
