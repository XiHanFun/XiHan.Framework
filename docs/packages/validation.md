# XiHan.Framework.Validation

> 数据校验的集成入口。**当前为薄占位**——仅有模块类，核心类型都在抽象包。

- **NuGet**：`XiHan.Framework.Validation`
- **模块类**：`XiHanValidationModule`
- **所在层**：应用层
- **关键依赖**：仅框架内部 `XiHan.Framework.Core` 与 `XiHan.Framework.Validation.Abstractions`，无第三方依赖

## 概述

这个包是数据校验能力的**实现/集成入口**，通过 `XiHanValidationModule` 把校验体系接入框架的模块化生命周期，并以 `[DependsOn]` 传递引入 [XiHan.Framework.Validation.Abstractions](./validation-abstractions) 提供的验证错误契约与统一验证异常。

> **现状说明**：截至当前源码，本包仅含模块类 `XiHanValidationModule`，其 `ConfigureServices` 未注册任何额外服务，实质内容全部来自抽象包。校验的核心类型（`IHasValidationErrors`、`XiHanValidationException`）定义在 [Validation.Abstractions](./validation-abstractions)。**若你只需要抛出/识别验证异常，直接使用抽象包即可**；引用本包主要是为了以模块方式声明「启用校验能力」并为后续实现预留挂载点。

## 何时使用

- 你希望以模块方式声明「启用校验能力」，通过 `[DependsOn(typeof(XiHanValidationModule))]` 一并引入抽象契约。
- 你在为校验体系预留集成点（后续实现将挂载于此模块）。
- 若只需契约与异常类型本身，用 [Validation.Abstractions](./validation-abstractions) 更直接。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Validation
```

```csharp
[DependsOn(typeof(XiHanValidationModule))]
public class MyModule : XiHanModule { }
```

模块通过 `[DependsOn(typeof(XiHanValidationAbstractionsModule))]` 引入校验抽象；自身 `ConfigureServices` 不注册额外服务。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanValidationModule` | 模块入口，`[DependsOn]` 抽象模块并参与模块化生命周期；当前无服务注册 |

校验的错误契约（`IHasValidationErrors`）与统一异常（`XiHanValidationException`）由抽象包提供，用法见 [Validation.Abstractions](./validation-abstractions)。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Validation.Abstractions](./validation-abstractions) — 验证错误契约与 `XiHanValidationException` 的定义处。

## 相关模块

- [XiHan.Framework.Validation.Abstractions](./validation-abstractions) — 抽象包，本包的核心内容来源。
