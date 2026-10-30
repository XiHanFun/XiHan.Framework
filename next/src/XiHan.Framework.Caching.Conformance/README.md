# XiHan.Framework.Caching.Conformance

缓存一致性套件。任何 `ICacheStore` 实现（无论提供 `caching.local` 还是 `caching.distributed`）都必须继承 `CacheStoreConformance` 并通过全部用例。

```csharp
public sealed class RedisCacheStoreConformanceTests : CacheStoreConformance
{
    protected override ICacheStore CreateStore() => new RedisCacheStore(...);
}
```

套件覆盖：读写与未命中、覆盖、删除、不存在才写的原子性与并发、比较删、绝对过期、滑动过期延长、键区分大小写、大值、基于存储的分布式锁的互斥与到期，以及存储实现 `ICacheStoreInspector` 时的按前缀分页列键、读元数据与按前缀删除（不实现即跳过）。过期用例用真实时间等待，`ExpirationGrace` 可按 Provider 调整。

## 依赖方向

依赖 `Caching.Abstractions` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
