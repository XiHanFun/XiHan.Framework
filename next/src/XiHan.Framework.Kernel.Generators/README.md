# XiHan.Framework.Kernel.Generators

Roslyn 源生成器（工具链）。扫描标注 `[Operation]` 且实现 `IOperation<TRequest, TResponse>` 的类，生成：

- `GeneratedOperations` 静态类（命名空间为项目的 `RootNamespace`）：每个操作一个 `Add<类名>(this FeatureContext)`，以及登记全部操作的 `AddGeneratedOperations(this FeatureContext)`；契约取自特性（ID、稳定性、`Expose`、策略/权限），序列化元数据取自项目里标注 `[JsonSerializable]` 的 `JsonSerializerContext`。
- 每个操作的请求与响应 JSON Schema（draft 2020-12），随登记一起放进 `OperationDescriptor.Schema`，供 `Protocols.Http` 的 OpenAPI 与 `Protocols.Mcp` 的 tool 描述使用，内核的 Validate 阶段按请求 Schema 校验载荷（ADR-0024）。属性名按序列化上下文的命名策略，可空性与 `required` 按 C# 类型；嵌套对象进 `$defs`。根 `title` 与定义名取类型名，封闭泛型为 `<外层名>Of<实参名>`（多个实参以 `And` 连接、嵌套递归，如 `PageResultOfUserResponse`、`EnvelopeOfListOfUserResponseAndInt32`），不同实参的同一泛型是不同的 Schema；按字符串序列化的枚举（`UseStringEnumConverter`）进 `$defs/<枚举类型名>`（`type: string` + `enum`）并以 `$ref` 引用，可空为 `anyOf` 带 `null`，整数枚举仍内联为 `integer`（ADR-0034）。属性（或位置记录的主构造参数）上的 DataAnnotations 折进 Schema：`[Required]` 进 `required` 并去掉可空（字符串加 `minLength: 1`，`AllowEmptyStrings = true` 除外），`[Range]` → `minimum` / `maximum`（`MinimumIsExclusive` / `MaximumIsExclusive` → 开区间），`[StringLength]` / `[MinLength]` / `[MaxLength]` / `[Length]` → 字符串的 `minLength` / `maxLength` 或集合的 `minItems` / `maxItems`，`[RegularExpression]` → `pattern`；`[EmailAddress]` 等其他注解不折。

```csharp
[Operation("orders.create", Expose = Expose.Http | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(...) : IOperation<PlaceOrderRequest, OrderResponse> { … }

public void Configure(FeatureContext context) => context.AddGeneratedOperations();   // 生成的登记代码
```

| 规则 | 含义 |
| --- | --- |
| `XHG001` | 标注 `[Operation]` 的类必须实现 `IOperation<TRequest, TResponse>` |
| `XHG002` | 请求或响应类型没有任何 `JsonSerializerContext` 用 `[JsonSerializable]` 登记 |
| `XHG003` | 操作 ID 不符合 `<resource>.<verb>` 文法 |
| `XHG004` | 缺省关闭：操作没写 `Expose`、程序集也没有 `[assembly: OperationDefaults]`，于是暴露给全部协议；以 `dotnet_diagnostic.XHG004.severity = error` 打开即强制写明 |
| `XHG005` | `create` / `update` / `delete` 声明了 `ReadOnly = true` |

暴露协议的缺省（ADR-0041）：操作没写 `Expose` 时取程序集级 `[assembly: OperationDefaults(Expose = Expose.Http)]` 的值，再没有就是 `Expose.All`；落定的值写进生成的 `OperationContract`。`ReadOnly = true` 原样生成为契约的 `readOnly: true`。

引用方式：`<ProjectReference Include="…/XiHan.Framework.Kernel.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`，或 NuGet 包（分析器形式，不进入运行时依赖）。

## 依赖方向

只依赖 `Microsoft.CodeAnalysis.CSharp`；生成的代码引用 `XiHan.Framework.Kernel`。

## 状态

P3：操作登记与 JSON Schema 可用，`Protocols.Http` 的 OpenAPI、`Protocols.Mcp` 的 tool 描述与 `Protocols.Grpc` 的 protobuf 描述及 `.proto` 都从这份 Schema 产出；`[Capability]` 清单生成顺延。
