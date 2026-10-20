// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// 诊断描述与范围判定。范围由 MSBuild 属性 <c>XiHanFrameworkPackage</c>、<c>XiHanRing</c> 传入。
/// </summary>
internal static class Rules
{
    public const string Category = "XiHan";

    public const string ApiLevelAttributeName = "XiHan.Framework.Primitives.ApiLevelAttribute";

    private const string HelpLink = "https://github.com/XiHanFun/XiHan.Framework/blob/next/next/src/XiHan.Framework.Kernel.Analyzers/README.md";

    public static readonly DiagnosticDescriptor MissingApiLevel = new(
        "XH0001",
        "公开类型缺少 ApiLevel 标注",
        "公开类型 '{0}' 必须标注 [ApiLevel]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor XiHanPrefix = new(
        "XH0002",
        "类型名不得以 XiHan 开头",
        "类型名 '{0}' 不得以 XiHan 开头，命名空间已标识归属",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor InternalApiUsage = new(
        "XH0003",
        "不得使用 Internal 稳定性的 API",
        "'{0}' 标注为 Internal 稳定性，不得在其程序集之外使用",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor ForbiddenName = new(
        "XH0004",
        "类型名使用了禁用词",
        "类型名 '{0}' 使用了禁用词 '{1}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor ReflectionInKernel = new(
        "XH0005",
        "Ring 0/1 不得使用反射",
        "'{0}' 是反射 API，原语层与内核禁止反射",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// 诊断消息里的符号写法：<c>Type.Member(参数类型)</c>，不带返回类型与命名空间。
    /// </summary>
    public static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static bool IsFrameworkPackage(AnalyzerOptions options)
        => options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.XiHanFrameworkPackage", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static int Ring(AnalyzerOptions options)
        => options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.XiHanRing", out var value)
            && int.TryParse(value, out var ring)
            ? ring
            : -1;

    public static bool IsEffectivelyPublic(INamedTypeSymbol type)
    {
        for (ISymbol? current = type; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasApiLevel(ISymbol symbol, INamedTypeSymbol attribute)
    {
        foreach (var data in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 符号自身是否标注为 Internal（枚举值 3）。
    /// </summary>
    public static bool IsInternalStability(ISymbol symbol, INamedTypeSymbol attribute)
    {
        foreach (var data in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)
                && data.ConstructorArguments.Length > 0
                && data.ConstructorArguments[0].Value is int level
                && level == 3)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 类型或其任一包含类型是否标注为 Internal。
    /// </summary>
    public static bool IsInternalType(INamedTypeSymbol type, INamedTypeSymbol attribute)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (IsInternalStability(current, attribute))
            {
                return true;
            }
        }

        return false;
    }
}
