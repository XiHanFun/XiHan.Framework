# ADR-0032 OpenAPI 生成器补齐：应用指定标题版本、Bearer 认证方案、按区域的操作摘要、普通端点收录

## 状态

已采纳（`spec/protocol/http.md` 修订）。

## 日期

2026-10-27

## 背景

BasicApp 6.0 拍板"一份 `openapi.json`、一个生成器、一份 Schema"，不引入 `Microsoft.AspNetCore.OpenApi`。现状：`OpenApiDocumentBuilder.Build` 只读 `OperationRegistry`；`MapXiHanOpenApi` 的标题取应用名、版本取框架版本、整份缓存；文档没有 `securitySchemes`、没有操作 `summary`；`MapXiHanOperations` 映射的是裸 `RequestDelegate`，普通 ASP.NET Core 端点（`MapGet` / `MapPost` + `RequireXiHanPermission`）完全不进文档，前端拿不到文件上传、下载这类端点的描述。约束：零第二套 Schema 推导（Ring 5 可以反射，但 JSON 类型的 Schema 只能有一个来源——`Kernel.Generators` 产出的操作 Schema）；权限码与策略名不是 OAuth scope（ADR-0031）。

## 备选

1. 引入 `Microsoft.AspNetCore.OpenApi` 并把操作文档合并进去：两套生成器、两套 Schema，与拍板相反。
2. 生成器多读一遍 `EndpointDataSource`：按元数据收录普通端点，JSON 类型只能引用已登记操作的根 Schema。
3. 收录规则用名单（排除 Hub、探针等）或用元数据（带 `IAcceptsMetadata` / `IProducesResponseTypeMetadata` 之一）。
4. 认证方案从能力图谱猜（清单 ID）或从容器里的 `IAuthenticationHandler.Scheme` 读。
5. 摘要整份缓存只出默认区域，或按协商区域分别缓存。
6. 标题 / 版本经配置节或经 `MapXiHanOpenApi` 的选项参数。
7. 普通端点 JSON 类型引用不到时：推导一份最小 Schema、写 `{}`，或生成失败。

## 决策

采用 2、3（元数据）、4（处理器方案名）、5（按区域缓存）、6（选项参数）、7（失败）。

- `MapXiHanOpenApi(OpenApiOptions? options = null, string pattern = "/openapi.json")`：`OpenApiOptions { Title, Version, Servers }`（`OpenApiServer(Url, Description?)`），缺省行为不变。按 `Accept-Language` 协商出的区域各生成并缓存一份（没有装配本地化时一份、键为空），响应带 `Content-Language`。
- `OpenApiDocumentBuilder.Build(OpenApiDocumentSource)`：输入记录带登记表、HTTP 选项、权限码注册表、标题 / 版本 / `servers`、已装配的认证方案名、候选端点与按操作 ID 取摘要的函数；旧的五参数 `Build` 保留为薄包装。
- 认证方案：方案名含 `Bearer` → `components.securitySchemes.bearer`（`http` / `bearer` / `JWT`）+ 全局 `security`；没有授权要求的操作与端点标 `security: []`；其他方案不发布，权限与策略仍只走 `x-xihan-*`。
- 摘要：`<操作 ID>.description`（`-` 换 `_`，与 MCP 同一个键）经 `IMessageLocalizer.Find(key, locale)`，没有就不写。
- 普通端点：`EndpointReader` 按元数据筛选（没有 `OperationDescriptor`、有 `IHttpMethodMetadata`、带 `IAcceptsMetadata` 或 `IProducesResponseTypeMetadata` 之一、没有 `IExcludeFromDescriptionMetadata`），每个方法一条；路由参数、`operationId` / `summary` / `description` / `tags`、`AuthorizationRequirement`（`permission:` 前缀拆回权限码）照元数据写；内容类型分 JSON / `multipart/form-data` / 表单 / 文本 / 二进制。JSON 经 `OperationSchemaReference` 元数据（`WithXiHanOperationSchema`）显式声明或按类型匹配到操作的根 Schema，根提升到 `components/schemas/<title>`，操作自己也改为 `$ref`（一份 Schema 只出现一次）；引用不到 `PROTOCOLS_VALIDATION_010`；与操作共用路由 `PROTOCOLS_CONFLICT_002`。
- 新公开 API：`OpenApiOptions`、`OpenApiServer`、`OpenApiDocumentSource`、`OperationSchemaReference`、`OpenApiEndpointConventionBuilderExtensions.WithXiHanOperationSchema`、`OpenApiEndpointRouteBuilderExtensions.DefaultPattern`、`OpenApiDocumentBuilder.Build(OpenApiDocumentSource)`、`HttpErrors.OpenApiSchemaUnresolved`。

## 理由

- 按元数据而不是名单（备选 3）：没有描述元数据的端点本来就没有可描述的内容，名单会随宿主新增端点失效。
- 从处理器方案名读（备选 4）：任何以 Bearer 工作的 Provider 都自动命中，不绑清单 ID；ApiKey 之类没有标准对应关系，不猜。
- 按区域缓存（备选 5）：文档体积小、区域数有限，分别缓存比整份只出一种语言诚实；缺省区域仍是 `en-US`。
- 引用不到就失败（备选 7）：兜底推导会制造第二套 Schema，`{}` 会让前端生成空类型；失败在第一次请求文档时暴露，由开发者补声明或登记操作。
- 旧 `Build` 保留：测试与工具调用面小，不必为 Preview API 的扩展破坏它。

## 影响

- `Protocols.Http` 公开面 +72 符号（Preview）；`PublicAPI.Unshipped.txt` 同步。
- 行为变化：装配 `Security.Jwt` / `Security.OpenIdConnect` 的应用，文档多出 `securitySchemes` 与 `security`；装配本地化的应用，文档按区域分别生成；有描述元数据的普通端点进文档，其 JSON 类型若引用不到操作 Schema 则文档端点在第一次请求时失败（显式 `WithXiHanOperationSchema` 或 `ExcludeFromDescription()` 修正）。
- 测试：`Protocols.Http.Tests/OpenApiExtensionsTests`（选项、Bearer 与 `security: []`、按区域摘要与缓存、普通端点的表单 / 二进制 / 文本 / JSON 引用与提升、拒绝未知 JSON 类型与共用路由）、`Profiles.Full.Tests`（缺省标题版本、探针与 MCP 不进文档、ApiKey 不发布方案）。
