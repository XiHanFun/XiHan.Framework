# gRPC 载体

> 规范性。参考实现：`XiHan.Framework.Protocols.Grpc`。一元方法，HTTP/2，protobuf 线格式；`.proto` 由操作契约生成。

## 上下文映射（RFC-0003）

gRPC 元数据就是 HTTP/2 头，复用 HTTP 载体的映射（`protocol/http.md`）：`traceparent`、`baggage`、`authorization`、`accept-language`、`xihan-deadline`。截止时间另接受 gRPC 标准的 `grpc-timeout`（`{数值}{H|M|S|m|u|n}`，相对当前时刻；没有 `xihan-deadline` 时生效）。每次调用是一次独立调用：处理器以请求头构造上下文，经完整内核管道。

## 操作映射（RFC-0011）

- 方法名按 `conventions/operation-ids.md`：服务 `{Resource}`（资源段 PascalCase，连字符分段），方法 `{Verb}`（动词 PascalCase）；路径 `/{package}.{Service}/{Method}`，`package` 来自 `XiHan:Protocols:Grpc:Package`（空则无前缀）；显式覆盖 `XiHan:Protocols:Grpc:Methods:<操作 ID>` = `Service/Method`。名字文法 `^[A-Za-z_][A-Za-z0-9_]*$`；不合法或重复即启动失败（`PROTOCOLS_VALIDATION_007` / `PROTOCOLS_CONFLICT_002`）。
- 发布范围：`Expose` 含 `Grpc` 且非 `Stability.Internal` 的操作；必须有 `OperationSchema`，否则启动失败（`PROTOCOLS_VALIDATION_006`）。
- 消息：请求与响应的 JSON Schema 各对应一个 protobuf 消息（名字取 `title`，`$defs` 里的每个定义一个消息）。字段号按 `properties` 的声明顺序从 1 起；JSON 名就是属性名，`.proto` 里的字段名是其 lower_snake_case（proto 推导的 `json_name` 与属性名不同时显式标注）。

| JSON Schema | protobuf | 说明 |
| --- | --- | --- |
| `string` | `string` | `contentEncoding: base64` → `bytes`（JSON 侧 base64） |
| `boolean` | `bool` | |
| `integer` + `format: int32` / `int64` | `int32` / `int64` | 负数按补码 varint |
| `integer` 无格式 | `string` | 十进制文本，JSON 侧为数字（Int128、BigInteger） |
| `number` + `format: float` / `double` | `float` / `double` | |
| `number` 其他（decimal、无格式） | `string` | 十进制文本，JSON 侧为数字 |
| `array` | `repeated T` | 元素只能是标量或消息；数值与布尔打包写出，读取接受打包与不打包 |
| `object` + `additionalProperties` | `map<string, T>` | 值只能是标量或消息 |
| `$ref` | 消息 | 递归允许 |
| `type` 含 `null` / `anyOf` 含 `null` | `optional` | JSON `null` 与缺席等价，不上线 |
| 无约束（`{}`） | `string` | JSON 文本原样 |

- 线上没有的字段不出现在解码后的 JSON 里（proto3 默认值由消费方补）；描述里没有的字段号跳过；重复出现的单值字段取最后一个。
- `.proto`（proto3）由同一份消息描述生成，`GET /operations.proto`（`MapXiHanProto()`）发布；同名消息在不同操作里形状不同即启动失败（`PROTOCOLS_CONFLICT_003`）。

## 状态与错误

| 错误 KIND | gRPC 状态 |
| --- | --- |
| VALIDATION | `InvalidArgument` |
| NOTFOUND | `NotFound` |
| CONFLICT | `Aborted` |
| DENIED（`SECURITY_DENIED_001` 未认证） | `PermissionDenied`（`Unauthenticated`） |
| UNAVAILABLE | `Unavailable` |
| TIMEOUT | `DeadlineExceeded` |
| CANCELLED | `Cancelled` |
| 其他 | `Internal` |

- `grpc-message` 为按 `accept-language` 翻译的文案；尾部元数据 `xihan-error-code` 为框架错误码，`xihan-trace-id` 为 trace-id（成功时也有）。
- 请求不是合法 protobuf 或与 Schema 不符 → `InvalidArgument` + `PROTOCOLS_VALIDATION_005`；响应无法按 Schema 编码 → `Internal` + `PROTOCOLS_UNEXPECTED_001`；应用未就绪 → `Unavailable` + `KERNEL_UNAVAILABLE_001`；未登记的方法 → `Unimplemented`（gRPC 服务端默认）。
