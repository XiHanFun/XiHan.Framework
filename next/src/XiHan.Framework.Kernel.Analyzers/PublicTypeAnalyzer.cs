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
            if (!Rules.IsFrameworkPackage(start.Options))
            {
                return;
            }

            var attribute = start.Compilation.GetTypeByMetadataName(Rules.ApiLevelAttributeName);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol? attribute)
    {
        var type = (INamedTypeSymbol)context.Symbol;
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
