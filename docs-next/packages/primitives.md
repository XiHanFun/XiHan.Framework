# XiHan.Framework.Primitives

原语层：零行为、零依赖的原语与契约，跨语言语义由 XiHan Spec 的 RFC-0001 定义。框架里所有包都引用它，应用代码里的 `Result`、`Error`、`InvocationContext`、清单模型都来自这里。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Primitives` |
| 环层 | Ring 0（原语） |
| 能力 | — |
| 特性入口 | — |
| 配置节 | — |
| 依赖 | 无 |
| AOT | 兼容 |
| 稳定性 | 冻结承诺：只增不改，跨大版本不破坏 |

## 概述

原语层只放跨语言都需要、并且十年内不应改变语义的类型：结果与错误、可选值、强类型标识、调用上下文、管道阶段、生命周期状态、稳定性标记、清单的对象模型与格式校验、消息键与模板。它不包含任何实现、基类、宿主、依赖注入、反射或序列化代码，也不引用任何包。

冻结规则：只允许新增类型与成员，不允许改签名、默认值与语义；公开面以 `PublicAPI.Shipped.txt` 为准，CI 禁止修改已发布的行。必须破坏时发布新包 `XiHan.Framework.Primitives.V2` 与旧包并存。

## 安装与启用

一般不单独引用：内核与所有子系统包都依赖它，组合包会带上它。只写共享契约的类库可以单独引用：

```xml
<PackageReference Include="XiHan.Framework.Primitives" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Result` / `Result<T>` | 成功或携带 `Error` 的失败；值与错误可隐式转换；`default` 视为未初始化的失败 |
| `Error` / `ErrorCode` | 错误码 `<域>_<KIND>_<NNN>`、消息键、细节与内部错误；工厂方法校验 KIND 段；`Error.Literal` 承载线上还原与异常转换的文本 |
| `Maybe` / `Maybe<T>` | 类型层面的「可能没有」，`default` 即无值 |
| `ITypedId<TValue>` / `TypedId<TTag, TValue>` | 强类型标识，线上编码为底层值 |
| `InvocationContext` | 取消令牌、截止时间、W3C Trace、Baggage、作用域对象袋与作用域服务 |
| `BaggageKeys` | 已登记的 Baggage 键：`xihan.tenant`、`xihan.principal`、`xihan.correlation`、`xihan.idempotency-key`、`xihan.locale` |
| `PipelineStage` / `PipelineStageExtensions` | 固定的十一个管道阶段及其清单名 |
| `LifecycleState` / `LifecycleStateExtensions` | 九个生命周期状态与合法迁移（`CanTransitionTo`、`IsServing`） |
| `Stability` / `ApiLevelAttribute` | 稳定性分级 |
| `FrameworkException` | 带错误码的异常基类，`ToError()` 转成 `Error` |
| `FrameworkInfo` | 框架名称、版本、规范版本、仓库与文档地址；版本由构建期常量提供 |
| `MessageTemplate` / `MessageArguments` / `MessageKeyExtensions` | 消息键文法、`{name}` / `{0}` 模板格式化、参数字典、`key.L()` |
| `IMessageResolver` / `MessageScope` | 消息解析器契约与环境作用域 |
| `Manifests.*` | 清单对象模型：`Manifest`、`CapabilityDeclaration`、`CapabilityRequirement`、`Binding`、`Contributions`、`PipelineContribution`、`ConfigContribution`、`ManifestKind`、`ManifestRole`、`CapabilityId`、`ManifestId` |

## 使用示例

结果与错误：

```csharp
using XiHan.Framework.Primitives;

namespace Shop.Inventory;

public static class InventoryErrors
{
    public const string OutOfStock = "SHOP_CONFLICT_001";
}

public static class InventoryMessages
{
    public const string OutOfStock = "shop.inventory.out_of_stock";
}

public sealed class StockLedger
{
    private int _available = 10;

    public Result Reserve(int quantity)
    {
        if (quantity > _available)
        {
            return Error.Conflict(InventoryErrors.OutOfStock, InventoryMessages.OutOfStock,
                MessageArguments.Of(("requested", quantity), ("available", _available)));
        }

        _available -= quantity;
        return Result.Success();
    }
}
```

强类型标识：

```csharp
using XiHan.Framework.Primitives;

namespace Shop.Inventory;

public sealed class WarehouseTag;

public static class Warehouses
{
    public static TypedId<WarehouseTag, string> Main { get; } = new("main");
}
```

消息键取当前区域的文案（按 `MessageScope.Current`：调用链里是调用方语言，否则是进程默认区域；找不到键时返回「键 (参数)」）：

```csharp
var text = InventoryMessages.OutOfStock.L(("requested", 3), ("available", 1));
```

## 行为与限制

| 规则 | 说明 |
| --- | --- |
| 成功值非空 | `Result.Success<T>(value)` 拒绝 `null`；可能没有值时用 `Result<Maybe<T>>` |
| 消息键文法 | `^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$`，首段是领域，最长 128 个字符（`MessageTemplate.MaxKeyLength`） |
| Baggage 键 | 键名格式 `xihan.<name>`（`BaggageKeys.IsWellFormed`），登记表见 `BaggageKeys.Registered`；HTTP 宿主入站只保留格式合法的 `xihan.` 键；主体只传 ID，敏感信息不进 Baggage |
| 清单校验 | `Manifest.Validate()` 校验字段格式与跨字段约束，失败为 `PRIMITIVES_VALIDATION_001` |

| 错误码 | 情形 |
| --- | --- |
| `PRIMITIVES_VALIDATION_001` | 原语参数非法：空错误码、空消息键、清单跨字段约束等 |
| `PRIMITIVES_VALIDATION_002` | 使用了未初始化的 `Result`（`default` 值） |

一致性向量在 XiHan Spec 的 `conformance/vectors/primitives/`：错误码格式、生命周期迁移、能力依赖文本、Baggage 键，单元测试直接回放；Go SDK 的 `primitives` 包通过同一套向量。

## 相关

- [结果与错误](../guide/errors)
- [国际化](../guide/localization)
- [生命周期与健康](../guide/lifecycle)
- [Kernel](./kernel)
- 规范：RFC-0001《原语》、RFC-0003《调用上下文与传播》、RFC-0010《本地化》
