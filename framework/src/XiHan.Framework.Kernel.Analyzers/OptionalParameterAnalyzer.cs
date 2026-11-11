// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// XH0008：生效级别为 Stable 的公开方法、构造与索引器不得声明可选参数
/// </summary>
/// <remarks>
/// 默认值编译进调用方，以后加参数或改默认值都破坏二进制兼容（RFC-0001「可选参数」，ADR-0157）：一个可选值用重载，多个收进选项记录。
/// 只约束 Stable：Preview 与 Experimental 可以在小版本破坏，升 Stable 时这条规则随之生效。
/// 重写与接口实现照原声明，不在这里报告；面向源码使用的工具链包（程序集名以 <c>.Testing</c> 或 <c>.Conformance</c> 结尾）例外
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptionalParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rules.StableOptionalParameter);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (!Rules.IsFrameworkPackage(start.Options) || IsToolingPackage(start.Compilation.AssemblyName))
            {
                return;
            }

            var attribute = start.Compilation.GetTypeByMetadataName(Rules.ApiLevelAttributeName);
            if (attribute is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute), SymbolKind.Method, SymbolKind.Property);
        });
    }

    /// <summary>
    /// 检查一个方法或索引器
    /// </summary>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attribute)
    {
        var symbol = context.Symbol;
        var parameters = symbol switch
        {
            IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion } method => method.Parameters,
            IPropertySymbol { IsIndexer: true } indexer => indexer.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty,
        };
        if (parameters.IsEmpty || symbol.IsImplicitlyDeclared || !IsVisible(symbol) || IsInherited(symbol))
        {
            return;
        }

        if (EffectiveStability(symbol, attribute) != 0)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            // 调用方信息参数的值由编译器在调用处填写，默认值只是占位，不算可选参数
            if (parameter.HasExplicitDefaultValue && !IsCallerInfo(parameter) && FirstSourceLocation(parameter) is { } location)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.StableOptionalParameter, location, symbol.ToDisplayString(Rules.DisplayFormat), parameter.Name));
            }
        }
    }

    /// <summary>
    /// 是否为调用方信息参数
    /// </summary>
    /// <remarks>
    /// <c>CallerArgumentExpression</c>、<c>CallerMemberName</c>、<c>CallerFilePath</c>、<c>CallerLineNumber</c>
    /// </remarks>
    private static bool IsCallerInfo(IParameterSymbol parameter)
    {
        foreach (var data in parameter.GetAttributes())
        {
            if (data.AttributeClass is { ContainingNamespace: { } ns } attribute
                && ns.ToDisplayString() == "System.Runtime.CompilerServices"
                && attribute.Name is "CallerArgumentExpressionAttribute" or "CallerMemberNameAttribute" or "CallerFilePathAttribute" or "CallerLineNumberAttribute")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 是否为面向源码使用的工具链包
    /// </summary>
    private static bool IsToolingPackage(string? assemblyName)
        => assemblyName is not null
            && (assemblyName.EndsWith(".Testing", System.StringComparison.Ordinal) || assemblyName.EndsWith(".Conformance", System.StringComparison.Ordinal));

    /// <summary>
    /// 是否为重写或接口实现
    /// </summary>
    /// <remarks>
    /// 签名由被重写或被实现的声明决定，可选参数照原声明（含 BCL 与第三方接口）
    /// </remarks>
    private static bool IsInherited(ISymbol symbol)
    {
        if (symbol.IsOverride)
        {
            return true;
        }

        switch (symbol)
        {
            case IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 }:
            case IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 }:
                return true;
        }

        if (symbol.ContainingType is not { } container)
        {
            return false;
        }

        foreach (var implemented in container.AllInterfaces)
        {
            foreach (var member in implemented.GetMembers(symbol.Name))
            {
                if (SymbolEqualityComparer.Default.Equals(container.FindImplementationForInterfaceMember(member), symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 生效级别
    /// </summary>
    /// <remarks>
    /// 自身没有标注时逐层取包含类型的标注；一路都没有返回 <see langword="null"/>
    /// </remarks>
    private static int? EffectiveStability(ISymbol symbol, INamedTypeSymbol attribute)
    {
        for (var current = symbol; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            foreach (var data in current.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)
                    && data.ConstructorArguments.Length > 0
                    && data.ConstructorArguments[0].Value is int level)
                {
                    return level;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 使用者看得到的成员
    /// </summary>
    /// <remarks>
    /// public、protected 或 protected internal，且所在类型有效公开
    /// </remarks>
    private static bool IsVisible(ISymbol symbol)
        => symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal
            && symbol.ContainingType is { } container
            && Rules.IsEffectivelyPublic(container);

    /// <summary>
    /// 第一个源码位置
    /// </summary>
    private static Location? FirstSourceLocation(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return null;
    }
}
