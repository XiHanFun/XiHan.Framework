# ADR-0299 SDK 里绑定的 JSON 写出遮值与 Python 的只读映射

## 状态

已采纳（六个 SDK 各自的 `dev` 上落地：Go `Binding.MarshalJSON`、Java `ResolvedBinding` 实现 Jackson 的 `JsonSerializable`、PHP `ResolvedBinding` 实现 `JsonSerializable`、TypeScript 的 `toJSON` 改成属性名升序；Python 的 `Binding.properties` 与 `Envelope.extensions` 换成 SDK 自己的只读映射；规范不改，框架代码不改）

## 日期

2026-11-25

## 背景

ADR-0285 让下发给插件的绑定带上属性，规范 `plugin/lifecycle.md` §7.2 要求 SDK 的日志、错误与诊断输出不得带主值与属性值（连接串、口令可能就在里面）。六个 SDK 交给作者的绑定类型都已把各自语言的调试出口遮住（`String()` / `toString()` / `Debug` / `repr` / `inspect` / `__debugInfo`），但 JSON 序列化不一致：

- TypeScript 的 `toJSON` 只写 `{"name","type","propertyNames"}`，属性名按线上的先后。
- Go（`encoding/json`）、Java（Jackson）、PHP（`json_encode`）照样写出主值与全部属性值。Go 的 slog 只对顶层值调 `LogValue`，`[]Binding` 与 `Configuration` 走 `encoding/json`，所以 SDK 自己的 JSON 日志处理器记绑定列表或整个配置时也会写出值；PHP 的 `Logger` 归一化属性时对嵌套对象用 `get_object_vars`，嵌在数组里的绑定同样带值。

用 JSON 日志器记下绑定或配置是常见写法，这等于把凭据写进日志。

另一件事：Python SDK 用 `types.MappingProxyType` 做 `Binding.properties` 与 `Envelope.extensions` 的只读视图，代价是 `Binding`、带绑定的 `Configuration`、`Envelope` 不能再 `copy.deepcopy`、`pickle`、`dataclasses.asdict`（ADR-0285 的迁移说明里写过这条）。

## 备选

1. **统一遮值**（采纳）：Go、Java、PHP 的 JSON 写出与 TypeScript 的 `toJSON` 同形，只有绑定名、类型与属性名。代价是合法的 JSON 序列化也丢值，要持久化值的代码自己取 `value` / `properties`；遮过的 JSON 读不回原值。
2. **统一不遮**：去掉 TypeScript 的 `toJSON`，JSON 照写全部值，只靠文档提醒作者别把绑定交给日志。否决：与规范「SDK 的诊断输出不得带值」的方向相反，且作者很难意识到「整个配置写进 JSON 日志」会带出凭据。
3. **保持现状**：否决，同一个插件换一种语言写，日志里有没有凭据取决于语言。
4. **Java 用注解（`@JsonValue` / `@JsonIgnore`）**：否决，关掉 `MapperFeature.USE_ANNOTATIONS` 的映射器会绕过；实现 `com.fasterxml.jackson.databind.JsonSerializable` 在注解之前生效，调用不经反射，模块路径上不必 `opens`。jackson-databind 本来就是 Java SDK 的依赖，不新增依赖。
5. **Python 继续用 `MappingProxyType`**：否决，作者拷贝或序列化配置时直接报错，而原因是 SDK 的实现细节。
6. **Python 的只读映射不是 `dict` 子类**：否决，`dataclasses.asdict` 遇到不是 `dict` 的 Mapping 时整个 `copy.deepcopy`，得到的还是只读类型，`json.dumps` 写不了；遇到 `dict` 子类时按 `type(obj)(键值对)` 重建。所以只读类型做成禁写的 `dict` 子类，并让「以类来调用」时返回普通 `dict`，`asdict`、`.copy()` 与 `|` 的结果都是可改写、可写成 JSON 的普通 `dict`。

## 决策

1. 六个 SDK 的绑定写成 JSON 时都是 `{"name":<绑定名>,"type":<类型>,"propertyNames":[<属性名>…]}`，属性名按序数升序（TypeScript 与 Java 按 UTF-16 码元，Go 与 PHP 按字节；属性名的文法只有 ASCII，几种排法结果相同），没有属性时是空数组；同一个绑定在六个 SDK 里得到同一份文本。不提供从这份 JSON 读回绑定的路径。
   - Go：值接收者的 `MarshalJSON`；`LogValue` 的组成员随之由 `properties` 改名 `propertyNames`，单个绑定与切片里的绑定在 JSON 日志里同名。
   - Java：`ResolvedBinding` 实现 Jackson 2 的 `JsonSerializable`（`serialize` 与 `serializeWithType`）。
   - PHP：`ResolvedBinding` 实现 `\JsonSerializable`，属性名一律写成字符串；SDK 的 `Logger` 写嵌在数组里的绑定也经它。`var_export`、`serialize`、`(array)` 与 `get_object_vars` 不经任何钩子，拦不住，类的文档写明。
   - TypeScript：`toJSON` 与调试出口里的属性名改为升序（以前按线上的先后）。
2. Python 新增内部模块 `_frozen`：`FrozenDict`（禁写的 `dict` 子类，内容哈希 `hash(frozenset(items))`，`copy` / `deepcopy` / `pickle` 经 `__reduce__` 还原成同一类型）与 `RedactedDict`（`repr` 只带键、值写成 `...`）。`Envelope.extensions` 用 `FrozenDict`，`Binding.properties` 用 `RedactedDict`，对外仍声明为 `Mapping[str, str]`。`Binding` 与 `Envelope` 因此都可哈希。
3. 六个 SDK 的规范指针升到含 ADR-0292、ADR-0293 的规范提交；Go 的一致性模块随之升级，把规范新增的五条宿主一侧的固定 `reason` 列入宿主一侧的名单。插件一侧 gRPC 服务端的接收上限六个 SDK 都是各自库的缺省 4 MiB，满足 `plugin/lifecycle.md` 的新要求，不用改。

## 理由

- 遮值放在类型自己的序列化出口上，作者不必记得「记日志前先脱敏」；要值的地方本来就是读 `value` / `properties`，不受影响。
- 同一份 JSON 文本让跨语言的日志与测试可以逐字比较。
- Python 的拷贝与序列化是标准库的基本能力，SDK 的只读语义不该让它们失效。

## 影响

- **破坏性**（SDK 仍在 0.x，按各仓库的迁移说明处理）：
  - Go：`json.Marshal(Binding)` 不再写 `Name`、`Value`、`Properties`；slog 里绑定组的成员 `properties` 改名 `propertyNames`。
  - Java：Jackson 不再写出 `value` 与 `properties`，也读不回 `ResolvedBinding`；公开签名里多了 Jackson 2 的接口，将来迁 Jackson 3 要一起改。
  - PHP：`json_encode` 不再带 `value` 与 `properties`。
  - Python：`isinstance(x, MappingProxyType)` 不再成立（判断只读映射改用 `collections.abc.Mapping`），`isinstance(x, dict)` 现在为真。
  - TypeScript：`JSON.stringify(binding)` 里属性名的先后变了。
- **规范**：不改。
- **框架**：代码不改；文档站 `docs/sdks/*.md` 的能力说明与迁移说明同步。
- **待定**：Python 的 `JsonLogHandler` 遇到作者把 `binding.properties` 本身放进 `extra` 时会写出值（它是 `dict` 子类，按 JSON 对象写），要不要在处理器里专门遮 `RedactedDict` 另议。
