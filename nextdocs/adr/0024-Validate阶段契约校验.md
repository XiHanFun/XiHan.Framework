# ADR-0024 Validate 阶段契约校验：内核按请求 JSON Schema 校验载荷，DataAnnotations 折进生成的 Schema，宿主进管道前绑定载荷

## 状态

已采纳（RFC-0004 / RFC-0011 修订）。

## 日期

2026-10-27

## 背景

RFC-0004 与总体方案附录 B 把「Schema 校验（由契约驱动）」放在 `validate` 阶段，但内核只有 `ObserveMiddleware` 与 `DeadlineMiddleware`，Validate 阶段是空的：载荷在 Handle 阶段才解码，不合法的请求要到操作里才被发现，BasicApp 的每个操作都得自己写参数校验。内核是 Ring 1：零反射、零第三方依赖、AOT 可用，所以不能引 JSON Schema 库，也不能用 `System.ComponentModel.DataAnnotations.Validator`（反射遍历属性，`RequiresUnreferencedCode`）。另外 HTTP 适配器在端点里（Handle 阶段）才把路由值、请求体与查询串绑定为载荷，Validate 阶段的中间件看不到它。用户于 2026-10-27 决定在框架里实现。

## 备选

1. 引入第三方 JSON Schema 库放在 `Protocols.Http` 做校验（只覆盖 HTTP，内核仍空，且 Ring 5 才校验）。
2. 内核自带一个只覆盖生成器产出关键字的 JSON Schema 校验器，Validate 阶段按 `OperationDescriptor.Schema` 校验载荷；DataAnnotations 由 `Validator` 在运行期执行。
3. 同 2，但 DataAnnotations 在编译期由 `Kernel.Generators` 折进 Schema（`[Range]` → `minimum` / `maximum` 等），运行期只校验 Schema 与 `IValidatableObject`。
4. HTTP 的载荷继续在端点里绑定，校验在 `ExecuteAsync` 里做（名义上 Validate、实际在 Handle，且 Idempotency / Transaction 已经执行）。
5. HTTP 端点元数据带绑定委托，`UseXiHan()` 在进入管道前绑定载荷放进上下文，端点只执行。

## 决策

采用备选 3 与 5。

- 内核新增 `Validation` 功能区：`JsonSchemaValidator`（draft 2020-12 子集：`type`（含 `[type, "null"]`）、`properties`、`required`、`additionalProperties`、`items`、`enum`、`const`、`anyOf` / `oneOf` / `allOf`、`$ref`（`#/$defs/…`）、数值范围、字符串长度与 `pattern`、元素数；`format` 等不断言；违规上限 32 条、深度上限 64；`pattern` 正则带 1 秒超时并有界缓存）、`SchemaViolation`（`$.path: reason`）、`ValidationMiddleware`（Validate 阶段开头，随 `KernelOptions.UseBuiltInMiddleware` 装配）。
- 中间件顺序：取载荷（原始调用用上下文里的载荷；类型化调用只有对象，按 `RequestInfo` 编码一次）→ 有 Schema 则校验（违规 `KERNEL_VALIDATION_003`，不是 JSON `KERNEL_VALIDATION_001`）→ 原始调用在这里解码并记录请求对象（Handle 阶段直接执行）→ 请求实现 `IValidatableObject` 时以 AOT 友好的 `ValidationContext(instance, displayName, services, items)` 调用 `Validate`，结果按成员列为违规（`KERNEL_VALIDATION_004`）。任一步失败即结束调用，不进 Idempotency / Transaction / Handle。
- `Kernel.Generators` 把属性与位置记录主构造参数上的 DataAnnotations 折进请求 Schema：`[Required]` 进 `required` 并去掉可空（字符串加 `minLength: 1`，`AllowEmptyStrings = true` 除外），`[Range]` → `minimum` / `maximum`（`MinimumIsExclusive` / `MaximumIsExclusive` → `exclusiveMinimum` / `exclusiveMaximum`），`[StringLength]` / `[MinLength]` / `[MaxLength]` / `[Length]` → 字符串的 `minLength` / `maxLength` 或集合的 `minItems` / `maxItems`，`[RegularExpression]` → `pattern`；`[EmailAddress]` 等不折。OpenAPI / MCP / gRPC 描述因此同时带上这些约束。
- `Hosting.AspNetCore` 新增端点元数据委托 `OperationPayloadBinder`；`Protocols.Http` 的 `MapXiHanOperations` 把 `OperationRequestBinder.BindAsync` 挂上去，`UseXiHan()` 在构造上下文时绑定载荷并 `SetOperationPayload`（绑定失败直接写 Problem Details，不进管道），端点 `HandleAsync` 用上下文里的载荷 `ExecuteAsync`；`OperationInvoker.DispatchAsync` 有 Validate 阶段解码的请求就直接执行。gRPC / MCP / 命令行 / AI 工具本来就经 `InvokeAsync` 带载荷，自动纳入。
- gRPC 解码补 proto3 默认值：线上缺席的布尔与数值写零值、`repeated` 写 `[]`、`map` 写 `{}`，否则非可空的 `int Page` 这类必填标量在 gRPC 上永远"缺席"（proto3 不上线默认值）；字符串、字节与任意 JSON 不补（空串对 date-time、uuid、枚举这类带格式的字符串不是合法值，会把本来能按默认值解码的请求变成解码失败），`optional` 与嵌套消息缺席即缺席。
- 新错误码 `KERNEL_VALIDATION_003` / `_004` 与消息键 `kernel.operation.schema_violation` / `kernel.operation.request_invalid`（`id`、`reason`；细节 `violations` 列全部），10 种区域。
- `SimpleApp` 的 `PlaceOrderRequest` 加 `[Required, StringLength(64, MinimumLength = 1)]`、`[Range(1, 1000000)]` 与 `IValidatableObject`（名称不能只有空白，文案走消息键 `samples.order.name_blank`）。

## 理由

- 校验器只实现生成器会产出的关键字，几百行即可、零依赖、可 AOT，且与 OpenAPI 公布的契约逐字一致：客户端看到的约束就是服务端执行的约束。
- DataAnnotations 折进 Schema 而不是运行期 `Validator`：运行期需要反射，Ring 1 禁止；折进 Schema 后约束同时进入 OpenAPI / MCP / gRPC 描述，一处声明三处受益。`IValidatableObject` 是接口调用、不需要反射，保留给 Schema 表达不了的跨字段规则。
- 载荷在进管道前绑定（备选 5）才是真正的 Validate 阶段：备选 4 会在工作单元已开启、幂等键已记录之后才拒绝请求。绑定委托挂在端点元数据上让 Ring 4 的宿主不需要引用 Ring 5 的绑定器。

## 影响

- 内核公开面（Preview）：`Validation` 区三个类型；`OperationInvocationExtensions.SetOperationPayload` / `GetOperationPayload`；`KernelErrors` / `KernelMessages` 各两项。管道打印多一行 `validate XiHan.Framework.Kernel.Validation.ValidationMiddleware`。
- `Hosting.AspNetCore` 新增 `OperationPayloadBinder`；`Protocols.Http` 的端点行为变化：请求体不是 JSON 对象时在进管道前返回 400。
- 行为变化：带 Schema 的操作收到不合法载荷时在 Validate 阶段返回 `KERNEL_VALIDATION_003`，操作不再被调用；没有 Schema（手写登记不传）的操作不受影响；`UseBuiltInMiddleware = false` 时不校验。
- 测试：`Kernel.Tests/Validation`（校验器 30 余例、中间件 8 例）、`Kernel.Generators.Tests` 注解折叠、`Protocols.Http.Tests/ValidationStageTests`。
