// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Kernel.Generators.Json;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Kernel.Generators.Tests;

/// <summary>
/// 操作源生成器：登记代码、JSON Schema 与诊断。生成的代码必须能在带内核引用的编译里通过编译。
/// </summary>
public sealed class OperationGeneratorTests
{
    /// <summary>
    /// 一个带嵌套类型、可空、默认值、集合与枚举的操作，以及手写的序列化上下文桩（生成器只看特性，不需要 STJ 生成器真的运行）。
    /// </summary>
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using System.Threading.Tasks;
        using XiHan.Framework.Kernel.Operations;
        using XiHan.Framework.Primitives;

        namespace Demo.App;

        public enum OrderStatus { Open, Closed }

        public sealed record OrderLine(string Sku, int Quantity);

        public sealed record CreateOrderRequest(string Name, List<OrderLine> Lines, int Amount = 0, string? Note = null, Dictionary<string, decimal>? Prices = null);

        public sealed record OrderResponse(Guid Id, DateTimeOffset CreatedAt, OrderStatus Status, OrderLine? First, byte[] Receipt);

        public sealed record AnnotatedRequest(
            [property: Required, StringLength(64, MinimumLength = 2)] string? Code,
            [Range(1, 1000)] int Amount,
            [MinLength(1), MaxLength(5)] List<string> Tags,
            [RegularExpression("^[a-z]+$")] string Slug,
            [Range(0.5, 9.5, MinimumIsExclusive = true)] double Ratio,
            [Required] int? Count,
            [Required(AllowEmptyStrings = true)] string? Label = null);

        [Operation("orders.annotate")]
        public sealed class AnnotateOrder : IOperation<AnnotatedRequest, OrderResponse>
        {
            public ValueTask<Result<OrderResponse>> HandleAsync(AnnotatedRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("orders.create", Stability = Stability.Experimental, Expose = Expose.Http | Expose.Mcp, Permission = "orders.write")]
        public sealed class CreateOrder : IOperation<CreateOrderRequest, OrderResponse>
        {
            public ValueTask<Result<OrderResponse>> HandleAsync(CreateOrderRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("orders.list", Policy = "authenticated")]
        public sealed class ListOrders : IOperation<CreateOrderRequest, OrderResponse>
        {
            public ValueTask<Result<OrderResponse>> HandleAsync(CreateOrderRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(CreateOrderRequest))]
        [JsonSerializable(typeof(OrderResponse), TypeInfoPropertyName = "Order")]
        [JsonSerializable(typeof(AnnotatedRequest))]
        internal sealed class DemoJsonContext : JsonSerializerContext
        {
            public static DemoJsonContext Default { get; } = new();
            public DemoJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<CreateOrderRequest> CreateOrderRequest => null!;
            public JsonTypeInfo<OrderResponse> Order => null!;
            public JsonTypeInfo<AnnotatedRequest> AnnotatedRequest => null!;
        }

        public enum UserStatus { Active, Disabled }

        public sealed record PageResult<T>(IReadOnlyList<T> Items, long Total);

        public sealed record Envelope<TBody, TMeta>(TBody Body, TMeta Meta);

        public sealed record UserResponse(Guid Id, UserStatus Status, UserStatus? PreviousStatus);

        public sealed record RoleResponse(Guid Id, string Name);

        public sealed record PageQuery(int Page = 1, UserStatus? Status = null);

        [Operation("users.list", Expose = Expose.Http | Expose.Grpc)]
        public sealed class ListUsers : IOperation<PageQuery, PageResult<UserResponse>>
        {
            public ValueTask<Result<PageResult<UserResponse>>> HandleAsync(PageQuery request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("roles.list", Expose = Expose.Http | Expose.Grpc)]
        public sealed class ListRoles : IOperation<PageQuery, PageResult<RoleResponse>>
        {
            public ValueTask<Result<PageResult<RoleResponse>>> HandleAsync(PageQuery request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("users.export")]
        public sealed class ExportUsers : IOperation<PageQuery, Envelope<List<UserResponse>, int>>
        {
            public ValueTask<Result<Envelope<List<UserResponse>, int>>> HandleAsync(PageQuery request, InvocationContext context) => throw new NotImplementedException();
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
        [JsonSerializable(typeof(PageQuery))]
        [JsonSerializable(typeof(PageResult<UserResponse>))]
        [JsonSerializable(typeof(PageResult<RoleResponse>))]
        [JsonSerializable(typeof(Envelope<List<UserResponse>, int>))]
        internal sealed class PagedJsonContext : JsonSerializerContext
        {
            public static PagedJsonContext Default { get; } = new();
            public PagedJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<PageQuery> PageQuery => null!;
            public JsonTypeInfo<PageResult<UserResponse>> PageResultUserResponse => null!;
            public JsonTypeInfo<PageResult<RoleResponse>> PageResultRoleResponse => null!;
            public JsonTypeInfo<Envelope<List<UserResponse>, int>> EnvelopeListUserResponseInt32 => null!;
        }
        """;

    /// <summary>
    /// 运行生成器并把生成的代码编回编译；返回编译诊断、生成器诊断与生成的源文本。
    /// </summary>
    private static (ImmutableArray<Diagnostic> Compile, ImmutableArray<Diagnostic> Generator, string Generated) Run(string source, string rootNamespace = "Demo.App")
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = trustedAssemblies
            .Concat([typeof(OperationAttribute).Assembly.Location, typeof(Primitives.Error).Assembly.Location, typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Demo.App",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new OperationGenerator().AsSourceGenerator()], optionsProvider: new TestAnalyzerConfigOptionsProvider(rootNamespace));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        var generated = output.SyntaxTrees.FirstOrDefault(static t => t.FilePath.EndsWith("GeneratedOperations.g.cs", StringComparison.Ordinal))?.ToString() ?? string.Empty;
        return (output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(), generatorDiagnostics, generated);
    }

    [Fact]
    public void Generates_RegistrationAndSchemas_ThatCompile()
    {
        var (compile, generator, generated) = Run(Source);
        var text = generated.Replace("\"\"", "\"", StringComparison.Ordinal);

        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("namespace Demo.App;", generated, StringComparison.Ordinal);
        Assert.Contains("public static class GeneratedOperations", generated, StringComparison.Ordinal);
        Assert.Contains("AddCreateOrder(this global::XiHan.Framework.Kernel.Hosting.FeatureContext context)", generated, StringComparison.Ordinal);
        Assert.Contains("context.AddOperation<global::Demo.App.CreateOrder, global::Demo.App.CreateOrderRequest, global::Demo.App.OrderResponse>(", generated, StringComparison.Ordinal);
        Assert.Contains("new global::XiHan.Framework.Kernel.Operations.OperationContract(@\"orders.create\", (global::XiHan.Framework.Primitives.Stability)2, (global::XiHan.Framework.Kernel.Operations.Expose)5, null, @\"orders.write\")", generated, StringComparison.Ordinal);
        Assert.Contains("global::Demo.App.DemoJsonContext.Default.CreateOrderRequest", generated, StringComparison.Ordinal);
        Assert.Contains("global::Demo.App.DemoJsonContext.Default.Order,", generated, StringComparison.Ordinal);
        Assert.Contains("(global::XiHan.Framework.Kernel.Operations.Expose)31, @\"authenticated\", null)", generated, StringComparison.Ordinal);
        Assert.Contains("context.AddCreateOrder();", generated, StringComparison.Ordinal);
        Assert.Contains("context.AddListOrders();", generated, StringComparison.Ordinal);

        // 请求 Schema：camelCase、必填、可空、默认值、嵌套 $defs、字典
        Assert.Contains("\"title\":\"CreateOrderRequest\",\"type\":\"object\"", text, StringComparison.Ordinal);
        Assert.Contains("\"name\":{\"type\":\"string\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"lines\":{\"type\":\"array\",\"items\":{\"$ref\":\"#/$defs/OrderLine\"}}", text, StringComparison.Ordinal);
        Assert.Contains("\"amount\":{\"type\":\"integer\",\"format\":\"int32\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"note\":{\"type\":[\"string\",\"null\"]}", text, StringComparison.Ordinal);
        Assert.Contains("\"prices\":{\"type\":[\"object\",\"null\"],\"additionalProperties\":{\"type\":\"number\",\"format\":\"decimal\"}}", text, StringComparison.Ordinal);
        Assert.Contains("\"required\":[\"name\",\"lines\"]", text, StringComparison.Ordinal);
        Assert.Contains("\"$defs\":{\"OrderLine\":{\"type\":\"object\",\"properties\":{\"sku\":{\"type\":\"string\"},\"quantity\":{\"type\":\"integer\",\"format\":\"int32\"}},\"required\":[\"sku\",\"quantity\"]}}", text, StringComparison.Ordinal);

        // DataAnnotations 折进请求 Schema：[Required] 去可空并进 required（字符串默认 minLength 1，AllowEmptyStrings 除外）、[Range] 的闭开区间、长度与元素数、模式；注解在参数或属性上都认
        Assert.Contains("\"code\":{\"type\":\"string\",\"minLength\":2,\"maxLength\":64}", text, StringComparison.Ordinal);
        Assert.Contains("\"amount\":{\"type\":\"integer\",\"format\":\"int32\",\"minimum\":1,\"maximum\":1000}", text, StringComparison.Ordinal);
        Assert.Contains("\"tags\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"minItems\":1,\"maxItems\":5}", text, StringComparison.Ordinal);
        Assert.Contains("\"slug\":{\"type\":\"string\",\"pattern\":\"^[a-z]+$\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"ratio\":{\"type\":\"number\",\"format\":\"double\",\"exclusiveMinimum\":0.5,\"maximum\":9.5}", text, StringComparison.Ordinal);
        Assert.Contains("\"count\":{\"type\":\"integer\",\"format\":\"int32\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"label\":{\"type\":\"string\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"required\":[\"code\",\"amount\",\"tags\",\"slug\",\"ratio\",\"count\",\"label\"]", text, StringComparison.Ordinal);

        // 响应 Schema：uuid、date-time、枚举按整数、可空引用的 anyOf、字节数组 base64
        Assert.Contains("\"id\":{\"type\":\"string\",\"format\":\"uuid\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"createdAt\":{\"type\":\"string\",\"format\":\"date-time\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"status\":{\"type\":\"integer\",\"format\":\"int32\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"first\":{\"anyOf\":[{\"$ref\":\"#/$defs/OrderLine\"},{\"type\":\"null\"}]}", text, StringComparison.Ordinal);
        Assert.Contains("\"receipt\":{\"type\":\"string\",\"contentEncoding\":\"base64\"}", text, StringComparison.Ordinal);

        // 生成的 Schema 字面量必须是内核能接受的 JSON 对象
        var schemas = CSharpSyntaxTree.ParseText(generated).GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(static c => c.Type.ToString().EndsWith("OperationSchema", StringComparison.Ordinal))
            .Select(static c => c.ArgumentList!.Arguments.Select(static a => ((LiteralExpressionSyntax)a.Expression).Token.ValueText).ToArray())
            .ToList();
        Assert.Equal(6, schemas.Count);
        foreach (var pair in schemas)
        {
            var schema = new OperationSchema(pair[0], pair[1]);
            using var request = JsonDocument.Parse(schema.Request);
            Assert.Equal("object", request.RootElement.GetProperty("type").GetString());
        }
    }

    [Fact]
    public void Names_ClosedGenericsByTypeArguments_AndPutsStringEnumsInDefinitions()
    {
        var (compile, generator, generated) = Run(Source);
        var text = generated.Replace("\"\"", "\"", StringComparison.Ordinal);

        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("global::Demo.App.PagedJsonContext.Default.PageResultUserResponse", generated, StringComparison.Ordinal);

        // 封闭泛型：根 title 带实参名，两个 PageResult<T> 各是一份 Schema，列表项分别引用各自的实参类型
        Assert.Contains("\"title\":\"PageResultOfUserResponse\",\"type\":\"object\",\"properties\":{\"items\":{\"type\":\"array\",\"items\":{\"$ref\":\"#/$defs/UserResponse\"}},\"total\":{\"type\":\"integer\",\"format\":\"int64\"}},\"required\":[\"items\",\"total\"]", text, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"PageResultOfRoleResponse\",\"type\":\"object\",\"properties\":{\"items\":{\"type\":\"array\",\"items\":{\"$ref\":\"#/$defs/RoleResponse\"}}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"title\":\"PageResult\"", text, StringComparison.Ordinal);
        // 嵌套泛型递归、多个实参以 And 连接
        Assert.Contains("\"title\":\"EnvelopeOfListOfUserResponseAndInt32\",\"type\":\"object\",\"properties\":{\"body\":{\"type\":\"array\",\"items\":{\"$ref\":\"#/$defs/UserResponse\"}},\"meta\":{\"type\":\"integer\",\"format\":\"int32\"}}", text, StringComparison.Ordinal);

        // 字符串枚举进 $defs/<枚举类型名>，属性以 $ref 引用，可空为 anyOf 带 null；枚举定义先于引用它的对象定义写出
        Assert.Contains("\"$defs\":{\"UserStatus\":{\"type\":\"string\",\"enum\":[\"Active\",\"Disabled\"]},\"UserResponse\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\",\"format\":\"uuid\"},\"status\":{\"$ref\":\"#/$defs/UserStatus\"},\"previousStatus\":{\"anyOf\":[{\"$ref\":\"#/$defs/UserStatus\"},{\"type\":\"null\"}]}},\"required\":[\"id\",\"status\"]}}", text, StringComparison.Ordinal);
        // 请求里的可空枚举同样引用定义；没有必填属性时没有 required
        Assert.Contains("\"title\":\"PageQuery\",\"type\":\"object\",\"properties\":{\"page\":{\"type\":\"integer\",\"format\":\"int32\"},\"status\":{\"anyOf\":[{\"$ref\":\"#/$defs/UserStatus\"},{\"type\":\"null\"}]}},\"$defs\":{\"UserStatus\":{\"type\":\"string\",\"enum\":[\"Active\",\"Disabled\"]}}", text, StringComparison.Ordinal);
        // 不按字符串序列化的上下文里枚举仍是内联整数
        Assert.Contains("\"status\":{\"type\":\"integer\",\"format\":\"int32\"}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OrderStatus\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_MissingInterface_MissingContext_AndInvalidId()
    {
        var (_, generator, generated) = Run("""
            using System.Text.Json.Serialization;
            using XiHan.Framework.Kernel.Operations;
            namespace Demo.App;
            public sealed record Ping;
            [Operation("pings.create")] public sealed class NotAnOperation { }
            [Operation("Pings.Create")] public sealed class BadId : IOperation<Ping, Ping> { public System.Threading.Tasks.ValueTask<XiHan.Framework.Primitives.Result<Ping>> HandleAsync(Ping request, XiHan.Framework.Primitives.InvocationContext context) => throw new System.NotImplementedException(); }
            [Operation("pings.get")] public sealed class Unregistered : IOperation<Ping, Ping> { public System.Threading.Tasks.ValueTask<XiHan.Framework.Primitives.Result<Ping>> HandleAsync(Ping request, XiHan.Framework.Primitives.InvocationContext context) => throw new System.NotImplementedException(); }
            """);

        Assert.Equal(["XHG001", "XHG002", "XHG003"], generator.Select(static d => d.Id).OrderBy(static id => id, StringComparer.Ordinal));
        Assert.Empty(generated);
    }

    [Fact]
    public void FallsBackToAssemblyName_WhenRootNamespaceIsMissing()
    {
        var (compile, generator, generated) = Run(Source, rootNamespace: "");

        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("namespace Demo.App;", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NamingPolicies_MatchSystemTextJson()
    {
        Assert.Equal("orderLines", NamingPolicies.Apply(JsonNamingPolicyKind.CamelCase, "OrderLines"));
        Assert.Equal("ioStream", NamingPolicies.Apply(JsonNamingPolicyKind.CamelCase, "IOStream"));
        Assert.Equal("order_lines", NamingPolicies.Apply(JsonNamingPolicyKind.SnakeCaseLower, "OrderLines"));
        Assert.Equal("ORDER-LINES", NamingPolicies.Apply(JsonNamingPolicyKind.KebabCaseUpper, "OrderLines"));
        Assert.Equal("OrderLines", NamingPolicies.Apply(JsonNamingPolicyKind.Unspecified, "OrderLines"));
    }
}
