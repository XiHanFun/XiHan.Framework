# ADR-0052 OpenAPI 的文案、标签、分组与字段说明

## 状态

已采纳（`spec/protocol/http.md` 的 OpenAPI 一节与 `spec/rfcs/0011-operations.md` 修订）。

## 日期

2026-10-30

## 背景

用户要求生成的 OpenAPI 与 4.x 一样支持标签、分组与描述，否则 Scalar 里看着很乱。现状有五处不足：

- 操作的 `summary` 取 `<操作 ID>.description` 整句，BasicApp 的文案里还手写了「需要 X 权限。」。
- `tags` 只有资源码，没有顶层 `tags` 说明。
- 只有一份文档，没有分组。
- 参数与字段没有说明。

4.x（`XiHan.Framework.Web.Docs`）的做法：

- 文案来自 XML 注释：`summary` 取 `<summary>`，`description` 取 `<remarks>`，参数取 `<param>`，响应取 `<returns>`。
- `[DynamicApi(Group, GroupName, Tag)]` 给中文标签。
- 每个 Group 一份文档，Scalar 列出「API V1」加各组。

## 备选

1. 全部照 4.x 取 XML 注释：
   - 操作文案在 5.0 已按 D9 放进消息目录（MCP 的 tool 描述按区域取）；改回 XML 就只有一种语言。
   - 标签与分组在 6.0 没有对应的注解。
2. 全部走消息目录：字段说明要为每个 DTO 属性写十种区域，量太大。
3. 分开处理：
   - 操作、标签与分组的文案走消息目录（按区域）。
   - 字段说明取 XML 注释作源语言文案，消息目录可按区域覆盖。

## 决策

采用 3。

- **操作**：
  - `summary` 取 `<操作 ID>.title`，`description` 取 `<操作 ID>.description`（RFC-0011 已定的两个键，与 MCP `tools/list` 共用）。
  - 各自没有就不写，不互相顶替。
  - 文案不再手写授权要求：生成器按契约在 `description` 末尾空一行附上，文案在框架目录 `protocols.http.openapi_*`（十种区域）。
    - 权限是「需要权限 <码>（<显示名>）」：显示名先取 `permissions.<权限码>.name`（按区域），没有再取权限定义的（BasicApp 的定义是英文），都没有时只有码。`x-xihan-permissions` 的显示名与说明同样先取 `permissions.<权限码>.name` / `.description`。
    - 命名策略是「需要策略 <名>」。
    - `authenticated` 是「需要登录」。
  - `x-xihan-permission` / `x-xihan-policy` 照旧。
- **标签**：顶层 `tags` 列出文档里用到的标签，按名字排序。
  - `name` 仍是资源码，客户端生成不跟着变。
  - `x-displayName`（Scalar 认）取 `openapi.tags.<资源段>.name`，`description` 取 `openapi.tags.<资源段>.description`。
- **分组**：`OpenApiOptions.Groups` 声明 `OpenApiGroup`（名字、排序、所属特性的清单 ID、额外标签）。
  - 操作按 `OwnerId` 入组；普通端点按标签入组，组的标签是组内操作的资源段加额外标签。
  - 全量文档带 `x-tagGroups`（显示名取 `openapi.groups.<组名>.name`，没进组的归「其他」）。一个标签只出现一次：资源的操作分属几个特性时（BasicApp 的 `auth.*` 分在 Identity 与 Access），归排序靠前的那一组，Scalar 左栏不重复列出。
  - 每组一份文档 `/openapi/<组名>.json`：只含本组的操作、端点、组件与引用到的权限码。
  - `GetXiHanOpenApiDocuments()` 列出全部文档（全量文档名为 `all`），宿主据此调用 Scalar 的 `AddDocument`。
- **字段说明**：
  - `Kernel.Generators` 把 XML 注释折进 Schema 的 `description`：类型的 `<summary>` 进根与 `$defs`，属性的 `<summary>` 进属性，没有时取位置记录主构造参数在类型上的 `<param>`。
  - `<see cref>` 取名字，空白折叠；中文换行处直接相连。
  - 参数取对应属性的说明，请求体取请求根说明，成功响应取响应根说明，没有时用按区域的「成功」，失败响应用「错误」。
  - 某个区域要换文案时，用 `openapi.schemas.<Schema 名>.description` 与 `.properties.<属性名>` 覆盖（都转成 snake_case）。

## 理由

- 一份文案两处用：MCP 与 OpenAPI 都读 `.title` / `.description`，LLM 与人看到的是同一份说明。
- 授权要求由生成器统一写：各包不用十种语言各写一遍「需要 X 权限」，权限改了说明不会过时。
- 标签名保持资源码：前端类型化客户端与契约快照不受显示名改动影响。
- 字段说明的来源就是 DTO 的 XML 注释，写一次即可；需要多语言时再按区域覆盖，不强制十种。

## 影响

- 公开 API（`Protocols.Http`）：
  - `OpenApiDocumentSource.Describe` 改为 `Localize`（按键与参数取文案）。
  - 新增 `Groups` / `Group`、`OpenApiOptions.Groups`、`OpenApiGroup`、`OpenApiDocumentLink`、`GetXiHanOpenApiDocuments`、`GroupPattern`、`FullDocumentName`，`FromServices` 增加 `group` 参数。
  - 新增 `HttpMessages` 与十种区域文案。
- 行为：
  - 操作的 `summary` 由 `.description` 改为 `.title`。
  - 所有生成的 Schema 多了来自 XML 注释的 `description`（只是注解，校验器忽略）。
  - 示例 SimpleApp 的文案拆成标题与不含权限的说明。
- BasicApp：
  - 为每个操作补 `.title`，去掉 `.description` 里的权限句。
  - 写 `openapi.tags.*` 与 `openapi.groups.*`。
  - 宿主声明分组并登记全部文档到 Scalar。
  - DTO 属性写简明的 XML `<summary>`。
- 测试：
  - 生成器：XML 注释进根、`$defs`、属性与主构造参数，`remarks` 不进，中文换行相连。
  - OpenAPI：标题与说明分开、四种授权要求行、顶层标签与 `x-tagGroups`、分组文档的内容与权限范围、字段说明进参数 / 请求体 / 响应且按区域覆盖、分组文档端点与文档列表、分组校验与 snake_case。
