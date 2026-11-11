// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// XH0006 / XH0007：稳定性级别之间的依赖
/// </summary>
/// <remarks>
/// XH0006：生效级别为 Stable 的公开类型与成员，签名（基类、接口、泛型约束、参数、返回值、属性与字段类型）不得引用 Preview 或 Experimental 的类型，泛型实参一并检查。
/// XH0007：成员或嵌套类型单独标注的级别不得高于所在类型的生效级别（RFC-0001「稳定性级别」，ADR-0131）。
/// 生效级别取符号自身的标注，没有时逐层取包含类型的标注；没有任何标注的类型（BCL、第三方）不参与判定
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StabilityDependencyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// <c>Stability.Stable</c> 的整数值
    /// </summary>
    private const int Stable = 0;

    /// <summary>
    /// <c>Stability.Preview</c> 的整数值
    /// </summary>
    private const int Preview = 1;

    /// <summary>
    /// <c>Stability.Experimental</c> 的整数值
    /// </summary>
    private const int Experimental = 2;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.StableReferencesUnstable, Rules.MemberAboveType);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            // 级别承诺只约束框架正式包；应用、示例与测试项目不标注级别
            if (!Rules.IsFrameworkPackage(start.Options))
            {
                return;
            }

            var attribute = start.Compilation.GetTypeByMetadataName(Rules.ApiLevelAttributeName);
            if (attribute is null)
            {
                return;
            }

            start.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, attribute),
                SymbolKind.NamedType,
                SymbolKind.Method,
                SymbolKind.Property,
                SymbolKind.Field,
                SymbolKind.Event);
        });
    }

    /// <summary>
    /// 检查一个类型或成员
    /// </summary>
    /// <remarks>
    /// 只看使用者看得到的符号：类型有效公开，成员是 public 或 protected，编译器生成的成员跳过
    /// </remarks>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attribute)
    {
        var symbol = context.Symbol;
        if (symbol.IsImplicitlyDeclared || !IsVisible(symbol) || FirstSourceLocation(symbol) is not { } location)
        {
            return;
        }

        // 属性与事件的访问器随属性、事件本身检查，委托的 Invoke 随委托类型检查
        if (symbol is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise or MethodKind.DelegateInvoke or MethodKind.StaticConstructor or MethodKind.Destructor })
        {
            return;
        }

        CheckAgainstContainer(context, symbol, attribute, location);

        if (EffectiveStability(symbol, attribute) != Stable)
        {
            return;
        }

        // 同一个不稳定类型在一个签名里出现多次只报一次
        var reported = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var referenced in SignatureTypes(symbol))
        {
            foreach (var type in Expand(referenced))
            {
                var level = EffectiveStability(type, attribute);
                if (level is Preview or Experimental && reported.Add(type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rules.StableReferencesUnstable,
                        location,
                        symbol.ToDisplayString(Rules.DisplayFormat),
                        type.ToDisplayString(Rules.DisplayFormat),
                        level == Preview ? "Preview" : "Experimental"));
                }
            }
        }
    }

    /// <summary>
    /// XH0007：符号自身的标注不得高于所在类型的生效级别
    /// </summary>
    /// <remarks>
    /// 只比较 Stable、Preview、Experimental 三级；Internal 与 Deprecated 不在这条序列上
    /// </remarks>
    private static void CheckAgainstContainer(SymbolAnalysisContext context, ISymbol symbol, INamedTypeSymbol attribute, Location location)
    {
        if (symbol.ContainingType is not { } container || !TryGetOwnStability(symbol, attribute, out var own))
        {
            return;
        }

        var outer = EffectiveStability(container, attribute);
        if (outer is { } containerLevel && Rank(own) is { } ownRank && Rank(containerLevel) is { } containerRank && ownRank > containerRank)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.MemberAboveType,
                location,
                symbol.ToDisplayString(Rules.DisplayFormat),
                Name(own),
                Name(containerLevel),
                container.ToDisplayString(Rules.DisplayFormat)));
        }
    }

    /// <summary>
    /// 签名里直接出现的类型
    /// </summary>
    private static IEnumerable<ITypeSymbol> SignatureTypes(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol type:
                if (type.BaseType is { } baseType)
                {
                    yield return baseType;
                }

                foreach (var implemented in type.Interfaces)
                {
                    yield return implemented;
                }

                foreach (var constraint in Constraints(type.TypeParameters))
                {
                    yield return constraint;
                }

                if (type.DelegateInvokeMethod is { } invoke)
                {
                    foreach (var part in MethodTypes(invoke))
                    {
                        yield return part;
                    }
                }

                break;
            case IMethodSymbol method:
                foreach (var part in MethodTypes(method))
                {
                    yield return part;
                }

                break;
            case IPropertySymbol property:
                yield return property.Type;
                foreach (var parameter in property.Parameters)
                {
                    yield return parameter.Type;
                }

                break;
            case IFieldSymbol field:
                yield return field.Type;
                break;
            case IEventSymbol @event:
                yield return @event.Type;
                break;
        }
    }

    /// <summary>
    /// 方法的返回值、参数与泛型约束
    /// </summary>
    private static IEnumerable<ITypeSymbol> MethodTypes(IMethodSymbol method)
    {
        yield return method.ReturnType;
        foreach (var parameter in method.Parameters)
        {
            yield return parameter.Type;
        }

        foreach (var constraint in Constraints(method.TypeParameters))
        {
            yield return constraint;
        }
    }

    /// <summary>
    /// 泛型参数的约束类型
    /// </summary>
    private static IEnumerable<ITypeSymbol> Constraints(ImmutableArray<ITypeParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                yield return constraint;
            }
        }
    }

    /// <summary>
    /// 展开数组、指针与泛型实参，得到参与判定的命名类型
    /// </summary>
    /// <remarks>
    /// 泛型类型取原始定义，标注在定义上；泛型参数本身没有级别
    /// </remarks>
    private static IEnumerable<INamedTypeSymbol> Expand(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var element in Expand(array.ElementType))
                {
                    yield return element;
                }

                break;
            case IPointerTypeSymbol pointer:
                foreach (var element in Expand(pointer.PointedAtType))
                {
                    yield return element;
                }

                break;
            case INamedTypeSymbol named:
                yield return named.OriginalDefinition;
                foreach (var argument in named.TypeArguments)
                {
                    foreach (var element in Expand(argument))
                    {
                        yield return element;
                    }
                }

                break;
        }
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
            if (TryGetOwnStability(current, attribute, out var level))
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>
    /// 符号自身标注的级别
    /// </summary>
    /// <remarks>
    /// 按构造参数的整数值读取，分析器不引用原语层的 <c>Stability</c> 枚举
    /// </remarks>
    private static bool TryGetOwnStability(ISymbol symbol, INamedTypeSymbol attribute, out int level)
    {
        foreach (var data in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)
                && data.ConstructorArguments.Length > 0
                && data.ConstructorArguments[0].Value is int value)
            {
                level = value;
                return true;
            }
        }

        level = -1;
        return false;
    }

    /// <summary>
    /// 级别在 Experimental → Preview → Stable 序列上的位置
    /// </summary>
    /// <remarks>
    /// 越稳定越大；Internal 与 Deprecated 不在序列上，返回 <see langword="null"/>
    /// </remarks>
    private static int? Rank(int level) => level switch
    {
        Experimental => 0,
        Preview => 1,
        Stable => 2,
        _ => null,
    };

    /// <summary>
    /// 级别在诊断消息里的名字
    /// </summary>
    private static string Name(int level) => level switch
    {
        Stable => "Stable",
        Preview => "Preview",
        Experimental => "Experimental",
        3 => "Internal",
        4 => "Deprecated",
        _ => level.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// 使用者看得到的符号
    /// </summary>
    /// <remarks>
    /// 类型要有效公开；成员要是 public、protected 或 protected internal，且所在类型有效公开
    /// </remarks>
    private static bool IsVisible(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol type)
        {
            return Rules.IsEffectivelyPublic(type);
        }

        return symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal
            && symbol.ContainingType is { } container
            && Rules.IsEffectivelyPublic(container);
    }

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
