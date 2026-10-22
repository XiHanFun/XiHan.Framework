# XiHan.Framework.Caching.Redis

`caching.distributed@1` 的 Redis Provider（Ring 3，`role: provider`）。注册后自动顶替内存实现的共享缓存：`ISharedCache`、`IDistributedLock` 与 Idempotency 阶段中间件由本包提供，`ICache`（进程内）仍由 `Caching.InMemory` 提供。

```csharp
builder.AddXiHan(x => x.UseFeature<InMemoryCachingFeature>().UseFeature<RedisCachingFeature>());
```

连接来自 `XiHan:Caching:Redis:Configuration`，或绑定 `cache`（`cache:distributed`）的值；两者都没有时装配失败。
值的序列化按顺序取：已注册的 `ICacheSerializer` → 已注册的 `JsonSerializerOptions`（须带 `TypeInfoResolver`）→ 反射版 `JsonCacheSerializer`（本包不支持 AOT，允许反射）。

## 语义

- 值前带 1 字节头：`0` 绝对过期，`1` 滑动过期并跟随 8 字节窗口毫秒数；滑动条目在读取时 `EXPIRE` 延长。
- 不存在才写用 `SET NX PX`；比较删用 Lua 脚本比较载荷后 `DEL`，原子。
- 键加前缀 `XiHan:Caching:Redis:KeyPrefix`（默认 `xihan:`）；未指定过期时用 `DefaultTimeToLiveSeconds`（默认 300）。
- 连接与超时失败抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`。

## 测试

一致性套件需要真实 Redis：设置环境变量 `XIHAN_TEST_REDIS`（如 `localhost:6379`）后运行；未设置时用例跳过。CI 用服务容器运行。

## 依赖方向

依赖 `Caching.Abstractions` 与 `StackExchange.Redis`。

## 状态

P2：可用，通过 `Caching.Conformance` 套件。
