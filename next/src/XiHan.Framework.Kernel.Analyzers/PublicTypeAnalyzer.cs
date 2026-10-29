// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// XH0001 / XH0002 / XH0004：框架包中公开类型的稳定性标注与命名。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// 禁用的类型名后缀。
    /// </summary>
    /// <remarks>
    /// 与 <c>Default</c> 前缀一起构成 XH0004 的词表。
    /// </remarks>
    private static readonly ImmutableArray<string> _forbiddenSuffixes = ImmutableArray.Create("Manager", "Helper", "Utils", "Util", "Common", "Shared", "Impl", "Base");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.MissingApiLevel, Rules.XiHanPrefix, Rules.ForbiddenName);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            // 只对框架正式包生效；应用、示例与测试项目不受这些命名规则约束
            if (!Rules.IsFrameworkPackage(start.Options))
            {
                return;
            }

            // 找不到 ApiLevelAttribute 时只检查命名，不报 XH0001
            var attribute = start.Compilation.GetTypeByMetadataName(Rules.ApiLevelAttributeName);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute), SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// 对单个命名类型执行 XH0001、XH0002 与 XH0004。
    /// </summary>
    /// <remarks>
    /// 只看源码里声明的有效公开类型。
    /// </remarks>
    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol? attribute)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // 跳过编译器生成的类型（隐式声明或名字带 '<'）、非有效公开的类型，以及没有源码位置的类型
        if (type.IsImplicitlyDeclared || !Rules.IsEffectivelyPublic(type) || type.Locations.Length == 0 || type.Name.IndexOf('<') >= 0)
        {
            return;
        }

        var location = type.Locations[0];

        if (attribute is not null && !Rules.HasApiLevel(type, attribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.MissingApiLevel, location, type.Name));
        }

        if (type.Name.StartsWith("XiHan", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.XiHanPrefix, location, type.Name));
        }

        // Default 前缀与禁用后缀同属 XH0004，一个类型只报一次；名字恰好等于禁用词本身（如 Default、Base）不算
        if (type.Name.StartsWith("Default", StringComparison.Ordinal) && type.Name.Length > "Default".Length)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenName, location, type.Name, "Default"));
            return;
        }

        foreach (var suffix in _forbiddenSuffixes)
        {
            if (type.Name.Length > suffix.Length && type.Name.EndsWith(suffix, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenName, location, type.Name, suffix));
                return;
            }
        }
    }
}
