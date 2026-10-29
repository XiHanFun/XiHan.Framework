// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// 诊断描述与范围判定。
/// </summary>
/// <remarks>
/// 范围由 MSBuild 属性 <c>XiHanFrameworkPackage</c>、<c>XiHanRing</c> 传入。
/// </remarks>
internal static class Rules
{
    /// <summary>
    /// 全部诊断共用的类别。
    /// </summary>
    public const string Category = "XiHan";

    /// <summary>
    /// <c>ApiLevelAttribute</c> 的元数据全名。
    /// </summary>
    /// <remarks>
    /// 编译里找不到该类型时，依赖它的规则直接跳过。
    /// </remarks>
    public const string ApiLevelAttributeName = "XiHan.Framework.Primitives.ApiLevelAttribute";

    /// <summary>
    /// 诊断的帮助链接，指向分析器 README 中的规则表。
    /// </summary>
    private const string HelpLink = "https://github.com/XiHanFun/XiHan.Framework/blob/next/next/src/XiHan.Framework.Kernel.Analyzers/README.md";

    /// <summary>
    /// XH0001：框架包里的公开类型缺少 <c>[ApiLevel]</c> 标注。
    /// </summary>
    public static readonly DiagnosticDescriptor MissingApiLevel = new(
        "XH0001",
        "Public type is missing an ApiLevel annotation",
        "Public type '{0}' must be annotated with [ApiLevel]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XH0002。
    /// </summary>
    /// <remarks>
    /// 类型名以 <c>XiHan</c> 开头；归属由命名空间表达，类型名不再重复。
    /// </remarks>
    public static readonly DiagnosticDescriptor XiHanPrefix = new(
        "XH0002",
        "Type name must not start with XiHan",
        "Type name '{0}' must not start with 'XiHan' because the namespace already identifies ownership",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XH0003：在定义程序集之外使用了标注为 <c>Stability.Internal</c> 的类型或成员。
    /// </summary>
    public static readonly DiagnosticDescriptor InternalApiUsage = new(
        "XH0003",
        "Internal API must not be used outside its assembly",
        "'{0}' is annotated with Internal stability and must not be used outside its own assembly",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XH0004：类型名使用了禁用词（<c>Default</c> 前缀，或 <c>Manager</c>、<c>Helper</c> 等后缀）。
    /// </summary>
    public static readonly DiagnosticDescriptor ForbiddenName = new(
        "XH0004",
        "Type name uses a forbidden word",
        "Type name '{0}' uses the forbidden word '{1}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XH0005：Ring 0/1 的代码调用了反射 API。
    /// </summary>
    public static readonly DiagnosticDescriptor ReflectionInKernel = new(
        "XH0005",
        "Reflection is not allowed in Ring 0/1",
        "'{0}' is a reflection API, which is forbidden in the primitives and kernel rings",
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

    /// <summary>
    /// 当前编译是否为框架正式包：MSBuild 属性 <c>XiHanFrameworkPackage</c> 为 <c>true</c>。
    /// </summary>
    public static bool IsFrameworkPackage(AnalyzerOptions options)
        => options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.XiHanFrameworkPackage", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 当前编译所在的环层。
    /// </summary>
    /// <remarks>
    /// MSBuild 属性 <c>XiHanRing</c>；未设置或不是整数时为 -1。
    /// </remarks>
    public static int Ring(AnalyzerOptions options)
        => options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.XiHanRing", out var value)
            && int.TryParse(value, out var ring)
            ? ring
            : -1;

    /// <summary>
    /// 类型是否有效公开。
    /// </summary>
    /// <remarks>
    /// 自身与所有包含类型都是 <c>public</c>。嵌套在 internal 类型里的 public 类型不算。
    /// </remarks>
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

    /// <summary>
    /// 符号自身是否直接标注了 <c>[ApiLevel]</c>。
    /// </summary>
    /// <remarks>
    /// 不看包含类型。
    /// </remarks>
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
            // 按构造参数的整数值判定，不依赖 Stability 枚举类型本身，分析器不引用原语层
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
