# 操作 ID

```text
OperationId = Resource "." Verb
Resource    = 小写复数名词，连字符分隔（orders、order-lines）
Verb        = 小写动词（create、get、list、update、delete、cancel、...）
```

示例：`orders.create`、`orders.get`、`order-lines.list`。

## 协议映射（默认推导，可显式覆盖）

| 动词 | HTTP | gRPC | MCP tool |
| --- | --- | --- | --- |
| `create` | `POST /{resource}` | `{Resource}.Create` | `{resource}_create` |
| `get` | `GET /{resource}/{id}` | `{Resource}.Get` | `{resource}_get` |
| `list` | `GET /{resource}` | `{Resource}.List` | `{resource}_list` |
| `update` | `PUT /{resource}/{id}` | `{Resource}.Update` | `{resource}_update` |
| `delete` | `DELETE /{resource}/{id}` | `{Resource}.Delete` | `{resource}_delete` |
| 其他 | `POST /{resource}/{id}:{verb}` | `{Resource}.{Verb}` | `{resource}_{verb}` |

一份契约一份 Schema；HTTP 错误用 RFC 9457 Problem Details。
