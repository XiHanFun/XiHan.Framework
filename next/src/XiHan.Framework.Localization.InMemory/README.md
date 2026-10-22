# XiHan.Framework.Localization.InMemory

内存本地化。清单 `role: default`，提供 `localization.resources@1`；`Profiles.Minimal` 与 `Profiles.Standard` 都装配它。

## 语义

- `MessageLocalizer` 汇集 DI 里的全部 `MessageCatalog`，既是 `IMessageLocalizer`（显式区域）也是 `IMessageResolver`（环境作用域）；同一键先注册的目录优先。
- 回退链：精确区域 → 启用列表里同语言的其他区域 → 默认区域 → 源语言 `en-US`；占位符按命中区域的文化格式化（`{amount:N2}` 在 `de-DE` 得到 `1.234,50`）。
- 找不到键或占位符缺值时整条模板不采用，退回键与参数的描述（`kernel.feature.start_failed (id=…, reason=…)`）。
- 装配时注册原语层与内核的语言文件目录；启动时把本地化器设为进程默认 `MessageScope`（默认区域），于是 `KernelMessages.ManifestEmpty.L()`、`Error.Message` 在任何地方都有默认语言文案；内核在每次调用入口按 Baggage `xihan.locale` 安装调用方区域的作用域。
- 选项 `XiHan:Localization:DefaultLocale`（默认 `en-US`）与 `XiHan:Localization:Locales`（逗号分隔，默认内置 10 种）。

## 依赖方向

依赖 `XiHan.Framework.Localization.Abstractions`。

## 状态

P2：可用。
