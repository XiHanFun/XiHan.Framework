# XiHan.Framework.Localization.Conformance

本地化一致性套件（RFC-0010）。Provider 的测试项目继承 `MessageLocalizerConformance` 并实现 `CreateLocalizer(catalogs, options)`，全部用例随之执行：回退链（精确区域 → 同语言的启用区域 → 默认区域 → 源语言）、按命中区域的文化格式化、缺模板或缺参数时退回描述、错误按键与细节翻译、按调用上下文的 Baggage 区域、先注册的目录优先。实现同时必须是 `IMessageResolver`。

`Localization.InMemory` 与 `Localization.Database` 都通过本套件。

## 依赖方向

依赖 `XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Testing` 与 xunit 的断言与扩展核心包。

## 状态

P3：可用。
