# ADR-0041 程序集级 `Expose` 缺省与只读声明

## 状态

已采纳（`spec/rfcs/0011-operations.md`、`spec/protocol/mcp.md`、`spec/conventions/operation-ids.md` 修订）。

## 日期

2026-10-28

## 背景

BasicApp 6.0 N2 第一批（操作表定稿）提出两项：

1. `OperationAttribute.Expose` 缺省 `Expose.All`，生成器在没写 `Expose` 时取 31。BasicApp 的规矩是"管理操作只 Http，选中的只读操作加 Mcp | Ai，内部调用加 Grpc"；它只能在自己的架构测试里用正则扫源码，强制每个 `[Operation]` 都写 `Expose =`。操作表定稿后 70 个操作里 63 个只走 HTTP、7 个超出 HTTP。
2. MCP 行为提示只按动词推导，只有 `get` / `list` 算只读，`departments.tree`、`users.list-online` 这类查询变体被标成"可能破坏"。BasicApp 因此暂不把它们暴露给 MCP / AI。

## 备选

1. 程序集缺省：`[assembly: OperationDefaults(Expose = …)]` 由生成器读取；或 MSBuild 属性（需要随包发 `CompilerVisibleProperty`，还要处理每个项目各写一份）；或运行期按程序集补缺省（内核零反射，做不到）。
2. "必须写明"的强制：生成器诊断默认开（会让所有没写 `Expose` 的现有代码编译失败）；默认关、应用以 `.editorconfig` 打开；或特性上加 `RequireExplicitExpose` 开关。
3. 只读：给 `get-*` / `list-*` 前缀定动词规则（`get-or-create` 这类动词会被误判）；在 `[Operation]` 上显式声明；或引入"效果"枚举（只读 / 幂等写 / 破坏性……，比需求宽）。
4. HTTP：只读的非标准动词仍走 POST；或与 `get` 同规则改用 GET。

## 决策

- 采用 1 的第一项：`Kernel.Operations.OperationDefaultsAttribute`（程序集级，`Expose` 缺省 `All`，Preview）。生成器对没写 `Expose` 的操作取程序集缺省，再没有就是 `Expose.All`；落定的值写进生成的 `OperationContract`，运行期登记表与之一致，运行期不读特性。写了特性却没写 `Expose` 等于声明缺省是 `All`。
- 采用 2 的第二项：`XHG004`（Warning，`isEnabledByDefault: false`）在"操作没写 `Expose` 且程序集没有 `OperationDefaults`"时报告；应用以 `dotnet_diagnostic.XHG004.severity = error` 打开即强制写明。声明了程序集缺省就不存在隐式的全部协议，打开了也不报。生成器诊断经编译选项过滤，测试验证了默认不报、打开后按配置的严重性报告。
- 采用 3 的第二项：`OperationAttribute.ReadOnly` 与 `OperationContract(…, readOnly)`；`OperationContract.IsReadOnly` = 动词是 `get` / `list` 或声明了只读。`create` / `update` / `delete` 不能声明只读：生成器报 `XHG005`（Error）并跳过该操作，手写契约构造抛 `ArgumentException`。不加前缀规则。
- MCP：`McpToolAnnotations.For` 对只读契约给只读、非破坏、幂等；其余动词规则不变。
- 采用 4 的第二项：`HttpRouteConvention` 对只读的非标准动词，请求没有嵌套对象时走 `GET {target}:{verb}`（集合级 `GET /departments:tree`、实例级 `GET /departments/{id}:tree`），有嵌套对象时仍是 POST——与 `get` / `list` 同一条"GET 不读请求体"规则（ADR-0015），也是 Google AIP-136 对只读自定义方法的做法。没声明只读的自定义动词照旧 POST。
- AI 工具目录不另加字段：`Microsoft.Extensions.AI` 的工具声明没有标准的只读位，消费方从 `OperationAIFunction` 的描述符取 `Contract.IsReadOnly`。插件声明（`plugin.proto`）暂不带只读位，插件操作只按动词推导。

## 理由

- 程序集缺省让"安全的缺省"由应用一次声明：忘写 `Expose` 的操作最多只走 HTTP，而不是全部协议；应用不必再扫源码。
- 诊断默认关是因为默认开会让每个没写 `Expose` 的现有项目编译失败；打开它是应用自己的选择。
- 只读必须显式声明：动词前缀猜不准，误判成只读的写操作会让 MCP 客户端跳过确认、HTTP 走可缓存的 GET，代价远大于多写一个属性。
- 一处声明驱动所有协议：MCP 提示、HTTP 方法、AI 消费方看的是同一个 `IsReadOnly`。

## 影响

- 公开 API（Kernel，Unshipped 内调整）：新增 `OperationDefaultsAttribute`、`OperationAttribute.ReadOnly`、`OperationContract.IsReadOnly`；`OperationContract` 构造函数末尾加可选参数 `readOnly`，原调用源码兼容。生成器新增 `XHG004`、`XHG005`。
- 行为：没有程序集缺省、没有只读声明的现有代码，生成结果与路由完全不变。声明只读的非标准动词的 HTTP 路由从 POST 变为 GET（前端生成的客户端随之变化）。
- 测试：生成器（程序集缺省落定、显式值优先、只读原样生成、`XHG004` 默认关 / 打开后报告 / 有缺省时不报、`XHG005`）、内核契约（`IsReadOnly` 与写动词拒绝）、MCP 提示、HTTP 路由推导与真实宿主上 `GET /orders/{id}:tree` 的查询串绑定及 OpenAPI 的 GET + 查询参数。
