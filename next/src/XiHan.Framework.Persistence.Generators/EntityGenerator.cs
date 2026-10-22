// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XiHan.Framework.Persistence.Generators;

/// <summary>
/// 从实现 <c>IEntity&lt;TKey&gt;</c> 的实体类生成 <c>GeneratedEntities</c>：仓储登记 <c>AddGeneratedRepositories</c>、引用了 EF Core 时的实体登记 <c>ApplyGeneratedEntities</c>，
/// 以及每个软删除 / 多租户实体的 <c>&lt;实体&gt;Filters</c> 规约（<c>NotDeleted</c>、<c>ForTenant</c>、<c>ActiveFor</c>）。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EntityGenerator : IIncrementalGenerator
{
    /// <summary>
    /// <c>IEntity&lt;TKey&gt;</c> 的元数据名。
    /// </summary>
    private const string EntityInterfaceName = "XiHan.Framework.Domain.IEntity`1";

    /// <summary>
    /// <c>IEntity&lt;TKey&gt;</c> 原始定义的显示名，供语法阶段快速筛选。
    /// </summary>
    private const string EntityInterfaceDisplay = "XiHan.Framework.Domain.IEntity<TKey>";

    /// <summary>
    /// <c>ISoftDeletable</c> 的元数据名。
    /// </summary>
    private const string SoftDeletableName = "XiHan.Framework.Domain.ISoftDeletable";

    /// <summary>
    /// <c>IMultiTenant</c> 的元数据名。
    /// </summary>
    private const string MultiTenantName = "XiHan.Framework.Domain.IMultiTenant";

    /// <summary>
    /// EF Core <c>ModelBuilder</c> 的元数据名；存在时生成实体登记。
    /// </summary>
    private const string ModelBuilderName = "Microsoft.EntityFrameworkCore.ModelBuilder";

    /// <summary>
    /// 持久化 EF Core Provider 的约定扩展；存在时在实体登记后应用约定。
    /// </summary>
    private const string ConventionsName = "XiHan.Framework.Persistence.EntityFrameworkCore.ModelBuilderExtensions";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entities = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } declaration
                    && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
                    && !declaration.Modifiers.Any(SyntaxKind.StaticKeyword),
                static (syntaxContext, token) => syntaxContext.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntaxContext.Node, token))
            .Where(static symbol => symbol is not null && ImplementsEntity(symbol))
            .Select(static (symbol, _) => symbol!);

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) && !string.IsNullOrWhiteSpace(value) ? value : null);

        var input = entities.Collect().Combine(context.CompilationProvider).Combine(rootNamespace);
        context.RegisterSourceOutput(input, static (productionContext, data) => Emit(productionContext, data.Left.Left, data.Left.Right, data.Right));
    }

    /// <summary>
    /// 类是否（直接或经 <c>IAggregateRoot&lt;TKey&gt;</c>）实现了 <c>IEntity&lt;TKey&gt;</c>。
    /// </summary>
    private static bool ImplementsEntity(INamedTypeSymbol symbol)
    {
        foreach (var implemented in symbol.AllInterfaces)
        {
            if (implemented.IsGenericType && implemented.OriginalDefinition.ToDisplayString() == EntityInterfaceDisplay)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 校验每个实体并输出源文件。
    /// </summary>
    private static void Emit(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> candidates, Compilation compilation, string? rootNamespace)
    {
        var entityInterface = compilation.GetTypeByMetadataName(EntityInterfaceName);
        if (entityInterface is null || candidates.IsDefaultOrEmpty)
        {
            return;
        }

        var softDeletable = compilation.GetTypeByMetadataName(SoftDeletableName);
        var multiTenant = compilation.GetTypeByMetadataName(MultiTenantName);
        var models = new List<EntityModel>();
        // partial 类的每个声明都会命中一次，按显示名去重
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in candidates.OrderBy(static c => c.ToDisplayString(), StringComparer.Ordinal))
        {
            if (!seen.Add(symbol.ToDisplayString()))
            {
                continue;
            }

            var model = CreateModel(context, symbol, entityInterface, softDeletable, multiTenant, compilation);
            if (model is not null)
            {
                models.Add(model);
            }
        }

        if (models.Count == 0)
        {
            return;
        }

        var hasModelBuilder = compilation.GetTypeByMetadataName(ModelBuilderName) is not null;
        var hasConventions = hasModelBuilder && compilation.GetTypeByMetadataName(ConventionsName) is not null;
        var ns = Sanitize(rootNamespace ?? compilation.AssemblyName ?? "Generated");
        context.AddSource("GeneratedEntities.g.cs", SourceText.From(Render(ns, models, hasModelBuilder, hasConventions), Encoding.UTF8));
    }

    /// <summary>
    /// 解析键类型与协议；有问题的实体报诊断并跳过。
    /// </summary>
    private static EntityModel? CreateModel(SourceProductionContext context, INamedTypeSymbol symbol, INamedTypeSymbol entityInterface, INamedTypeSymbol? softDeletable, INamedTypeSymbol? multiTenant, Compilation compilation)
    {
        var location = symbol.Locations.FirstOrDefault() ?? Location.None;
        var keys = new List<ITypeSymbol>();
        foreach (var implemented in symbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, entityInterface)
                && !keys.Any(k => SymbolEqualityComparer.Default.Equals(k, implemented.TypeArguments[0])))
            {
                keys.Add(implemented.TypeArguments[0]);
            }
        }

        if (keys.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.AmbiguousKey, location, symbol.Name));
            return null;
        }

        if (symbol.ContainingType is not null || symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.InaccessibleEntity, location, symbol.Name));
            return null;
        }

        var isSoftDeletable = softDeletable is not null && symbol.AllInterfaces.Contains(softDeletable, SymbolEqualityComparer.Default);
        var isMultiTenant = multiTenant is not null && symbol.AllInterfaces.Contains(multiTenant, SymbolEqualityComparer.Default);
        var generateFilters = isSoftDeletable || isMultiTenant;
        if (generateFilters)
        {
            // 过滤规约类与实体同命名空间：已有同名类型时不生成并提示，不让生成代码制造编译错误
            var filtersName = symbol.Name + "Filters";
            var qualified = symbol.ContainingNamespace.IsGlobalNamespace ? filtersName : symbol.ContainingNamespace.ToDisplayString() + "." + filtersName;
            if (compilation.GetTypeByMetadataName(qualified) is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.FilterNameConflict, location, symbol.Name, qualified));
                generateFilters = false;
            }
        }

        return new EntityModel(symbol, keys[0], isSoftDeletable, isMultiTenant, generateFilters);
    }

    /// <summary>
    /// 渲染源文件。
    /// </summary>
    private static string Render(string ns, List<EntityModel> models, bool hasModelBuilder, bool hasConventions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// 由 XiHan.Framework.Persistence.Generators 从实现 IEntity<TKey> 的实体类生成：仓储登记、EF Core 实体登记与软删除 / 租户过滤规约。");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.Append("namespace ").AppendLine(ns);
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// 本程序集全部实体的登记入口。");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"XiHan.Framework.Persistence.Generators\", \"5.0\")]");
        builder.AppendLine("    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        builder.AppendLine("    public static class GeneratedEntities");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.Append("        /// 登记本程序集全部实体的仓储（").Append(models.Count).AppendLine(" 个）。");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedRepositories(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        foreach (var model in models)
        {
            builder.Append("            global::XiHan.Framework.Persistence.Abstractions.RepositoryServiceCollectionExtensions.AddRepository<").Append(Display(model.Type)).Append(", ").Append(Display(model.KeyType)).AppendLine(">(services);");
        }

        builder.AppendLine("            return services;");
        builder.AppendLine("        }");

        if (hasModelBuilder)
        {
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine(hasConventions ? "        /// 把本程序集全部实体登记进 EF Core 模型，并应用持久化约定（并发戳为并发令牌，租户与软删除建索引）。" : "        /// 把本程序集全部实体登记进 EF Core 模型。");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public static global::Microsoft.EntityFrameworkCore.ModelBuilder ApplyGeneratedEntities(this global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)");
            builder.AppendLine("        {");
            foreach (var model in models)
            {
                builder.Append("            modelBuilder.Entity<").Append(Display(model.Type)).AppendLine(">();");
            }

            builder.AppendLine(hasConventions
                ? "            return global::XiHan.Framework.Persistence.EntityFrameworkCore.ModelBuilderExtensions.ApplyPersistenceConventions(modelBuilder);"
                : "            return modelBuilder;");
            builder.AppendLine("        }");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");

        foreach (var model in models.Where(static m => m.GenerateFilters))
        {
            RenderFilters(builder, model);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 渲染一个实体的过滤规约类，放在实体的命名空间里，可访问性与实体一致。
    /// </summary>
    private static void RenderFilters(StringBuilder builder, EntityModel model)
    {
        var type = Display(model.Type);
        var specification = "global::XiHan.Framework.Domain.Specification<" + type + ">";
        var accessibility = model.Type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var global = model.Type.ContainingNamespace.IsGlobalNamespace;
        var indent = global ? string.Empty : "    ";
        builder.AppendLine();
        if (!global)
        {
            builder.Append("namespace ").AppendLine(model.Type.ContainingNamespace.ToDisplayString());
            builder.AppendLine("{");
        }

        builder.Append(indent).AppendLine("/// <summary>");
        builder.Append(indent).Append("/// <see cref=\"").Append(type).AppendLine("\"/> 的软删除与租户过滤规约，可直接传给 <c>EntityQuery&lt;T&gt;.Where</c>。");
        builder.Append(indent).AppendLine("/// </summary>");
        builder.Append(indent).AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"XiHan.Framework.Persistence.Generators\", \"5.0\")]");
        builder.Append(indent).AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        builder.Append(indent).Append(accessibility).Append(" static class ").AppendLine(model.FiltersName);
        builder.Append(indent).AppendLine("{");
        if (model.IsSoftDeletable)
        {
            builder.Append(indent).AppendLine("    /// <summary>");
            builder.Append(indent).AppendLine("    /// 未软删除。");
            builder.Append(indent).AppendLine("    /// </summary>");
            builder.Append(indent).Append("    public static ").Append(specification).Append(" NotDeleted { get; } = new ").Append(specification).AppendLine("(static entity => !entity.IsDeleted);");
        }

        if (model.IsMultiTenant)
        {
            if (model.IsSoftDeletable)
            {
                builder.AppendLine();
            }

            builder.Append(indent).AppendLine("    /// <summary>");
            builder.Append(indent).AppendLine("    /// 属于指定租户。");
            builder.Append(indent).AppendLine("    /// </summary>");
            builder.Append(indent).Append("    public static ").Append(specification).Append(" ForTenant(string tenantId) => new ").Append(specification).AppendLine("(entity => entity.TenantId == tenantId);");
        }

        if (model.IsSoftDeletable && model.IsMultiTenant)
        {
            builder.AppendLine();
            builder.Append(indent).AppendLine("    /// <summary>");
            builder.Append(indent).AppendLine("    /// 未软删除且属于指定租户。");
            builder.Append(indent).AppendLine("    /// </summary>");
            builder.Append(indent).Append("    public static ").Append(specification).AppendLine(" ActiveFor(string tenantId) => NotDeleted.And(ForTenant(tenantId));");
        }

        builder.Append(indent).AppendLine("}");
        if (!global)
        {
            builder.AppendLine("}");
        }
    }

    /// <summary>
    /// 带 <c>global::</c> 的完整类型名；特殊类型仍用关键字（<c>int</c>、<c>string</c>）。
    /// </summary>
    private static string Display(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    /// <summary>
    /// 把根命名空间整理成合法的命名空间名。
    /// </summary>
    private static string Sanitize(string value)
    {
        var parts = value.Split('.').Select(static part =>
        {
            var cleaned = new string(part.Where(static c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            return cleaned.Length == 0 || char.IsDigit(cleaned[0]) ? "_" + cleaned : cleaned;
        });
        return string.Join(".", parts);
    }
}
