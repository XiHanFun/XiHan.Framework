// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using XiHan.Framework.Kernel.Generators.Json;

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 从 <c>[Operation]</c> 类生成登记代码与 JSON Schema：输出 <c>GeneratedOperations</c> 静态类，含每个操作的 <c>Add&lt;类名&gt;</c> 与登记全部操作的 <c>AddGeneratedOperations</c>。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class OperationGenerator : IIncrementalGenerator
{
    /// <summary>
    /// <c>OperationAttribute</c> 的全名。
    /// </summary>
    private const string OperationAttributeName = "XiHan.Framework.Kernel.Operations.OperationAttribute";

    /// <summary>
    /// <c>IOperation&lt;,&gt;</c> 的元数据名。
    /// </summary>
    private const string OperationInterfaceName = "XiHan.Framework.Kernel.Operations.IOperation`2";

    /// <summary>
    /// 操作 ID 文法，与原语层 <c>OperationId</c> 一致。
    /// </summary>
    private static readonly Regex _operationId = new("^[a-z][a-z0-9]*(-[a-z0-9]+)*\\.[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var operations = context.SyntaxProvider.ForAttributeWithMetadataName(
            OperationAttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, _) => (Symbol: (INamedTypeSymbol)syntaxContext.TargetSymbol, Attribute: syntaxContext.Attributes[0]));

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) && !string.IsNullOrWhiteSpace(value) ? value : null);

        var input = operations.Collect().Combine(context.CompilationProvider).Combine(rootNamespace);
        context.RegisterSourceOutput(input, static (productionContext, data) => Emit(productionContext, data.Left.Left, data.Left.Right, data.Right));
    }

    /// <summary>
    /// 校验每个操作、解析序列化上下文并输出源文件。
    /// </summary>
    private static void Emit(SourceProductionContext context, ImmutableArray<(INamedTypeSymbol Symbol, AttributeData Attribute)> candidates, Compilation compilation, string? rootNamespace)
    {
        if (candidates.IsDefaultOrEmpty)
        {
            return;
        }

        var operationInterface = compilation.GetTypeByMetadataName(OperationInterfaceName);
        var contexts = JsonContextModel.Collect(compilation);
        var models = new List<OperationModel>();
        foreach (var (symbol, attribute) in candidates.OrderBy(static c => c.Symbol.ToDisplayString(), StringComparer.Ordinal))
        {
            var model = CreateModel(context, symbol, attribute, operationInterface, contexts);
            if (model is not null)
            {
                models.Add(model);
            }
        }

        if (models.Count == 0)
        {
            return;
        }

        var ns = Sanitize(rootNamespace ?? compilation.AssemblyName ?? "Generated");
        context.AddSource("GeneratedOperations.g.cs", SourceText.From(Render(ns, models), Encoding.UTF8));
    }

    /// <summary>
    /// 读取特性与接口、解析上下文；有问题的操作报诊断并跳过。
    /// </summary>
    private static OperationModel? CreateModel(SourceProductionContext context, INamedTypeSymbol symbol, AttributeData attribute, INamedTypeSymbol? operationInterface, List<JsonContextModel> contexts)
    {
        var location = symbol.Locations.FirstOrDefault() ?? Location.None;
        var id = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
        if (id is null || !_operationId.IsMatch(id))
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.InvalidOperationId, location, id ?? string.Empty, symbol.Name));
            return null;
        }

        var implemented = operationInterface is null
            ? null
            : symbol.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, operationInterface));
        if (implemented is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.MissingOperationInterface, location, symbol.Name));
            return null;
        }

        var stability = 0;
        // Expose.All：Http | Grpc | Mcp | CommandLine | Ai（与内核枚举同步）
        var expose = 31;
        string? policy = null;
        string? permission = null;
        foreach (var argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case "Stability" when argument.Value.Value is int s:
                    stability = s;
                    break;
                case "Expose" when argument.Value.Value is int e:
                    expose = e;
                    break;
                case "Policy":
                    policy = argument.Value.Value as string;
                    break;
                case "Permission":
                    permission = argument.Value.Value as string;
                    break;
            }
        }

        var model = new OperationModel(symbol, id, stability, expose, policy, permission, implemented.TypeArguments[0], implemented.TypeArguments[1]);
        if (!Resolve(contexts, model.RequestType, out var requestContext, out var requestProperty))
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.MissingJsonTypeInfo, location, model.RequestType.ToDisplayString(), id));
            return null;
        }

        if (!Resolve(contexts, model.ResponseType, out var responseContext, out var responseProperty))
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratorRules.MissingJsonTypeInfo, location, model.ResponseType.ToDisplayString(), id));
            return null;
        }

        model.RequestContext = requestContext;
        model.RequestProperty = requestProperty;
        model.ResponseContext = responseContext;
        model.ResponseProperty = responseProperty;
        return model;
    }

    /// <summary>
    /// 在全部上下文里找登记了该类型的那个。
    /// </summary>
    private static bool Resolve(List<JsonContextModel> contexts, ITypeSymbol type, out JsonContextModel? found, out string? property)
    {
        foreach (var candidate in contexts)
        {
            if (candidate.TryGetPropertyName(type, out var name))
            {
                found = candidate;
                property = name;
                return true;
            }
        }

        found = null;
        property = null;
        return false;
    }

    /// <summary>
    /// 渲染源文件。
    /// </summary>
    private static string Render(string ns, List<OperationModel> models)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// 由 XiHan.Framework.Kernel.Generators 从 [Operation] 生成：操作登记代码与请求/响应 JSON Schema。");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.Append("namespace ").Append(ns).AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// 本程序集全部 <c>[Operation]</c> 的登记入口与 Schema。");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"XiHan.Framework.Kernel.Generators\", \"5.0\")]");
        builder.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        builder.AppendLine("public static class GeneratedOperations");
        builder.AppendLine("{");

        var methodNames = new List<string>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            var methodName = "Add" + model.OperationType.Name;
            var index = 2;
            while (!usedNames.Add(methodName))
            {
                methodName = "Add" + model.OperationType.Name + index++;
            }

            methodNames.Add(methodName);
            var requestSchema = new JsonSchemaBuilder(model.RequestContext!.NamingPolicy, model.RequestContext.UseStringEnumConverter).Build(model.RequestType);
            var responseSchema = new JsonSchemaBuilder(model.ResponseContext!.NamingPolicy, model.ResponseContext.UseStringEnumConverter).Build(model.ResponseType);

            builder.AppendLine("    /// <summary>");
            builder.Append("    /// 登记操作 <c>").Append(model.Id).Append("</c>（").Append(model.OperationType.Name).AppendLine("）。");
            builder.AppendLine("    /// </summary>");
            builder.Append("    public static global::XiHan.Framework.Kernel.Hosting.FeatureContext ").Append(methodName).AppendLine("(this global::XiHan.Framework.Kernel.Hosting.FeatureContext context)");
            builder.Append("        => context.AddOperation<").Append(Display(model.OperationType)).Append(", ").Append(Display(model.RequestType)).Append(", ").Append(Display(model.ResponseType)).AppendLine(">(");
            builder.Append("            new global::XiHan.Framework.Kernel.Operations.OperationContract(").Append(Literal(model.Id))
                .Append(", (global::XiHan.Framework.Primitives.Stability)").Append(model.Stability)
                .Append(", (global::XiHan.Framework.Kernel.Operations.Expose)").Append(model.Expose)
                .Append(", ").Append(model.Policy is null ? "null" : Literal(model.Policy))
                .Append(", ").Append(model.Permission is null ? "null" : Literal(model.Permission)).AppendLine("),");
            builder.Append("            ").Append(Display(model.RequestContext.Type)).Append(".Default.").Append(model.RequestProperty).AppendLine(",");
            builder.Append("            ").Append(Display(model.ResponseContext.Type)).Append(".Default.").Append(model.ResponseProperty).AppendLine(",");
            builder.Append("            new global::XiHan.Framework.Kernel.Operations.OperationSchema(").Append(Literal(requestSchema)).Append(", ").Append(Literal(responseSchema)).AppendLine("));");
            builder.AppendLine();
        }

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// 登记本程序集的全部操作。");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static global::XiHan.Framework.Kernel.Hosting.FeatureContext AddGeneratedOperations(this global::XiHan.Framework.Kernel.Hosting.FeatureContext context)");
        builder.AppendLine("    {");
        foreach (var methodName in methodNames)
        {
            builder.Append("        context.").Append(methodName).AppendLine("();");
        }

        builder.AppendLine("        return context;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    /// 带 <c>global::</c> 的完整类型名。
    /// </summary>
    private static string Display(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    /// <summary>
    /// C# 字符串字面量（逐字字符串，双写引号）。
    /// </summary>
    private static string Literal(string value) => "@\"" + value.Replace("\"", "\"\"") + "\"";

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
