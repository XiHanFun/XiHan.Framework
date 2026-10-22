# 插件调用、事件与回调

> 规范性。协议 `xihan.plugin.v1`（`plugin.proto`，RFC-0017）。

## 1. 代理操作

握手声明的每个操作在宿主 `OperationRegistry` 登记为代理操作：契约取 `OperationDeclaration`（`id`、`stability`、`expose`、`permission` / `policy`），Schema 取 `request_schema` / `response_schema`。代理操作与本地操作无差别：协议适配器按 `expose` 发布（HTTP 路由、gRPC 方法、MCP tool、命令行），调用经宿主的完整管道，Authorize 阶段按契约判定；Handle 阶段把载荷与上下文发给插件。

- 一元操作：`Plugin.Invoke`。`payload` 是请求类型的 JSON（空等价于 `{}`）；`InvokeResponse.status.ok` 为真时 `payload` 是响应类型的 JSON，否则 `error` 是业务失败。
- 流式操作（`streaming: true`）：`Plugin.InvokeStream`。调用方先发 `start`（`InvokeRequest`，`payload` 为请求对象），再发零个或多个 `chunk`（每帧一个完整 JSON 值），最后发 `end`（`ok: true`；`ok: false` 表示中止，被调方应停止处理）；被调方发零个或多个 `chunk`，最后发 `end`。任一方收到不符合顺序的帧（`start` 不是第一帧、`end` 之后还有帧）以 gRPC `InvalidArgument` 结束流。.NET 参考实现在 RFC-0011 定义流式操作契约之前只把流式操作暴露为内部调用，不经协议适配器发布。
- 调用到达时插件未 `Ready`：懒激活按 `lifecycle.md` 等待，否则 `PLUGINS_UNAVAILABLE_001`。

## 2. 上下文映射（RFC-0003）

| 字段 | `InvocationMetadata` | 说明 |
| --- | --- | --- |
| Trace | `trace_parent`、`trace_state` | 插件以其为父 Span |
| Deadline | `deadline` | RFC 3339 UTC；宿主同时把它设为 gRPC 调用的截止时间，二者相同 |
| Baggage | `baggage` | 只含已登记的 `xihan.*` 键（`conventions/baggage-keys.md`）；租户、主体、关联 ID、幂等键、区域都在其中 |
| Items / Services | — | 不传播 |

插件必须按 `deadline` 与 gRPC 取消停止工作；返回的 `Error` 若是超时或取消，用 `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001`。

## 3. 错误映射

- 业务失败走 `Status.error`，gRPC 状态保持 `OK`；`Error` 字段与 RFC-0001 的 JSON 编码一一对应（`details` 是 JSON 对象文本）。宿主用握手时得到的目录翻译 `message_key`；键不在目录里时文案为"键 (参数)"。
- gRPC 状态只表示传输或协议层失败，宿主映射为：

| gRPC 状态 | 宿主错误 |
| --- | --- |
| `Unavailable`、连接失败、进程已退出 | `PLUGINS_UNAVAILABLE_001` |
| `DeadlineExceeded` | `KERNEL_TIMEOUT_001` |
| `Cancelled` | `KERNEL_CANCELLED_001` |
| `Unimplemented`（插件未实现该 RPC 或操作） | `KERNEL_NOTFOUND_001` |
| `InvalidArgument`（帧序或载荷不合法） | `KERNEL_VALIDATION_001` |
| 其他 | `PLUGINS_UNEXPECTED_001`，细节含状态码与消息 |

- 宿主把插件错误原样放进调用结果；协议适配器再按各自载体映射（HTTP Problem Details、gRPC 状态 + `xihan-error-code`、MCP `isError`）。

## 4. 事件投递（RFC-0009）

- 宿主为握手声明的每个 `Subscription` 向消息代理订阅主题；消息到达时派发器按 RFC-0009 开新作用域、构造上下文、经完整管道，在 Handle 阶段调用 `Plugin.HandleEvent`。`message_type` 非空时只投递 `type` 相同的消息，其余在宿主侧按成功处理（不进插件）。
- `Envelope` 是 CloudEvents 1.0 属性的平铺；`attempt` 从 1 起。插件返回 `ok` 表示已处理；返回 `error` 由代理按重试与死信策略处理；插件不可用期间的投递同样按该策略处理。
- 至少一次投递：插件处理器必须幂等（可用 `baggage` 里的 `xihan.idempotency-key` 或信封 `id`）。

## 5. 回调（`Host` 服务）

每次回调带元数据 `xihan-plugin-session`（`handshake.md`）；宿主以插件身份执行：主体 ID 为清单 ID、认证方案 `Plugin`、无声明；回调元数据里的 `xihan.principal` 被忽略并覆盖，`xihan.tenant` 与其余已登记 Baggage 采用，追踪以回调的 `trace_parent` 为父。

| RPC | 权限 | 语义 |
| --- | --- | --- |
| `Host.Invoke` | `capability:kernel.pipeline` | 以新的调用经宿主完整管道执行操作（含 Authorize：插件主体须被授予操作要求的权限）；未登记的操作 `KERNEL_NOTFOUND_001` |
| `Host.Publish` | `capability:messaging.queue` | 经宿主消息代理发布，逐项结果；`topic` 为空的信封 `MESSAGING_VALIDATION_002` |

权限未授予返回 gRPC `PermissionDenied` + `PLUGINS_DENIED_002`。`net.outbound:` 与 `fs.*:` 权限不经协议表达，由宿主通过操作系统沙箱（若启用）强制。

## 6. 日志与诊断

- 插件日志写 stderr，一行一条；宿主逐行转为日志（来源 `plugins.<清单 ID>`）。以 `{` 开头且能解析为 JSON 对象的行按结构化日志处理：`level`（`trace` / `debug` / `information` / `warning` / `error` / `critical`）、`message`、其余字段为作用域属性。
- 宿主为每次 `Invoke` / `InvokeStream` / `HandleEvent` 记录内核的 `xihan.invoke` Span 子 Span `xihan.plugin.invoke`，标签 `xihan.plugin.id`、`xihan.operation`。

## 7. 一致性

P4-04 的 gRPC 一致性运行器以本文件与 `lifecycle.md` 为用例来源：握手协商、清单一致性、`Invoke` 的上下文与错误映射、流式帧序、事件投递的幂等与失败、回调的令牌与权限。数据向量见 `conformance/vectors/plugin/`。
