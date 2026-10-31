# XiHan.Framework.Profiles.Minimal

Minimal 组合包（元包）：一个包引用带上原语层、内核、ASP.NET Core 宿主与内存本地化，`UseMinimalProfile()` 只装配本地化默认实现，不装配任何业务子系统。适用于边缘服务、工具与插件宿主。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Profiles.Minimal` |
| 环层 | Ring 6（组合包） |
| 能力 | 自身不声明；装配的 `InMemoryLocalizationFeature` 提供 `localization.resources@1`（`role: default`），内核提供 `kernel.*@1` |
| 特性入口 | `UseMinimalProfile()` |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Primitives`、`XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Localization.InMemory` |
| AOT | 兼容 |
| 稳定性 | Stable |

## 概述

组合包本身没有特性与清单，只做两件事：用包引用把一组包一次带进来，用一个 `AppBuilder` 扩展方法装配其中的默认实现。`UseMinimalProfile()` 只调用 `UseFeature<InMemoryLocalizationFeature>()`：它注册原语层与内核的语言文件目录并设置进程默认消息作用域，于是 `key.L()`、`Error.Message` 与 Problem Details 的 `detail` 在最小组合下就有文案（缺省 `en-US`，内置 10 种区域，[国际化](../guide/localization)）。内核与宿主由 `AddXiHan()` / `UseXiHan()` 提供，管道、探针与 Problem Details 随宿主可用。

## 何时使用

- 只需要内核管道、操作契约与宿主，业务子系统自己挑：从 Minimal 开始，按需 `UseFeature`。
- 命令行工具与 MCP stdio 服务：`samples/SimpleCli` 用 Minimal 组合加 `Hosting.CommandLine`。
- 业务服务需要持久化、缓存、多租户、安全与消息：用 [Profiles.Standard](./profiles-standard)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Profiles.Minimal" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Minimal;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseMinimalProfile());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `MinimalProfile` | 静态类，扩展方法 `UseMinimalProfile(this AppBuilder)` |

## 行为与限制

- 装配的 `InMemoryLocalizationFeature` 是 `role: default`：再 `UseFeature` 一个显式的本地化 Provider（如 `Localization.Database`）即顶替它，被顶替的默认实现不装配、不启动。
- 组合包不读配置，也不改任何默认值；各特性的配置节照常生效。

## 相关

- [能力包与清单](../guide/modularity)、[宿主与 Web 应用](../guide/web)
- [Profiles.Standard](./profiles-standard)、[Profiles.Full](./profiles-full)、[Localization.InMemory](./localization-inmemory)
