# XiHan.Framework.Primitives

原语层（Ring 0）。零行为的原语与契约，跨语言语义由 XiHan Spec 定义。

## 责任边界

- 包含：`Result<T>` / `Result`、`Error`、`Maybe<T>`、`TypedId<T>`、`InvocationContext`、清单模型、`Stability` / `ApiLevelAttribute`、`LifecycleState`、`FrameworkException`、`FrameworkInfo`。
- 不包含：任何实现、基类、宿主、依赖注入、反射。

## 依赖方向

不依赖任何包。被 `XiHan.Framework.Kernel` 及其上所有环引用。

## 承诺

只增不改。公开面以 `PublicAPI.Shipped.txt` 为准，已发布行禁止修改；若必须破坏，发布新包 ID 并存。

## 状态

P0：空包，公开面待 RFC-0001 采纳后在 P1 填充。
