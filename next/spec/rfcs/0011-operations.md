# RFC-0011 操作契约与 XGI

> 状态：已采纳 | 日期：2026-10-22 | 对应 ADR：— | 参考实现：`XiHan.Framework.Kernel.Operations` | 一致性：`conformance/vectors/operations/`（随协议适配器补）

## 摘要

业务以操作为单位声明：一个类实现 `IOperation<TRequest, TResponse>`，带操作 ID、稳定性、暴露的协议与授权要求。协议适配器把线上消息翻译成 XGI（XiHan Gateway Interface）请求交给调用器，调用器让它经过完整管道并在 Handle 阶段执行，再把结果翻译回各协议。一份契约，多协议暴露；内核零反射，请求与响应用源生成的 JSON 元数据编解码。

## 操作 ID

见 `conventions/operation-ids.md`：`<resource>.<verb>`，资源是小写复数名词（连字符分隔），动词小写；文法 `^[a-z][a-z0-9]*(-[a-z0-9]+)*\.[a-z][a-z0-9]*(-[a-z0-9]+)*$`（`OperationId`）。

## 契约

```csharp
[Operation("orders.create", Stability = Stability.Stable, Expose = Expose.Http | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder : IOperation<CreateOrderRequest, OrderResponse>
{
    public ValueTask<Result<OrderResponse>> HandleAsync(CreateOrderRequest request, InvocationContext context) { … }
}
```

- `IOperation<TRequest, TResponse>.HandleAsync` 返回 `Result<TResponse>`；可恢复失败走 `Result`，不抛异常。成功值不能为 `null`。
- `[Operation]` 是源生成器与工具链的输入，运行期不读；运行期形态是 `OperationContract(id, stability, expose, policy, permission)`，`policy` 与 `permission` 二选一。
- 登记：特性在 `Configure` 里 `context.AddOperation<CreateOrder, CreateOrderRequest, OrderResponse>(contract, jsonContext.CreateOrderRequest, jsonContext.OrderResponse)`；操作按作用域注册到容器，描述进入 `OperationRegistry`（ID 唯一，容量 10000）。`Kernel.Generators` 从 `[Operation]` 生成这段登记代码（`GeneratedOperations.Add<类名>` 与 `AddGeneratedOperations`）与 Schema，特性只写 `context.AddGeneratedOperations()`。
- `OperationDescriptor`：契约、归属特性、实现/请求/响应类型、可选的 `OperationSchema`，以及不经反射的 `DecodeRequest`、`EncodeResponse`、`ExecuteAsync`。
- `OperationSchema`：请求与响应的 JSON Schema（draft 2020-12）文本，两段都必须是 JSON 对象；属性名按序列化上下文的命名策略，`required` 为不可空且无默认值的属性，嵌套对象进 `$defs`，枚举按整数（`JsonStringEnumConverter` 时按字符串）。生成器产出，手写登记可省略；协议适配器据此发布描述（HTTP 的 OpenAPI、MCP 的 tool 描述）。

## XGI 消息

```text
OperationRequest  = Id (操作 ID) + Payload (请求类型的 UTF-8 JSON；空等价于 {})
OperationResponse = Outcome (Result) + Payload (成功时响应类型的 UTF-8 JSON；失败为空)
```

JSON 是 XGI 的规范编码；gRPC 等二进制协议在适配器里转换（`Protocols.Grpc` 以 `OperationSchema` 为 protobuf 描述，在线格式与 JSON 之间转换，`.proto` 由同一份描述生成；见 `protocol/grpc.md`），或走类型化入口避免中间编码。

## 调用器

`IOperationInvoker`：

| 方法 | 语义 |
| --- | --- |
| `InvokeAsync(context, OperationRequest)` | 未登记返回 `KERNEL_NOTFOUND_001`（不进管道）；否则把描述放进上下文，经 `App.InvokeAsync` 走完整管道，Handle 阶段解码、执行、编码 |
| `ExecuteAsync(context, OperationRequest)` | 调用方已在管道里（宿主端点）：直接解码、执行、编码并写入上下文结果 |
| `InvokeAsync<TRequest, TResponse>(context, id, request)` / `ExecuteAsync<…>` | 类型化入口，不经 JSON；类型与登记不符返回 `KERNEL_VALIDATION_002` |

规则：

1. 描述在管道开始前进入上下文（`context.SetOperation`）。Authorize 阶段没有显式授权要求时按契约的 `permission` / `policy` 判定（`Security.Abstractions.AuthorizationMiddleware`）。
2. 载荷不合法（非 JSON、`null`、类型不匹配）返回 `KERNEL_VALIDATION_001`，不执行操作。
3. 解码后的请求对象与执行成功的响应对象记录在上下文（`GetOperationRequest` / `GetOperationResponse`），供 Validate、Egress 与适配器使用。
4. 操作抛出的异常由 Observe 阶段翻译为 `KERNEL_UNEXPECTED_001`，与普通调用一致。
5. 宿主端点可用 `OperationDescriptor` 作为端点元数据，`Hosting.AspNetCore` 的 `UseXiHan()` 在构造上下文时把它放入上下文，端点内再 `ExecuteAsync`。

## 协议映射

见 `conventions/operation-ids.md` 的默认推导与显式覆盖；`Expose` 决定适配器是否发布该操作；`Stability.Internal` 的操作不对外发布。适配器发布描述文档时（如 `Protocols.Http` 的 `MapXiHanOpenApi()`、`Protocols.Mcp` 的 `tools/list`）只读登记表与 `OperationSchema`，不读源码、不反射类型。

面向人的操作标题与描述不写在契约里：放在消息目录，键为 `<操作 ID>.title` 与 `<操作 ID>.description`（ID 里的 `-` 换成 `_`，如 `order_lines.list.description`），由适配器按调用方区域取出（MCP tool 的 `title` / `description`）；没有就不给。

## 源生成器

`Kernel.Generators` 以分析器形式引用（`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`），不进入运行时依赖；输入是 `[Operation]` 类与项目里标注 `[JsonSerializable]` 的 `JsonSerializerContext`，输出在 `RootNamespace` 下的 `GeneratedOperations`。诊断：`XHG001` 未实现 `IOperation<TRequest, TResponse>`、`XHG002` 请求或响应类型未被任何序列化上下文登记、`XHG003` 操作 ID 不合文法；任一诊断成立时不生成代码。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `KERNEL_NOTFOUND_001` | 操作未登记 |
| `KERNEL_VALIDATION_001` | 操作载荷不合法 |
| `KERNEL_VALIDATION_002` | 类型化调用的请求或响应类型与登记不符 |
| `KERNEL_OPERATION_001` | 操作 ID 重复登记（配置期异常） |
| `KERNEL_OPERATION_002` | 操作登记表已满（配置期异常） |
