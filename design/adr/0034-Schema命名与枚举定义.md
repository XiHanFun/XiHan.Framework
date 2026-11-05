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

## 修订（2026-11-04）：任意 JSON 与可空标注的识别

缺陷修复，不另立 ADR。生成器按显示字符串比对类型：`ToDisplayString()` 带可空标注，`JsonNode?` 比不上 `"System.Text.Json.Nodes.JsonNode"`，于是 `JsonObject`（实现 `IDictionary<string, JsonNode?>`）、`JsonArray`、`IDictionary<string, JsonNode?>` 与 `JsonNode?` 属性都引用 `$defs/JsonNode`——一个带 `options` / `parent` / `root` 的对象，`JsonValue` 另成一个同形定义，`Uri?` 也被展开成 22 个属性的对象。Validate 阶段因此拒绝 `JsonObject` 里的标量值，OpenAPI 与前端类型跟着错。

- 规范：RFC-0011 写明类型映射（基元、字典、集合、任意 JSON），`JsonObject` 为 `object` + `additionalProperties: {}`、`JsonArray` 为 `array` + `items: {}`，`JsonNode` / `JsonValue` / `JsonElement` / `JsonDocument` / `object` 为 `{}`；可空只加 `null`、不改变形状。`protocol/grpc.md` 写明 `{}` 作数组元素或映射值同样是 JSON 文本。
- 实现：BCL 类型按所在命名空间与原始定义的元数据名识别（生成器内部的 `KnownTypes`，基元、字典接口、任意 JSON、`[JsonIgnore]` / `[JsonPropertyName]` 与 DataAnnotations），序列化上下文的特性与 `OperationDefaults` 按编译解析出的符号比对；可空标注、别名与泛型实参都不影响识别，别的命名空间里的同名类型仍是普通对象。
- 影响：带这些类型的操作重新生成后 `openapi.json`、MCP tool 描述与 `.proto` 变化（`$defs/JsonNode`、`$defs/JsonValue`、`$defs/JsonNodeOptions`、`$defs/Uri` 消失；gRPC 上 `JsonObject` 为 `map<string, string>`、`JsonArray` 为 `repeated string`，每个值一段 JSON 文本），请求与响应的 JSON 不变，Validate 阶段不再以 `KERNEL_VALIDATION_003` 拒绝 `JsonObject` / `JsonArray` 里的标量与数组；前端按新文档重新生成类型。
- 测试：`Kernel.Generators.Tests` 新增 3 例（任意 JSON 与可空标注的形状及 Validate 阶段的接受与拒绝、别的命名空间里的同名类型仍是对象、`[JsonIgnore]` / `[JsonPropertyName]` 含别名写法），`Protocols.Grpc.Tests` 新增 1 例（任意 JSON 的映射与数组以 JSON 文本往返、`.proto` 渲染）。

## 修订（2026-11-04，二）：忽略条件、非字符串键的字典与关键字转义

缺陷修复，不另立 ADR；来自上一节修订时发现的三处问题。

- `[JsonIgnore]` 原先不看 `Condition`，见到就把属性从 Schema 删掉，可 `WhenWritingDefault` / `WhenWritingNull` / `WhenWriting` / `WhenReading` / `Never` 的属性仍会序列化。改为与 System.Text.Json 一致：条件取 `[JsonIgnore]`（不写即 `Always`），没有时取序列化上下文的 `DefaultIgnoreCondition`；只有 `Always` 不进 Schema，其余照常出现，可能被省略的不进 `required`（`WhenWriting` / `WhenReading` 总有一个方向没有它，`WhenWritingDefault` 的值类型取默认值时不写出；不可空的引用按可空契约不会是 null，`WhenWritingDefault` / `WhenWritingNull` 不改变它的必填）。备选是按请求 / 响应拆两份形状（只读入的属性只在请求里），否决：同一类型在 OpenAPI 组件与 protobuf 消息里按名字合并，拆开会让同名组件形状不同（gRPC 启动即 `PROTOCOLS_CONFLICT_003`）。
- 键不是字符串的字典原先落进集合分支，输出成 `KeyValuePair` 的数组，与 System.Text.Json 写出的对象不符。改为任何键类型都是 `object` + `additionalProperties`，键的文本形式写在 `propertyNames`（整数与浮点的 `pattern`、`bool` 的 `True` / `False`、`char` 的单字符、枚举成员名并按 `DictionaryKeyPolicy` 改写、`Guid` 与日期时间与 `Uri` 的 `format`，以 .NET 10 的 System.Text.Json 实测为准）。System.Text.Json 不支持的键类型（运行期 `NotSupportedException`）选的是「照写形状 + 生成器警告 `XHG006`」，不是错误：键类型带 `[JsonConverter]` 或上下文登记了 `Converters` 时转换器可能实现了属性名的读写，生成器看不出来，这两种情况不报。Validate 阶段不断言 `propertyNames`，键文本不合法在解码时以 `KERNEL_VALIDATION_001` 失败；gRPC 仍是 `map<string, T>`。
- `OperationGenerator.Display` 用 `WithMiscellaneousOptions` 整体替换了完整格式的杂项选项，丢了关键字转义，命名空间或类型名是 C# 关键字（`@event`、`@class`）时生成的代码编不过。改为 `AddMiscellaneousOptions`；序列化上下文的属性名与根命名空间的关键字段也加 `@`。`Persistence.Generators` 的过滤规约类命名空间（默认显示格式不转义）与根命名空间同样修了。
- 影响：PublicAPI 不变；生成器新增诊断 `XHG006`（警告，缺省开启）。带条件忽略属性、非字符串键字典的操作重新生成后 `openapi.json`、MCP tool 描述与 `.proto` 变化（`KeyValuePairOf…` 定义消失，gRPC 由 `repeated KeyValuePairOf…` 变为 `map<string, T>`，线格式随之变化）；生成的登记代码里特殊类型改写成关键字（`global::System.String` → `string`），语义不变。
- 测试：`Kernel.Generators.Tests` 新增 3 例（忽略条件与上下文缺省、非字符串键的形状与 `DictionaryKeyPolicy` 及 `XHG006`、关键字命名空间与类型名），`Persistence.Generators.Tests` 新增 1 例（关键字命名空间），`Protocols.Grpc.Tests` 新增 1 例（带 `propertyNames` 的映射仍是 `map<string, T>`）。
