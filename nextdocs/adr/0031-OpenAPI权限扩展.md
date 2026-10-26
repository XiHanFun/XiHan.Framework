# ADR-0031 OpenAPI 文档输出操作的权限 / 策略扩展与权限定义组件

## 状态

已采纳

## 日期

2026-10-27

## 背景

`Protocols.Http` 的 `MapXiHanOpenApi()` 从 `OperationRegistry` 生成 OpenAPI 3.1 文档，操作对象上除标准字段外只有 `x-xihan-stability`。操作契约（`OperationContract`）里的授权要求——权限码 `Permission` 或策略名 `Policy`（二选一）——以及特性经 `FeatureContext.AddPermission` 登记进 `PermissionRegistry` 的权限定义（`Code`、`DisplayName`、`Description`）都不在文档里。

前端（next-ui）接 5.0 时需要两件事：按操作的权限码隐藏当前用户无权的入口；按应用的权限定义生成授权界面（角色勾选权限）。这两份数据都在后端的登记表里，没有协议把它们带给前端。用户于 2026-10-27 决定在框架里实现，而不是由 BasicApp 另开接口绕过。

## 备选

1. **用标准 `security` / `securitySchemes` 表达**：把权限码写成一个安全方案的 scope，每个操作的 `security` 引用它。否：权限码不是 OAuth scope，策略名更不是；`security` 的语义是"调用需要哪种凭据"，工具链会据此生成认证流程，与"调用方要持有哪个权限"混在一起会误导客户端生成器。标准字段留给认证方案（JWT、ApiKey 等）用。
2. **`x-xihan-*` 扩展**（取）：操作上 `x-xihan-permission: "<码>"` 或 `x-xihan-policy: "<策略名>"`；`components` 下 `x-xihan-permissions` 按权限码索引权限定义。与已有的 `x-xihan-stability` 同一风格，不改变标准字段的含义，客户端生成器忽略即可。
3. **权限定义只收录被 HTTP 操作引用的**：文档自洽，但授权界面需要应用的全部权限，其中有些不被任何 HTTP 操作引用（只在 gRPC / MCP 暴露的操作、端点级 `RequireXiHanPermission`、插件回调的 `capability:` 权限）。
4. **权限定义 = 被引用的 ∪ 注册表全部**（取）：被引用的权限码保证操作能对上条目，注册表全部保证授权界面完整；被引用却没登记的只写 `code`，因为没有定义可取。
5. **另开一个 `/permissions` 端点**：多一个契约、多一次请求，而且与 OpenAPI 文档不在同一份快照里；前端还是要把两份数据按权限码拼起来。

## 决策

采用备选 2 与 4。

- `OpenApiDocumentBuilder.WriteOperation`：契约有 `Permission` 时写 `"x-xihan-permission": "<码>"`，有 `Policy` 时写 `"x-xihan-policy": "<策略名>"`，都没有时不写（二者互斥由 `OperationContract` 构造保证）。
- `components` 下新增 `"x-xihan-permissions"`：对象，键是权限码、按序数排序；值为 `{ "code", "displayName", "description" }`，`description` 为 `null` 时省略。收录范围 = 文档里任一已发布操作引用的权限码 ∪ `PermissionRegistry.All()`；被引用但注册表里没有的只写 `{ "code" }`；没有注册表时只按引用写。
- `OpenApiDocumentBuilder.Build(registry, options, permissions, title, version)` 增加参数 `PermissionRegistry? permissions`（Preview API，直接改签名，不留旧重载）；`MapXiHanOpenApi` 以 `services.GetService<PermissionRegistry>()` 传入——内核在 `AppBuilder.Build` 装配完全部特性后把注册表以单例登记。
- 文档在第一次请求时生成并缓存的语义不变：此时特性已配置完毕，注册表与登记表都已定型。

形状：

```json
{
  "paths": {
    "/orders": {
      "post": { "operationId": "orders.create", "x-xihan-permission": "orders.write" },
      "get": { "operationId": "orders.list", "x-xihan-policy": "authenticated" }
    }
  },
  "components": {
    "schemas": { "...": {} },
    "x-xihan-permissions": {
      "orders.read": { "code": "orders.read", "displayName": "Read orders" },
      "orders.write": { "code": "orders.write", "displayName": "Place orders", "description": "Create orders for the current tenant." }
    }
  }
}
```

## 理由

- 扩展字段是 OpenAPI 规范认可的扩展方式，不改变标准字段语义，现有工具链忽略即可；前端按固定键名读取，无需解析安全方案。
- 并集而不是只取被引用的：授权界面的完整性由注册表保证，操作与定义的对应由被引用集合保证；两者都来自内核已有的登记表，没有新增任何登记动作。
- 未登记的权限码也输出：`OperationRegistry` 不要求权限码先登记（`PolicyCatalog` 只在判定时查），文档如实反映契约，前端能发现"有操作引用但没有定义"的缺口，而不是静默缺一条。
- 放在 `components` 下而不是文档根：它是被多处操作引用的共享定义，与 `schemas` 同级最符合 OpenAPI 对 `components` 的用法。

## 影响

- `Protocols.Http`：`Build` 签名变化（`PublicAPI.Unshipped.txt` 同步）；README 的 OpenAPI 说明补两项扩展与 `components.x-xihan-permissions` 的形状。
- `spec/protocol/http.md` 的 OpenAPI 一节由主会话修订，登记这三个扩展键。
- `Protocols.Grpc` / `Protocols.Mcp` 不受影响：gRPC 的 `.proto` 与 MCP 的 `tools/list` 没有对应的扩展位，权限信息仍以 HTTP 的 OpenAPI 文档为准。
- next-ui 据 `x-xihan-permission` 隐藏入口、据 `components.x-xihan-permissions` 生成授权界面；`x-xihan-policy` 只作展示，策略判定仍在后端。
