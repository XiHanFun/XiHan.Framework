# ADR-0034 Schema 命名：封闭泛型带实参名、字符串枚举进 `$defs`、OpenAPI 文档输入工厂

## 状态

已采纳（`spec/rfcs/0011-operations.md`、`spec/protocol/grpc.md`、`spec/protocol/http.md` 修订）。

## 日期

2026-10-28

## 背景

BasicApp 6.0 的 N0 冒烟在 `Kernel.Generators` 的 JSON Schema 上发现两项命名问题：

1. 封闭泛型的根 `title` 与 `$defs` 定义名只取 `ITypeSymbol.Name`：`PageResult<UserResponse>` 与 `PageResult<RoleResponse>` 都叫 `PageResult`。OpenAPI 组件提升"同名以先到者为准"会串型；gRPC 同名消息形状不同即 `PROTOCOLS_CONFLICT_003` 启动失败；前端 `openapi-typescript` 得到同名类型。第二个 list 操作就被阻塞。
2. `UseStringEnumConverter` 下枚举内联在属性上（`{"type":"string","enum":[…]}`），没有类型名：前端只能按"Schema 标题 + 属性名"起名（`UserResponseStatus`），同一枚举出现在多个 DTO 上生成多份、与后端枚举名对不上。

另外 `MapXiHanOpenApi` 里文档输入的组装（登记表、权限码注册表、认证方案、宿主端点、按区域取 `<操作 ID>.description`）是私有逻辑，BasicApp 的 `contracts export` 只能复制一份。

## 备选

1. 泛型命名：`<外层名>Of<实参名>`（NSwag 式，多个实参以 `And` 连接）；实参名作前缀 `UserResponsePageResult`（Swashbuckle 式）；带反引号与尖括号的 CLR 名（不是合法的组件名、proto 标识符与 TypeScript 标识符）。
2. 枚举：只有按字符串序列化的枚举进 `$defs`；整数枚举也进（定义只有 `type: integer`，没有值集，起名没有收益）；字符串枚举保持内联、由前端按属性起名。
3. gRPC 对 `$ref` 到枚举定义的字段：映射为 proto `enum`（值按序号上线，改线格式）；仍映射为 `string`（线格式不变）。
4. 文档输入：公开 `OpenApiDocumentSource.FromServices(services, options, endpoints, locale)`；或让 BasicApp 继续复制组装逻辑。

## 决策

- 采用 1 的 `Of` / `And`：生成器内部 `SchemaNames.Of(type)`——非泛型取类型名；封闭泛型 `<外层名>Of<实参 1>And<实参 2>…`，嵌套递归（`PageResultOfUserResponse`、`EnvelopeOfListOfUserResponseAndInt32`）；数组加 `Array`，可空值类型加 `Nullable` 前缀，基元类型用 CLR 名（`Int32`、`String`）。根 `title` 与 `$defs` 定义名同一规则，不同实参的同一泛型因此是不同的 Schema、组件与消息。
- 采用 2 的第一项：按字符串序列化的枚举进 `$defs/<枚举类型名>`（`{"type":"string","enum":[…]}`），属性以 `$ref` 引用，可空为 `anyOf: [{"$ref"}, {"type":"null"}]`；整数枚举保持内联 `integer`。
- 采用 3 的第二项：`ProtoSchema` 把 `$defs` 里没有 `properties` 的定义视为标量定义，`$ref` 到它的字段在引用处按该标量映射（枚举为 `string`，可空为 `optional`，数组与映射的元素同样是 `string`），不建消息也不建 proto enum，线格式不变；标量定义互相引用超过 16 跳以 `ProtoException` 失败而不是无限递归。
- `HttpRouteConvention` 对只有 Schema 的描述（插件代理）判断嵌套字段时，`$ref` 按 `$defs` 里的定义判定：指向标量定义的不算嵌套，指向对象定义的算，解析不到的仍按对象。
- 采用 4 的第一项：`OpenApiDocumentSource.FromServices(IServiceProvider, OpenApiOptions?, IEnumerable<Endpoint>, string? locale = null)`——登记表、HTTP 选项、权限码注册表、已装配认证处理器的方案名、标题 / 版本 / `servers`（标题缺省取宿主应用名、版本缺省取框架版本）、普通端点与按 `locale`（为 null 取本地化默认区域）的摘要；`MapXiHanOpenApi` 改为调用它。
- 顺带：`UniqueDefinitionName` 改按已占位的名字判重——原先按已写出的定义体判重，嵌套生成途中遇到的同名类型会撞名。

## 理由

- `Of` / `And` 是 OpenAPI 生态的通行写法，产出的名字同时是合法的组件名、proto 标识符与 TypeScript 标识符，前端生成器不必再改名。
- 字符串枚举是有名字的值集，进 `$defs` 后 OpenAPI 组件、前端枚举类型与后端枚举同名；整数枚举的 Schema 只有 `integer`，起名没有收益，保持内联不改前端现状。
- gRPC 保持 `string`，因为线格式是契约的一部分：proto enum 按序号上线会改变已有客户端的编解码，而字符串枚举在 JSON 侧本来就是名字。
- 文档输入工厂让导出工具与端点共用一条组装路径，`contracts export` 与 `/openapi.json` 逐字节一致由测试保证。

## 影响

- 契约变化（Preview）：封闭泛型的 `title` / 组件名 / 消息名改变（`PageResult` → `PageResultOfUserResponse`），字符串枚举从属性内联变成 `$ref` 到定义；重新导出 `openapi.json` 与 `.proto` 后消费方要重新生成。请求 / 响应 JSON 与 gRPC 线格式不变。
- 新公开 API：`OpenApiDocumentSource.FromServices`。`JsonSchemaBuilder`、`SchemaNames`、`TypeInfoNames` 是生成器内部类型。
- 测试：`Kernel.Generators.Tests`（两个 `PageResult<T>` 与嵌套多实参泛型的命名、枚举定义与可空引用、整数枚举仍内联）；`Protocols.Grpc.Tests`（标量定义的映射与线格式往返、两个封闭泛型消息并存且无冲突、宿主 `/operations.proto`、定义成环失败）；`Protocols.Http.Tests`（根与枚举的组件提升、查询参数引用枚举组件、`HasNestedFields` 对 `$ref` 的判定、工厂与端点文档逐字节一致）。
