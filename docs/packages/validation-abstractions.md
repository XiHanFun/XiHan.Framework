# XiHan.Framework.Validation.Abstractions

> 校验抽象：验证错误契约与统一的验证异常类型。体量很薄，只定义「如何表达验证错误」和「校验失败抛什么异常」两件事。

- **NuGet**：`XiHan.Framework.Validation.Abstractions`
- **模块类**：`XiHanValidationAbstractionsModule`
- **所在层**：应用层
- **关键依赖**：仅 `XiHan.Framework.Core` 与 BCL（`System.ComponentModel.DataAnnotations`、`Microsoft.Extensions.Logging`），无第三方依赖

## 概述

这个包是数据校验能力的**抽象层**。它只定义两个类型：一个携带 `ValidationResult` 列表的契约接口 `IHasValidationErrors`，以及一个可自记日志、带日志级别的验证异常 `XiHanValidationException`。上层与实现包 [XiHan.Framework.Validation](./validation) 依赖这些契约做依赖倒置，从而在框架各处以统一方式表示「数据校验失败」。

## 何时使用

- 你想以框架统一的方式抛出「数据校验失败」异常（`XiHanValidationException`）。
- 你想让某个类型对外暴露一组验证错误（实现 `IHasValidationErrors`）。
- 你在做全局异常处理，需要识别验证类异常并读取其错误明细。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Validation.Abstractions
```

```csharp
[DependsOn(typeof(XiHanValidationAbstractionsModule))]
public class MyModule : XiHanModule { }
```

模块类的 `ConfigureServices` 不注册任何额外服务。通常无需直接引用本包——引用实现包 [XiHan.Framework.Validation](./validation) 时会自动带上。

## 核心能力

- **验证错误契约（`IHasValidationErrors`）**：暴露 `IList<ValidationResult> ValidationErrors`（基于 BCL 的 `System.ComponentModel.DataAnnotations.ValidationResult`）。
- **统一验证异常（`XiHanValidationException`）**：继承框架 `XiHanException`，同时实现 `IHasValidationErrors`、`IHasLogLevel`（`LogLevel` 默认 `Warning`）与 `IExceptionWithSelfLogging`；可用一组 `ValidationResult` 构造，并通过 `Log(ILogger)` 把错误明细（含成员名）按其日志级别自行写入日志。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `IHasValidationErrors` | `IList<ValidationResult> ValidationErrors { get; }` |
| `XiHanValidationException` | 统一验证失败异常。构造重载：无参、`(string message)`、`(IList<ValidationResult>)`、`(string, IList<ValidationResult>)`、`(string, Exception)`；属性 `ValidationErrors`、`LogLevel`（默认 `Warning`）；方法 `void Log(ILogger logger)`（错误为空则跳过，否则汇总为多行文本按 `LogLevel` 输出） |

## 使用示例

在校验失败时抛出统一异常：

```csharp
var errors = new List<ValidationResult>
{
    new("名称不能为空", new[] { nameof(Product.Name) })
};

throw new XiHanValidationException("数据校验失败", errors);
```

在全局异常处理中识别并记录：

```csharp
if (ex is XiHanValidationException vex)
{
    vex.Log(logger);                 // 按 vex.LogLevel（默认 Warning）输出错误明细
    var details = vex.ValidationErrors;
}
```

## 依赖模块

- [XiHan.Framework.Core](./core) — 提供 `XiHanException` 基类、`IHasLogLevel` / `IExceptionWithSelfLogging` 等日志与异常抽象。
- 仅依赖 Core 与 BCL，不引入第三方依赖。

## 相关模块

- [XiHan.Framework.Validation](./validation) — 本抽象的实现/集成包（当前为薄占位）。
