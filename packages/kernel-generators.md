来源：https://framework.docs.xihanfun.com/packages/kernel-generators

# XiHan.Framework.Kernel.Generators

Roslyn 源生成器：扫描标注 `[Operation]` 且实现 `IOperation<TRequest, TResponse>` 的类，生成操作登记代码 `GeneratedOperations` 与每个操作的请求 / 响应 JSON Schema。HTTP 的 OpenAPI、MCP 的 tool 描述、gRPC 的 protobuf 描述与内核 Validate 阶段的契约校验都用这份 Schema，适用于每个声明操作契约的项目。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Kernel.Generators` |
| 环层 | 工具链 |
| 能力 | — |
| 特性入口 | —（生成的扩展方法 `AddGeneratedOperations(this FeatureContext)`） |
| 配置节 | —（输出命名空间取 MSBuild 属性 `RootNamespace`） |
| 依赖 | 只在构建期使用 `Microsoft.CodeAnalysis.CSharp`；目标 `netstandard2.0`，开发依赖；生成的代码引用 `XiHan.Framework.Kernel` |
| AOT | 生成的代码 AOT 兼容（序列化元数据来自源生成的 `JsonSerializerContext`）；生成器本身是编译期组件 |
| 稳定性 | —（编译期组件；输入特性 `OperationAttribute` 为 Stable，`OperationDefaultsAttribute` 为 Preview） |

## 概述

内核零反射，运行期不读 `[Operation]`：契约要以 `OperationContract` 经 `FeatureContext.AddOperation<TOperation, TRequest, TResponse>(contract, requestInfo, responseInfo, schema)` 登记。生成器把这件事在编译期做掉，输出一个文件 `GeneratedOperations.g.cs`：

- 静态类 `GeneratedOperations`，命名空间是项目的 `RootNamespace`（没有时取程序集名）。
- 每个操作一个扩展方法 `Add<类名>(this FeatureContext)`（类名重复时依次加数字后缀），契约取自特性：ID、稳定性、`Expose`、`Policy` 或 `Permission`、`ReadOnly`；请求与响应的 `JsonTypeInfo` 取自项目里用 `[JsonSerializable]` 登记了该类型的 `JsonSerializerContext`。
- 登记全部操作的 `AddGeneratedOperations(this FeatureContext)`，在特性的 `Configure` 里调用一次。
- 每个操作的请求与响应 JSON Schema（draft 2020-12），以 `OperationSchema` 随登记放进 `OperationDescriptor.Schema`。

Schema 的生成规则：

| 来源 | 进 Schema 的方式 |
| --- | --- |
| 属性名 | 按序列化上下文的命名策略（如 `JsonKnownNamingPolicy.CamelCase`） |
| 可空性与 `required` | 按 C# 类型；嵌套对象进 `$defs` |
| 基元 | `type` / `format`：`Guid` → `uuid`，`DateTime` / `DateTimeOffset` → `date-time`，`DateOnly` → `date`，`TimeOnly` → `time`，`TimeSpan` → `duration`，`Uri` → `uri`，`byte[]` → `contentEncoding: base64`，整数与浮点按位宽取 `int32` / `int64` / `float` / `double` / `decimal` |
| 字典与集合 | 字典（`IDictionary<TKey, TValue>` / `IReadOnlyDictionary<TKey, TValue>`，任何键类型）为 `object` + `additionalProperties`，与 System.Text.Json 写出的一样是 JSON 对象；其他集合为 `array` + `items` |
| 非字符串的字典键 | 键的文本形式写在 `propertyNames`：有符号整数 `pattern: ^-?[0-9]+$`，无符号整数 `^[0-9]+$`，浮点与 `decimal` 为数字文本的 `pattern`，`bool` 为 `enum: ["True", "False"]`，`char` 为单个字符，枚举为成员名（按上下文的 `DictionaryKeyPolicy` 改写），`Guid`、日期时间与 `Uri` 为对应的 `format`；`object`、`Version` 与带 `[JsonConverter]` 的键不加约束。System.Text.Json 不支持的键类型报 `XHG006`，形状照写 |
| `[JsonIgnore]` 与 `DefaultIgnoreCondition` | 只有 `Always`（不写条件即是）的属性不进 Schema；`WhenWriting` / `WhenReading` 的属性与 `WhenWritingDefault` 的值类型属性留在 `properties` 里但不进 `required`；`WhenWritingNull` 与不可空引用的 `WhenWritingDefault` 按可空契约照常；没有 `[JsonIgnore]` 时取序列化上下文的 `DefaultIgnoreCondition`，`Condition = Never` 覆盖它 |
| 任意 JSON | `object`、`JsonElement`、`JsonDocument`、`JsonNode` 与 `JsonValue` 为不加约束的 `{}`；`JsonObject` 为 `object` + `additionalProperties: {}`，`JsonArray` 为 `array` + `items: {}`，不按属性展开 |
| 可空标注 | `JsonNode?`、`Uri?`、`int?` 等只在 `type` 里加 `null`（`$ref` 为 `anyOf` 带 `null`），不改变形状；类型按命名空间与元数据名识别，别名与泛型实参不影响 |
| 根 `title` 与定义名 | 类型名；封闭泛型为 `<外层名>Of<实参名>`，多个实参以 `And` 连接、嵌套递归，如 `PageResultOfUserResponse` |
| 按字符串序列化的枚举（`UseStringEnumConverter`） | 进 `$defs/<枚举类型名>`（`type: string` + `enum`）并以 `$ref` 引用，可空为 `anyOf` 带 `null`；整数枚举内联为 `integer` |
| `[Required]` | 进 `required` 并去掉可空；字符串加 `minLength: 1`（`AllowEmptyStrings = true` 除外） |
| `[Range]` | `minimum` / `maximum`，`MinimumIsExclusive` / `MaximumIsExclusive` 为开区间 |
| `[StringLength]`、`[MinLength]`、`[MaxLength]`、`[Length]` | 字符串的 `minLength` / `maxLength`，集合的 `minItems` / `maxItems` |
| `[RegularExpression]` | `pattern` |
| XML 文档 | 类型的 `<summary>` 进根与 `$defs` 的 `description`，属性的 `<summary>` 进属性（没有时取位置记录主构造参数的 `<param>`）；`<remarks>` 不进；项目没开 `GenerateDocumentationFile` 时没有 |

`[EmailAddress]` 等其他 DataAnnotations 不折进 Schema。注解写在属性上或位置记录的主构造参数上都可以。

## 何时使用

- 项目里有 `[Operation]`：引用本生成器，在特性里调用 `context.AddGeneratedOperations()`。
- 不想用特性、要在运行期按条件决定登记哪些操作：直接调用 `FeatureContext.AddOperation<…>`，`schema` 参数可省略，但 OpenAPI、MCP 与 Validate 阶段就没有 Schema 可用。
- 实体与仓储的登记由另一个生成器 `Persistence.Generators` 负责（[Persistence.Generators](./persistence-generators)）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。在框架仓库里以项目引用的分析器形式使用（`samples/SimpleApp` 就这样引用）：

```xml
<ProjectReference Include="../../src/XiHan.Framework.Kernel.Generators/XiHan.Framework.Kernel.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## 配置

| 设置 | 位置 | 作用 |
| --- | --- | --- |
| `RootNamespace` | 项目文件 | `GeneratedOperations` 所在的命名空间 |
| `[assembly: OperationDefaults(Expose = …)]` | 源码，程序集级 | 本程序集里没写 `Expose` 的操作取这个值；没有时为 `Expose.All` |
| `dotnet_diagnostic.XHG004.severity = error` | `.editorconfig` | 打开缺省关闭的 XHG004，强制每个操作写明暴露协议或声明程序集缺省 |
| `GenerateDocumentationFile` | 项目文件 | 开启后 XML 文档进 Schema 的 `description` |

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `OperationAttribute`（`XiHan.Framework.Kernel.Operations`） | 输入：`Id`、`Stability`（缺省 `Stable`）、`Expose`、`ReadOnly`、`Policy` / `Permission`（二选一） |
| `OperationDefaultsAttribute`（同上） | 输入：程序集级的 `Expose` 缺省 |
| `GeneratedOperations`（生成） | `Add<类名>(this FeatureContext)` 与 `AddGeneratedOperations(this FeatureContext)` |

## 使用示例

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

[assembly: OperationDefaults(Expose = Expose.Http)]

namespace Contoso.Notes;

/// <summary>
/// 新建便签的请求
/// </summary>
/// <param name="Title">标题</param>
public sealed record CreateNoteRequest([Required, StringLength(80)] string Title);

/// <summary>
/// 便签
/// </summary>
public sealed record NoteResponse(Guid Id, string Title);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateNoteRequest))]
[JsonSerializable(typeof(NoteResponse))]
internal sealed partial class NotesJsonContext : JsonSerializerContext;

// 没写 Expose：取程序集缺省 Expose.Http
[Operation("notes.create", Stability = Stability.Preview, Permission = "notes.write")]
public sealed class CreateNote : IOperation<CreateNoteRequest, NoteResponse>
{
    public ValueTask<Result<NoteResponse>> HandleAsync(CreateNoteRequest request, InvocationContext context)
    {
        Result<NoteResponse> result = new NoteResponse(Guid.CreateVersion7(), request.Title);
        return ValueTask.FromResult(result);
    }
}

public sealed class NotesFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.contoso.notes",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Permissions = ["notes.write"] },
    };

    public void Configure(FeatureContext context)
    {
        // 权限码先登记，契约里才能引用
        context.AddPermission(new PermissionDefinition("notes.write", "Write notes"));
        // 生成的登记代码：契约、序列化元数据与 JSON Schema
        context.AddGeneratedOperations();
    }
}
```

生成的 `CreateNoteRequest` 请求 Schema 里，`title` 进 `required`、带 `minLength: 1` 与 `maxLength: 80`，`description` 取自 `<param>`。完整用法见 `samples/SimpleApp/Orders`（`CreateOrder`、`ListOrders` 与 `OrdersFeature`），操作契约本身见[操作契约](/web/operations)。

## 行为与限制

| 诊断 | 级别 | 含义 |
| --- | --- | --- |
| `XHG001` | Error | 标注 `[Operation]` 的类没有实现 `IOperation<TRequest, TResponse>` |
| `XHG002` | Error | 请求或响应类型没有被本项目任何 `JsonSerializerContext` 用 `[JsonSerializable]` 登记 |
| `XHG003` | Error | 操作 ID 不符合 `<resource>.<verb>` 文法（如 `orders.create`） |
| `XHG004` | 缺省关闭 | 操作没写 `Expose`、程序集也没有 `[assembly: OperationDefaults]`，于是暴露给全部协议 |
| `XHG005` | Error | `create` / `update` / `delete` 声明了 `ReadOnly = true` |
| `XHG006` | Warning | 请求或响应里有 System.Text.Json 不支持的字典键类型（记录、类、`Nullable<T>`、`BigInteger` 等），运行期序列化会抛出；代码与 Schema 照常生成，键类型带 `[JsonConverter]` 或上下文登记了 `Converters` 时不报 |

- `ReadOnly = true` 原样生成为契约的只读标记，协议据此推导（MCP 的 `readOnlyHint`、HTTP 的 `GET`）；`get` / `list` 本来就是只读。
- 落定的 `Expose` 值写进生成的 `OperationContract`，运行期不再读特性。
- 生成器只处理当前项目里的操作与序列化上下文，引用的其他程序集里的 `JsonSerializerContext` 不参与解析（XHG002）。
- 清单的源生成（`[Capability]` → `Manifest` 属性与嵌入的 `xihan.manifest.json`）顺延，尚未提供：每个特性在 `Manifest` 属性里手写清单（[能力包与清单](/startup/features)，ADR-0075）。

## 相关

- [操作契约](/web/operations)、[HTTP 与 OpenAPI](/web/routing)、[序列化与 Native AOT](/common/serialization)
- [Kernel](./kernel)、[Kernel.Analyzers](./kernel-analyzers)、[Persistence.Generators](./persistence-generators)
- 规范：RFC-0011《操作契约与 XGI》
