# XiHan.Framework.Kernel.Generators

Roslyn 源生成器（工具链）。扫描标注 `[Operation]` 且实现 `IOperation<TRequest, TResponse>` 的类，生成：

- `GeneratedOperations` 静态类（命名空间为项目的 `RootNamespace`）：每个操作一个 `Add<类名>(this FeatureContext)`，以及登记全部操作的 `AddGeneratedOperations(this FeatureContext)`；契约取自特性（ID、稳定性、`Expose`、策略/权限），序列化元数据取自项目里标注 `[JsonSerializable]` 的 `JsonSerializerContext`。
- 每个操作的请求与响应 JSON Schema（draft 2020-12），随登记一起放进 `OperationDescriptor.Schema`，供 `Protocols.Http` 的 OpenAPI 与 `Protocols.Mcp` 的 tool 描述使用。属性名按序列化上下文的命名策略，可空性与 `required` 按 C# 类型；嵌套对象进 `$defs`。

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

引用方式：`<ProjectReference Include="…/XiHan.Framework.Kernel.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`，或 NuGet 包（分析器形式，不进入运行时依赖）。

## 依赖方向

只依赖 `Microsoft.CodeAnalysis.CSharp`；生成的代码引用 `XiHan.Framework.Kernel`。

## 状态

P3：操作登记与 JSON Schema 可用，`Protocols.Http` 的 OpenAPI 与 `Protocols.Mcp` 的 tool 描述都从这份 Schema 产出；`.proto` 随 gRPC 适配器补充；`[Capability]` 清单生成顺延。
