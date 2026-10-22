# RFC-0010 本地化

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：— | 参考实现：`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Localization.InMemory`；原语在 `XiHan.Framework.Primitives` | 一致性：`conformance/vectors/localization/`（P3 补）

## 摘要

文案不写在代码里。代码只引用消息键（专门的代码，如 `security.token.subject_missing`），每种语言一个文件（`Localization/<区域>.json`，含默认语言 `en-US`），任何地方用 `key.L()` 取当前语言的文案，`Error` 只携带键与参数并由宿主在 Egress 按调用方语言翻译。默认语言英文，内置与 XiHan.UI 相同的 10 种区域。

## 区域

| 区域 | 语言 |
| --- | --- |
| `en-US` | 英语（默认，也是语言文件的基准） |
| `zh-CN` | 简体中文 |
| `zh-TW` | 繁体中文 |
| `ja-JP` | 日语 |
| `ko-KR` | 韩语 |
| `de-DE` | 德语 |
| `es-ES` | 西班牙语 |
| `fr-FR` | 法语 |
| `pt-BR` | 葡萄牙语（巴西） |
| `ru-RU` | 俄语 |

区域名文法 `^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$`，比较不区分大小写，规范形式为语言小写、地区大写、文字首字母大写（`LocaleName`）。

## 消息键

```text
MessageKey = Domain ("." Segment)+
Domain     = 术语表领域词的小写（kernel、persistence、security …）或应用自定义的小写标识
Segment    = [a-z][a-z0-9_]*
```

每个包一个 `<领域>Messages` 静态常量类（`KernelMessages`、`SecurityMessages`、应用的 `OrdersMessages`），只定义键，不含文案；键的说明与占位符名写在 XML 文档里。原语层按主题拆分（`ManifestMessages`、`ResultMessages`）。

## 语言文件

- 位置 `Localization/<区域>.json`，内容是键 → 模板的平面对象；以 `<EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />` 嵌入。
- 10 种内置区域每种一个文件，键集完全一致，各区域模板的占位符与 `en-US` 一致（架构测试 `LocalizationTests`）。
- 模板语法：`{name}` 按名字、`{0}` 按位置，可带 `:format` 格式说明符（`{amount:N2}`、`{when:yyyy-MM-dd}`），按命中区域的文化格式化；`{{`、`}}` 转义。
- `MessageCatalog.FromEmbeddedResources(name, typeof(AnyTypeInThatAssembly))` 装载一个程序集的全部语言文件；同名目录只注册一次（`AddMessageCatalog`）。原语层与内核的目录由 `Localization.InMemory` 代为注册；子系统目录由其 Provider 在 `Configure` 里注册；`Plugins.InProcess` 与应用的目录由应用注册。

## 解析

- `IMessageResolver`（原语层）：`TryResolve(key, locale, arguments, out text)`，找不到键或占位符缺值即失败。
- `MessageScope`（原语层）：环境作用域（解析器 + 区域），`AsyncLocal` 传播。内核在 `App.InvokeAsync` 入口按 Baggage `xihan.locale` 安装一个作用域；`Localization.InMemory` 启动时用默认区域设置进程默认。
- `key.L()`、`key.L(("id", id))`、`key.L(count, amount)`（原语层扩展方法）：按 `MessageScope.Current` 解析，调用链里是调用方语言，其他地方是默认语言；没有作用域或找不到键时返回 `MessageTemplate.Describe(key, arguments)`，即 `kernel.feature.start_failed (id=…, reason=…)`。
- `IMessageLocalizer`（Ring 2）：显式区域的入口，`Resolve(key, locale, arguments)` 给出文案与实际命中的区域，`Localize(...)` 退回描述；`context.Localize(key, arguments)` 扩展方法按上下文区域解析。
- 回退链：请求区域 → 启用列表里同语言的区域 → 默认区域 → `en-US`；同一键先注册的目录优先。

## 错误

- `Error(code, messageKey, details)`：`MessageKey` 必须合法，`Details` 同时是模板参数；`Message` 按进程默认区域解析（日志与异常用），没有默认作用域时是键与参数的描述。
- 线上还原与异常转换用 `Error.Literal(code, text)`（无键，不翻译）；`WithCode` 换码保留键与参数，`WithText` 填入已解析文案。
- 幂等记录保存 `ErrorMessageKey` 与字符串化的 `ErrorDetails`，回放时仍可按调用方区域翻译。
- 架构测试 `ErrorCallSites_UseMessageKeyConstants`：正式包里的 `Error.*`/`new Error(` 第二个参数必须是 `<领域>Messages` 常量，`Error.Literal` 只允许在 `FrameworkException.ToError` 与 `IdempotencyRecord`。
- `FrameworkException`（配置与编程错误）不走消息键，消息为英文。

## 协商与宿主

- 调用的区域来自 Baggage `xihan.locale`；HTTP 宿主把 `Accept-Language` 按权重取候选，经 `LocalizationOptions.Negotiate` 写入该 Baggage（已有 Baggage 时不覆盖，没有装配本地化时取第一个合法值）。协商：候选精确命中启用列表 → 按语言命中第一个同语言区域 → 默认区域。
- `XiHan:Localization:DefaultLocale`（默认 `en-US`）与 `XiHan:Localization:Locales`（逗号分隔，默认内置 10 种）可改默认与启用列表，默认区域必须在列表里。
- Problem Details：`detail` 是 `IMessageLocalizer.Resolve(error, locale)` 的文案，`Content-Language` 是实际命中的区域；没有装配本地化或自由文本错误时 `detail` 是 `Error.Message`。
- `Profiles.Minimal` 与 `Profiles.Standard` 都装配 `InMemoryLocalizationFeature`（`role: default`，`localization.resources@1`）。

## 错误码

见 `conventions/error-codes.md` 中 `LOCALIZATION_*`。
