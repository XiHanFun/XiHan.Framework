// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Kernel.Generators.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Validation;

namespace XiHan.Framework.Kernel.Generators.Tests;

/// <summary>
/// 操作源生成器
/// </summary>
/// <remarks>
/// 登记代码、JSON Schema 与诊断。生成的代码必须能在带内核引用的编译里通过编译
/// </remarks>
public sealed class OperationGeneratorTests
{
    /// <summary>
    /// 一个带嵌套类型、可空、默认值、集合与枚举的操作
    /// </summary>
    /// <remarks>
    /// 以及手写的序列化上下文桩（生成器只看特性，不需要 STJ 生成器真的运行）
    /// </remarks>
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
    /// 运行生成器并把生成的代码编回编译
    /// </summary>
    /// <remarks>
    /// 返回编译诊断、生成器诊断与生成的源文本
    /// </remarks>
    private static (ImmutableArray<Diagnostic> Compile, ImmutableArray<Diagnostic> Generator, string Generated) Run(string source, string rootNamespace = "Demo.App", IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null)
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
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
                .WithSpecificDiagnosticOptions(diagnosticOptions ?? ImmutableDictionary<string, ReportDiagnostic>.Empty));

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

    /// <summary>
    /// 程序集缺省与只读声明的源码
    /// </summary>
    /// <remarks>
    /// <c>[assembly: OperationDefaults]</c> 行由占位符替换，三个操作分别没写 Expose、显式写 Expose、声明只读
    /// </remarks>
    private const string DefaultsSource = """
        using System;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using System.Threading.Tasks;
        using XiHan.Framework.Kernel.Operations;
        using XiHan.Framework.Primitives;

        ASSEMBLY_DEFAULTS

        namespace Demo.App;

        public sealed record Ping(string Name);

        [Operation("pings.create")]
        public sealed class CreatePing : IOperation<Ping, Ping>
        {
            public ValueTask<Result<Ping>> HandleAsync(Ping request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("pings.get", Expose = Expose.Http | Expose.Mcp)]
        public sealed class GetPing : IOperation<Ping, Ping>
        {
            public ValueTask<Result<Ping>> HandleAsync(Ping request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("pings.tree", ReadOnly = true)]
        public sealed class TreePing : IOperation<Ping, Ping>
        {
            public ValueTask<Result<Ping>> HandleAsync(Ping request, InvocationContext context) => throw new NotImplementedException();
        }

        [JsonSerializable(typeof(Ping))]
        internal sealed class PingJsonContext : JsonSerializerContext
        {
            public static PingJsonContext Default { get; } = new();
            public PingJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<Ping> Ping => null!;
        }
        """;

    /// <summary>
    /// 生成代码里某个操作的契约构造表达式前缀
    /// </summary>
    private static string ContractOf(string id, int expose)
        => $"new global::XiHan.Framework.Kernel.Operations.OperationContract(@\"{id}\", (global::XiHan.Framework.Primitives.Stability)0, (global::XiHan.Framework.Kernel.Operations.Expose){expose}, null, null";

    [Fact]
    public void AssemblyDefaults_ApplyToOperationsWithoutExpose_AndReadOnlyIsEmitted()
    {
        var (compile, generator, generated) = Run(DefaultsSource.Replace("ASSEMBLY_DEFAULTS", "[assembly: OperationDefaults(Expose = Expose.Http)]", StringComparison.Ordinal));

        Assert.Empty(generator);
        Assert.Empty(compile);
        // 没写 Expose 的取程序集缺省（Http = 1），显式写的不受影响（Http | Mcp = 5），只读声明原样进契约
        Assert.Contains(ContractOf("pings.create", 1) + "),", generated, StringComparison.Ordinal);
        Assert.Contains(ContractOf("pings.get", 5) + "),", generated, StringComparison.Ordinal);
        Assert.Contains(ContractOf("pings.tree", 1) + ", readOnly: true),", generated, StringComparison.Ordinal);

        // 没有程序集缺省时没写 Expose 的仍是全部协议（31）
        var (_, _, withoutDefaults) = Run(DefaultsSource.Replace("ASSEMBLY_DEFAULTS", string.Empty, StringComparison.Ordinal));
        Assert.Contains(ContractOf("pings.create", 31) + "),", withoutDefaults, StringComparison.Ordinal);
        // 写了特性却没写 Expose：缺省就是全部协议
        var (_, _, bareDefaults) = Run(DefaultsSource.Replace("ASSEMBLY_DEFAULTS", "[assembly: OperationDefaults]", StringComparison.Ordinal));
        Assert.Contains(ContractOf("pings.create", 31) + "),", bareDefaults, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplicitExposeAll_IsOptIn_AndSilencedByAssemblyDefaults()
    {
        var enabled = new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal) { ["XHG004"] = ReportDiagnostic.Error };
        var withoutDefaults = DefaultsSource.Replace("ASSEMBLY_DEFAULTS", string.Empty, StringComparison.Ordinal);

        // 缺省关闭：不打开就不报
        Assert.Empty(Run(withoutDefaults).Generator);

        // 打开后，没写 Expose 又没有程序集缺省的操作逐个报告；显式写了的不报
        var reported = Run(withoutDefaults, diagnosticOptions: enabled).Generator;
        Assert.All(reported, static d => Assert.Equal(("XHG004", DiagnosticSeverity.Error), (d.Id, d.Severity)));
        Assert.Equal(["pings.create", "pings.tree"], reported.Select(static d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Split('\'')[1]).OrderBy(static id => id, StringComparer.Ordinal));

        // 声明了程序集缺省就没有隐式的全部协议，打开了也不报
        Assert.Empty(Run(DefaultsSource.Replace("ASSEMBLY_DEFAULTS", "[assembly: OperationDefaults(Expose = Expose.Http)]", StringComparison.Ordinal), diagnosticOptions: enabled).Generator);
    }

    [Fact]
    public void ReadOnly_OnWriteVerb_IsReported()
    {
        var (_, generator, generated) = Run("""
            using System.Text.Json.Serialization;
            using XiHan.Framework.Kernel.Operations;
            namespace Demo.App;
            public sealed record Ping;
            [Operation("pings.delete", ReadOnly = true)] public sealed class DeletePing : IOperation<Ping, Ping> { public System.Threading.Tasks.ValueTask<XiHan.Framework.Primitives.Result<Ping>> HandleAsync(Ping request, XiHan.Framework.Primitives.InvocationContext context) => throw new System.NotImplementedException(); }
            """);

        // 写操作的动词声明只读是编译期错误，该操作不生成登记代码（契约构造本来也会在启动时抛出）
        Assert.Equal(["XHG005"], generator.Select(static d => d.Id));
        Assert.Empty(generated);
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
    public void Descriptions_ComeFromXmlDocumentation()
    {
        const string documented = """
            using System;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Text.Json.Serialization.Metadata;
            using System.Threading.Tasks;
            using XiHan.Framework.Kernel.Operations;
            using XiHan.Framework.Primitives;

            namespace Demo.App;

            /// <summary>
            /// 订单状态。
            /// </summary>
            public enum NoteStatus { Open, Closed }

            /// <summary>
            /// 备注行，见 <see cref="T:Demo.App.NoteRequest"/>。
            /// </summary>
            public sealed class NoteLine
            {
                /// <summary>
                /// 行文本，可为 <see langword="null"/>。
                /// </summary>
                public string? Text { get; set; }
            }

            /// <summary>
            /// 添加备注。
            /// </summary>
            /// <remarks>
            /// 细节不进 Schema。
            /// </remarks>
            /// <param name="OrderId">订单 ID，与 <paramref name="Line"/> 一起提交。</param>
            /// <param name="Line">备注行。</param>
            public sealed record NoteRequest(Guid OrderId, NoteLine Line, NoteStatus Status, int Undocumented);

            [Operation("notes.create")]
            public sealed class CreateNote : IOperation<NoteRequest, NoteRequest>
            {
                public ValueTask<Result<NoteRequest>> HandleAsync(NoteRequest request, InvocationContext context) => throw new NotImplementedException();
            }

            [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
            [JsonSerializable(typeof(NoteRequest))]
            internal sealed class NoteJsonContext : JsonSerializerContext
            {
                public static NoteJsonContext Default { get; } = new();
                public NoteJsonContext() : base(null) { }
                protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
                public override JsonTypeInfo? GetTypeInfo(Type type) => null;
                public JsonTypeInfo<NoteRequest> NoteRequest => null!;
            }
            """;
        var (compile, generator, generated) = Run(documented);
        var text = generated.Replace("\"\"", "\"", StringComparison.Ordinal);

        Assert.Empty(generator);
        Assert.Empty(compile);

        // 根取类型的 summary（remarks 不进），位置记录的属性取类型上的 param，cref 取最后一段、paramref 与 langword 取文本、空白折叠（中文换行处直接相连）
        Assert.Contains("\"title\":\"NoteRequest\",\"description\":\"添加备注。\",\"type\":\"object\"", text, StringComparison.Ordinal);
        Assert.Contains("\"orderId\":{\"description\":\"订单 ID，与 Line 一起提交。\",\"type\":\"string\",\"format\":\"uuid\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"line\":{\"description\":\"备注行。\",\"$ref\":\"#/$defs/NoteLine\"}", text, StringComparison.Ordinal);
        Assert.Contains("\"undocumented\":{\"type\":\"integer\",\"format\":\"int32\"}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("细节不进 Schema", text, StringComparison.Ordinal);

        // $defs 里的对象与字符串枚举取类型的 summary，属性取属性的 summary
        Assert.Contains("\"NoteLine\":{\"description\":\"备注行，见 NoteRequest。\",\"type\":\"object\",\"properties\":{\"text\":{\"description\":\"行文本，可为 null。\",\"type\":[\"string\",\"null\"]}}}", text, StringComparison.Ordinal);
        Assert.Contains("\"NoteStatus\":{\"description\":\"订单状态。\",\"type\":\"string\",\"enum\":[\"Open\",\"Closed\"]}", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 成员是任意 JSON（<c>JsonNode</c> 系列、<c>JsonElement</c> 与 <c>JsonDocument</c>）的操作
    /// </summary>
    /// <remarks>
    /// 可空与不可空、别名、字典与集合里的节点都有；<c>JsonObject</c> / <c>JsonArray</c> 的元素类型在 System.Text.Json 的元数据里是可空标注的 <c>JsonNode?</c>。<c>Uri?</c> 是同一类可空标注的基元
    /// </remarks>
    private const string DynamicJsonSource = """
        using System;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using System.Threading.Tasks;
        using XiHan.Framework.Kernel.Operations;
        using XiHan.Framework.Primitives;
        using JsonBag = System.Text.Json.Nodes.JsonObject;

        namespace Demo.App;

        public sealed record DynamicRequest(
            JsonNode Node,
            JsonNode? OptionalNode,
            JsonObject Object,
            JsonObject? OptionalObject,
            JsonArray Array,
            JsonArray? OptionalArray,
            JsonValue Value,
            IDictionary<string, JsonNode?> Bag,
            IReadOnlyDictionary<string, JsonElement> Elements,
            JsonElement Element,
            JsonElement? OptionalElement,
            Uri Link,
            Uri? OptionalLink,
            JsonBag Aliased,
            Dictionary<string, JsonArray?> Groups,
            JsonDocument? Document);

        public sealed record DynamicResponse(JsonObject Data, List<JsonNode?> Items);

        [Operation("dynamics.create")]
        public sealed class CreateDynamic : IOperation<DynamicRequest, DynamicResponse>
        {
            public ValueTask<Result<DynamicResponse>> HandleAsync(DynamicRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(DynamicRequest))]
        [JsonSerializable(typeof(DynamicResponse))]
        internal sealed class DynamicJsonContext : JsonSerializerContext
        {
            public static DynamicJsonContext Default { get; } = new();
            public DynamicJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<DynamicRequest> DynamicRequest => null!;
            public JsonTypeInfo<DynamicResponse> DynamicResponse => null!;
        }
        """;

    /// <summary>
    /// 取出生成代码里每个操作的请求与响应 Schema 文本
    /// </summary>
    private static List<(string Request, string Response)> SchemasOf(string generated)
        => CSharpSyntaxTree.ParseText(generated).GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(static c => c.Type.ToString().EndsWith("OperationSchema", StringComparison.Ordinal))
            .Select(static c => c.ArgumentList!.Arguments.Select(static a => ((LiteralExpressionSyntax)a.Expression).Token.ValueText).ToArray())
            .Select(static pair => (pair[0], pair[1]))
            .ToList();

    /// <summary>
    /// 断言 Schema 的每个属性都是期望的 JSON 片段
    /// </summary>
    private static void AssertProperties(JsonElement schema, IReadOnlyDictionary<string, string> expected)
    {
        var properties = schema.GetProperty("properties");
        Assert.Equal(expected.Keys, properties.EnumerateObject().Select(static p => p.Name));
        foreach (var property in properties.EnumerateObject())
        {
            Assert.True(expected[property.Name] == property.Value.GetRawText(), $"Property '{property.Name}' is {property.Value.GetRawText()}, expected {expected[property.Name]}.");
        }
    }

    [Fact]
    public void DynamicJson_IsAnyValue_ObjectOrArray_RegardlessOfNullableAnnotations()
    {
        var (compile, generator, generated) = Run(DynamicJsonSource);

        Assert.Empty(generator);
        Assert.Empty(compile);
        var (requestText, responseText) = Assert.Single(SchemasOf(generated));
        using var request = JsonDocument.Parse(requestText);
        using var response = JsonDocument.Parse(responseText);

        // JsonNode / JsonValue / JsonElement / JsonDocument 是任意值（{}，可空也不改变形状）；JsonObject 与键为字符串的节点字典是 object + 任意值的 additionalProperties，
        // JsonArray 是 array + 任意值的 items，可空的只在 type 里加 null；Uri? 仍是 uri 格式的字符串。节点类型不展开成对象，也不进 $defs
        AssertProperties(request.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["node"] = "{}",
            ["optionalNode"] = "{}",
            ["object"] = "{\"type\":\"object\",\"additionalProperties\":{}}",
            ["optionalObject"] = "{\"type\":[\"object\",\"null\"],\"additionalProperties\":{}}",
            ["array"] = "{\"type\":\"array\",\"items\":{}}",
            ["optionalArray"] = "{\"type\":[\"array\",\"null\"],\"items\":{}}",
            ["value"] = "{}",
            ["bag"] = "{\"type\":\"object\",\"additionalProperties\":{}}",
            ["elements"] = "{\"type\":\"object\",\"additionalProperties\":{}}",
            ["element"] = "{}",
            ["optionalElement"] = "{}",
            ["link"] = "{\"type\":\"string\",\"format\":\"uri\"}",
            ["optionalLink"] = "{\"type\":[\"string\",\"null\"],\"format\":\"uri\"}",
            ["aliased"] = "{\"type\":\"object\",\"additionalProperties\":{}}",
            ["groups"] = "{\"type\":\"object\",\"additionalProperties\":{\"type\":[\"array\",\"null\"],\"items\":{}}}",
            ["document"] = "{}",
        });
        Assert.Equal(["node", "object", "array", "value", "bag", "elements", "element", "link", "aliased", "groups"], request.RootElement.GetProperty("required").EnumerateArray().Select(static e => e.GetString()));
        Assert.False(request.RootElement.TryGetProperty("$defs", out _), requestText);

        AssertProperties(response.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data"] = "{\"type\":\"object\",\"additionalProperties\":{}}",
            ["items"] = "{\"type\":\"array\",\"items\":{}}",
        });
        Assert.False(response.RootElement.TryGetProperty("$defs", out _), responseText);

        // 内核的 Validate 阶段按这份 Schema 接受任意 JSON 值，只约束 JsonObject 是对象、JsonArray 是数组
        using var valid = JsonDocument.Parse("""
            {"node":{"a":[1,"x",null]},"optionalNode":null,"object":{"k":{"deep":true},"n":null},"optionalObject":null,"array":[1,"two",{"three":3},null],"optionalArray":null,
             "value":42,"bag":{"x":"y"},"elements":{"e":[1]},"element":"text","optionalElement":null,"link":"https://example.com/","optionalLink":null,"aliased":{},"groups":{"g":[1,{"a":1}],"h":null},"document":{"any":[1]}}
            """);
        Assert.Empty(JsonSchemaValidator.Validate(request.RootElement, valid.RootElement));

        using var invalid = JsonDocument.Parse("""
            {"node":1,"object":[1],"array":{"a":1},"value":"v","bag":{},"elements":{},"element":true,"link":"https://example.com/","aliased":{},"groups":{}}
            """);
        Assert.Equal(["$.array", "$.object"], JsonSchemaValidator.Validate(request.RootElement, invalid.RootElement).Select(static v => v.Path).OrderBy(static p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void LookalikeTypes_InOtherNamespaces_AreObjects()
    {
        var (compile, generator, generated) = Run("""
            using System;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Text.Json.Serialization.Metadata;
            using System.Threading.Tasks;
            using XiHan.Framework.Kernel.Operations;
            using XiHan.Framework.Primitives;

            namespace Demo.System.Text.Json.Nodes
            {
                public sealed class JsonNode
                {
                    public string Name { get; set; } = "";
                }
            }

            namespace Demo.Lookalike
            {
                public sealed class Uri
                {
                    public string Href { get; set; } = "";
                }
            }

            namespace Demo.App
            {
                public sealed record LookalikeRequest(Demo.System.Text.Json.Nodes.JsonNode? Node, Demo.Lookalike.Uri Link);

                [Operation("lookalikes.create")]
                public sealed class CreateLookalike : IOperation<LookalikeRequest, LookalikeRequest>
                {
                    public ValueTask<Result<LookalikeRequest>> HandleAsync(LookalikeRequest request, InvocationContext context) => throw new NotImplementedException();
                }

                [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
                [JsonSerializable(typeof(LookalikeRequest))]
                internal sealed class LookalikeJsonContext : JsonSerializerContext
                {
                    public static LookalikeJsonContext Default { get; } = new();
                    public LookalikeJsonContext() : base(null) { }
                    protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
                    public override JsonTypeInfo? GetTypeInfo(Type type) => null;
                    public JsonTypeInfo<LookalikeRequest> LookalikeRequest => null!;
                }
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compile);
        using var request = JsonDocument.Parse(Assert.Single(SchemasOf(generated)).Request);

        // 识别按完整命名空间：别的命名空间里同名的 JsonNode / Uri 是普通对象，进 $defs
        AssertProperties(request.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["node"] = "{\"anyOf\":[{\"$ref\":\"#/$defs/JsonNode\"},{\"type\":\"null\"}]}",
            ["link"] = "{\"$ref\":\"#/$defs/Uri\"}",
        });
        var definitions = request.RootElement.GetProperty("$defs");
        Assert.Equal("{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}", definitions.GetProperty("JsonNode").GetRawText());
        Assert.Equal("{\"type\":\"object\",\"properties\":{\"href\":{\"type\":\"string\"}}}", definitions.GetProperty("Uri").GetRawText());
    }

    [Fact]
    public void JsonAttributes_AreRecognized_IncludingAliases()
    {
        var (compile, generator, generated) = Run("""
            using System;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Text.Json.Serialization.Metadata;
            using System.Threading.Tasks;
            using XiHan.Framework.Kernel.Operations;
            using XiHan.Framework.Primitives;
            using Ignore = System.Text.Json.Serialization.JsonIgnoreAttribute;

            namespace Demo.App;

            public sealed class Renamed
            {
                [JsonPropertyName("display_name")]
                public string? DisplayName { get; set; }

                [JsonIgnore]
                public string? Secret { get; set; }

                [Ignore]
                public string? Aliased { get; set; }

                public int Kept { get; set; }
            }

            [Operation("renames.create")]
            public sealed class CreateRename : IOperation<Renamed, Renamed>
            {
                public ValueTask<Result<Renamed>> HandleAsync(Renamed request, InvocationContext context) => throw new NotImplementedException();
            }

            [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
            [JsonSerializable(typeof(Renamed))]
            internal sealed class RenameJsonContext : JsonSerializerContext
            {
                public static RenameJsonContext Default { get; } = new();
                public RenameJsonContext() : base(null) { }
                protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
                public override JsonTypeInfo? GetTypeInfo(Type type) => null;
                public JsonTypeInfo<Renamed> Renamed => null!;
            }
            """);

        Assert.Empty(generator);
        Assert.Empty(compile);
        using var request = JsonDocument.Parse(Assert.Single(SchemasOf(generated)).Request);

        // [JsonPropertyName] 覆盖命名策略，[JsonIgnore]（含别名写法）的属性不进 Schema
        AssertProperties(request.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["display_name"] = "{\"type\":[\"string\",\"null\"]}",
            ["kept"] = "{\"type\":\"integer\",\"format\":\"int32\"}",
        });
    }

    /// <summary>
    /// 按根 <c>title</c> 取出某个操作的请求 Schema
    /// </summary>
    private static JsonDocument RequestSchemaTitled(string generated, string title)
        => SchemasOf(generated).Select(static pair => JsonDocument.Parse(pair.Request)).Single(document => document.RootElement.GetProperty("title").GetString() == title);

    /// <summary>
    /// 带各种 <c>[JsonIgnore]</c> 条件与上下文级 <c>DefaultIgnoreCondition</c> 的操作
    /// </summary>
    /// <remarks>
    /// <c>Always</c>（含不写条件）不序列化；<c>WhenWritingDefault</c> / <c>WhenWritingNull</c> 只在值为默认值 / null 时不写出；<c>WhenWriting</c> 只读不写、<c>WhenReading</c> 只写不读；<c>Never</c> 总是序列化，覆盖上下文的缺省
    /// </remarks>
    private const string IgnoreSource = """
        using System;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using System.Threading.Tasks;
        using XiHan.Framework.Kernel.Operations;
        using XiHan.Framework.Primitives;

        namespace Demo.App;

        public sealed class IgnoreRequest
        {
            [JsonIgnore] public string Hidden { get; init; } = "";
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)] public string AlsoHidden { get; init; } = "";
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public required int Count { get; init; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public required string Label { get; init; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public required string Note { get; init; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Maybe { get; init; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)] public required string Secret { get; init; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)] public required string Computed { get; init; }
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public required string Pinned { get; init; }
            public required string Plain { get; init; }
        }

        public sealed record DefaultsRequest(int Count, string Name, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int Pinned, bool Flag, string? Maybe);

        public sealed record NullsRequest(int Count, string Name, string? Maybe);

        [Operation("ignores.create")]
        public sealed class CreateIgnore : IOperation<IgnoreRequest, IgnoreRequest>
        {
            public ValueTask<Result<IgnoreRequest>> HandleAsync(IgnoreRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("defaults.create")]
        public sealed class CreateDefaults : IOperation<DefaultsRequest, DefaultsRequest>
        {
            public ValueTask<Result<DefaultsRequest>> HandleAsync(DefaultsRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("nulls.create")]
        public sealed class CreateNulls : IOperation<NullsRequest, NullsRequest>
        {
            public ValueTask<Result<NullsRequest>> HandleAsync(NullsRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(IgnoreRequest))]
        internal sealed class IgnoreJsonContext : JsonSerializerContext
        {
            public static IgnoreJsonContext Default { get; } = new();
            public IgnoreJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<IgnoreRequest> IgnoreRequest => null!;
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonSerializable(typeof(DefaultsRequest))]
        internal sealed class DefaultsJsonContext : JsonSerializerContext
        {
            public static DefaultsJsonContext Default { get; } = new();
            public DefaultsJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<DefaultsRequest> DefaultsRequest => null!;
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonSerializable(typeof(NullsRequest))]
        internal sealed class NullsJsonContext : JsonSerializerContext
        {
            public static NullsJsonContext Default { get; } = new();
            public NullsJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<NullsRequest> NullsRequest => null!;
        }
        """;

    [Fact]
    public void JsonIgnoreConditions_FollowSystemTextJson()
    {
        var (compile, generator, generated) = Run(IgnoreSource);

        Assert.Empty(generator);
        Assert.Empty(compile);

        // 只有 Always（含不写条件）的属性不进 Schema；条件忽略的保留：WhenWriting / WhenReading 总有一个方向没有它，不必填；
        // WhenWritingDefault 的值类型取默认值时不写出，不必填，不可空的引用按可空契约不会是 null，照常必填；WhenWritingNull 同理只影响可空的属性；Never 照常
        using var ignore = RequestSchemaTitled(generated, "IgnoreRequest");
        AssertProperties(ignore.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["count"] = "{\"type\":\"integer\",\"format\":\"int32\"}",
            ["label"] = "{\"type\":\"string\"}",
            ["note"] = "{\"type\":\"string\"}",
            ["maybe"] = "{\"type\":[\"string\",\"null\"]}",
            ["secret"] = "{\"type\":\"string\"}",
            ["computed"] = "{\"type\":\"string\"}",
            ["pinned"] = "{\"type\":\"string\"}",
            ["plain"] = "{\"type\":\"string\"}",
        });
        Assert.Equal(["label", "note", "pinned", "plain"], ignore.RootElement.GetProperty("required").EnumerateArray().Select(static e => e.GetString()));

        // 上下文的 DefaultIgnoreCondition = WhenWritingDefault：没有 [JsonIgnore] 的值类型属性取默认值时不写出，不必填；[JsonIgnore(Condition = Never)] 覆盖缺省
        using var defaults = RequestSchemaTitled(generated, "DefaultsRequest");
        Assert.Equal(["count", "name", "pinned", "flag", "maybe"], defaults.RootElement.GetProperty("properties").EnumerateObject().Select(static p => p.Name));
        Assert.Equal(["name", "pinned"], defaults.RootElement.GetProperty("required").EnumerateArray().Select(static e => e.GetString()));

        // DefaultIgnoreCondition = WhenWritingNull 只省略 null：不可空的值类型与引用照常必填
        using var nulls = RequestSchemaTitled(generated, "NullsRequest");
        Assert.Equal(["count", "name"], nulls.RootElement.GetProperty("required").EnumerateArray().Select(static e => e.GetString()));
    }

    /// <summary>
    /// 键不是字符串的字典
    /// </summary>
    /// <remarks>
    /// System.Text.Json 把受支持的键写成 JSON 对象的属性名：整数与浮点写成数字文本，<c>bool</c> 写成 <c>True</c> / <c>False</c>，枚举写成成员名（按上下文的 <c>DictionaryKeyPolicy</c> 改写），<c>Guid</c> 与日期时间写成对应格式的文本。不受支持的键（这里是记录与可空值类型）在运行期抛出 <c>NotSupportedException</c>
    /// </remarks>
    private const string KeyedSource = """
        using System;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using System.Threading.Tasks;
        using XiHan.Framework.Kernel.Operations;
        using XiHan.Framework.Primitives;

        namespace Demo.App;

        public enum Color { Red, Green }

        public enum Tone { DeepRed, LightGreen }

        public sealed record Line(string Sku);

        public sealed record Point(int X, int Y);

        [JsonConverter(typeof(SlugConverter))]
        public readonly record struct Slug(string Value);

        public sealed class SlugConverter : JsonConverter<Slug>
        {
            public override Slug Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString()!);
            public override void Write(Utf8JsonWriter writer, Slug value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
        }

        public sealed record KeyedRequest(
            Dictionary<int, string> ByInt,
            IReadOnlyDictionary<long, int> ByLong,
            Dictionary<uint, int> ByUnsigned,
            Dictionary<Guid, string>? ByGuid,
            Dictionary<Color, int> ByColor,
            Dictionary<bool, int> ByFlag,
            Dictionary<DateOnly, long> ByDay,
            Dictionary<DateTimeOffset, int> ByMoment,
            Dictionary<decimal, int> ByPrice,
            Dictionary<char, int> ByChar,
            SortedDictionary<int, Line> Lines,
            Dictionary<string, int> ByName,
            Dictionary<Slug, int> BySlug);

        public sealed record TonedRequest(Dictionary<Tone, int> ByTone, Dictionary<string, int> ByName);

        public sealed record UnsupportedRequest(Dictionary<Point, int> ByPoint, Dictionary<Point, string> AgainByPoint);

        [Operation("keyed.create")]
        public sealed class CreateKeyed : IOperation<KeyedRequest, KeyedRequest>
        {
            public ValueTask<Result<KeyedRequest>> HandleAsync(KeyedRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("toned.create")]
        public sealed class CreateToned : IOperation<TonedRequest, TonedRequest>
        {
            public ValueTask<Result<TonedRequest>> HandleAsync(TonedRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [Operation("unsupported.create")]
        public sealed class CreateUnsupported : IOperation<UnsupportedRequest, UnsupportedRequest>
        {
            public ValueTask<Result<UnsupportedRequest>> HandleAsync(UnsupportedRequest request, InvocationContext context) => throw new NotImplementedException();
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(KeyedRequest))]
        [JsonSerializable(typeof(UnsupportedRequest))]
        internal sealed class KeyedJsonContext : JsonSerializerContext
        {
            public static KeyedJsonContext Default { get; } = new();
            public KeyedJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<KeyedRequest> KeyedRequest => null!;
            public JsonTypeInfo<UnsupportedRequest> UnsupportedRequest => null!;
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DictionaryKeyPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
        [JsonSerializable(typeof(TonedRequest))]
        internal sealed class TonedJsonContext : JsonSerializerContext
        {
            public static TonedJsonContext Default { get; } = new();
            public TonedJsonContext() : base(null) { }
            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
            public JsonTypeInfo<TonedRequest> TonedRequest => null!;
        }
        """;

    [Fact]
    public void NonStringDictionaryKeys_AreObjects_WithKeyFormatsInPropertyNames()
    {
        var (compile, generator, generated) = Run(KeyedSource);

        Assert.Empty(compile);

        // 与 System.Text.Json 写出的形状一致：对象 + additionalProperties，键的文本形式写在 propertyNames；字符串键与自带转换器的键没有 propertyNames
        using var keyed = RequestSchemaTitled(generated, "KeyedRequest");
        AssertProperties(keyed.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["byInt"] = """{"type":"object","propertyNames":{"pattern":"^-?[0-9]+$"},"additionalProperties":{"type":"string"}}""",
            ["byLong"] = """{"type":"object","propertyNames":{"pattern":"^-?[0-9]+$"},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byUnsigned"] = """{"type":"object","propertyNames":{"pattern":"^[0-9]+$"},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byGuid"] = """{"type":["object","null"],"propertyNames":{"format":"uuid"},"additionalProperties":{"type":"string"}}""",
            ["byColor"] = """{"type":"object","propertyNames":{"enum":["Red","Green"]},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byFlag"] = """{"type":"object","propertyNames":{"enum":["True","False"]},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byDay"] = """{"type":"object","propertyNames":{"format":"date"},"additionalProperties":{"type":"integer","format":"int64"}}""",
            ["byMoment"] = """{"type":"object","propertyNames":{"format":"date-time"},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byPrice"] = """{"type":"object","propertyNames":{"pattern":"^-?[0-9]+(\\.[0-9]+)?([eE][+-]?[0-9]+)?$"},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byChar"] = """{"type":"object","propertyNames":{"minLength":1,"maxLength":1},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["lines"] = """{"type":"object","propertyNames":{"pattern":"^-?[0-9]+$"},"additionalProperties":{"$ref":"#/$defs/Line"}}""",
            ["byName"] = """{"type":"object","additionalProperties":{"type":"integer","format":"int32"}}""",
            ["bySlug"] = """{"type":"object","additionalProperties":{"type":"integer","format":"int32"}}""",
        });
        Assert.DoesNotContain("KeyValuePair", keyed.RootElement.GetRawText(), StringComparison.Ordinal);

        // 枚举键按上下文的 DictionaryKeyPolicy 改写成员名；字符串键不受影响
        using var toned = RequestSchemaTitled(generated, "TonedRequest");
        AssertProperties(toned.RootElement, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["byTone"] = """{"type":"object","propertyNames":{"enum":["deep_red","light_green"]},"additionalProperties":{"type":"integer","format":"int32"}}""",
            ["byName"] = """{"type":"object","additionalProperties":{"type":"integer","format":"int32"}}""",
        });

        // 不受支持的键：形状照写（对象，不写 propertyNames），每个操作每种键类型报一次 XHG006，代码照常生成
        using var unsupported = RequestSchemaTitled(generated, "UnsupportedRequest");
        Assert.Equal("""{"type":"object","additionalProperties":{"type":"integer","format":"int32"}}""", unsupported.RootElement.GetProperty("properties").GetProperty("byPoint").GetRawText());
        var warning = Assert.Single(generator);
        Assert.Equal(("XHG006", DiagnosticSeverity.Warning), (warning.Id, warning.Severity));
        Assert.Contains("'Demo.App.Point'", warning.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("'unsupported.create'", warning.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("context.AddCreateUnsupported();", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void KeywordNamespacesAndTypeNames_AreEscapedInGeneratedCode()
    {
        var (compile, generator, generated) = Run("""
            using System;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Text.Json.Serialization.Metadata;
            using System.Threading.Tasks;
            using XiHan.Framework.Kernel.Operations;
            using XiHan.Framework.Primitives;

            namespace Demo.@event;

            public sealed record @class(string Name);

            [Operation("classes.create")]
            public sealed class @static : IOperation<@class, @class>
            {
                public ValueTask<Result<@class>> HandleAsync(@class request, InvocationContext context) => throw new NotImplementedException();
            }

            [JsonSerializable(typeof(@class))]
            internal sealed class @namespace : JsonSerializerContext
            {
                public static @namespace Default { get; } = new();
                public @namespace() : base(null) { }
                protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
                public override JsonTypeInfo? GetTypeInfo(Type type) => null;
                public JsonTypeInfo<@class> @class => null!;
            }
            """, rootNamespace: "Demo.event");

        // 命名空间、类型名与序列化上下文的属性名是 C# 关键字时加 @，生成的代码能编译
        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("namespace Demo.@event;", generated, StringComparison.Ordinal);
        Assert.Contains("context.AddOperation<global::Demo.@event.@static, global::Demo.@event.@class, global::Demo.@event.@class>(", generated, StringComparison.Ordinal);
        Assert.Contains("global::Demo.@event.@namespace.Default.@class,", generated, StringComparison.Ordinal);
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
