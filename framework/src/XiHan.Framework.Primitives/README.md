# XiHan.Framework.Primitives

## 关于

`XiHan.Framework.Primitives` 原语层：零行为、零依赖的原语与契约，如 `Result`、`Error`、`Maybe`、强类型标识、调用上下文与清单模型。框架里所有包都引用它，一般由其他包带入，只写共享契约的类库可以单独引用。

## 主要特性

* `Result` / `Error`：可恢复失败用结果返回，错误码形如 `<领域>_<KIND>_<NNN>`
* 消息键与模板，`key.L()` 按当前区域取文案
* 公开面只增不改，跨大版本不破坏

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Primitives --prerelease
```

### 使用

返回 `Result` 而不是抛异常来表达可预期的失败：

```csharp
using XiHan.Framework.Primitives;

public sealed class StockLedger
{
    private int _available = 10;

    public Result Reserve(int quantity)
    {
        if (quantity > _available)
        {
            return Error.Conflict("SHOP_CONFLICT_001", "shop.inventory.out_of_stock",
                MessageArguments.Of(("requested", quantity), ("available", _available)));
        }

        _available -= quantity;
        return Result.Success();
    }
}
```

## 主要类型

此库提供的主要类型：

* `Result` / `Result<T>`、`Error`：成功或携带错误的失败
* `Maybe<T>`、`TypedId<TTag, TValue>`：可选值与强类型标识
* `InvocationContext`：取消、截止、Trace、Baggage 与作用域服务
* `MessageTemplate`、`MessageArguments`：消息键与模板
* `Manifest`：`xihan.manifest.json` 的对象模型

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/primitives)

## 相关包

* 内核：[XiHan.Framework.Kernel](https://www.nuget.org/packages/XiHan.Framework.Kernel)

## 反馈与贡献

`XiHan.Framework.Primitives` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
