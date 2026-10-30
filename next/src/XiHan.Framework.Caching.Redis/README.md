# XiHan.Framework.Caching.Redis

`caching.distributed@1` 的 Redis Provider（Ring 3，`role: provider`）。注册后自动顶替内存实现的共享缓存：`ISharedCache`、`ICacheStore`、`IDistributedLock` 与 Idempotency 阶段中间件由本包提供，`ICache`（进程内）仍由 `Caching.InMemory` 提供。

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
- 键管理 `ICacheStoreInspector`（ADR-0055）：列键用单个端点的 `SCAN`（续页标记是游标，页大小是提示值，同一个键可能出现在两页；前缀里的 glob 字符转义），读元数据把 `PTTL` / `STRLEN` / `GETRANGE` 放进一个批次，按前缀删除逐页 `SCAN` 后 `DEL`。
- 变更通知 `ICacheChangeFeed`（ADR-0055）：每次写入与删除（真的删到了才算，按前缀删除总是）后同步通知本实例的订阅者，并发后不等地向频道 `<KeyPrefix>__changes` 发布，消息带来源标识，自己发出的不回送；第一次订阅时才订阅频道，释放存储即退订。`XiHan:Caching:Redis:PublishChanges`（默认 `true`）关掉发布，本实例的同步通知不受影响；发布失败不让写入失败。
- 健康项 `caching.redis.connection`：启动时同步 PING 一次，之后按 `XiHan:Caching:Redis:HealthProbeIntervalSeconds`（默认 15）在后台重探并缓存结果；PING 失败或超时条目 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 是异常消息），应用仍在服务。

## 测试

一致性套件需要真实 Redis：设置环境变量 `XIHAN_TEST_REDIS`（如 `localhost:6379`）后运行；未设置时用例跳过。CI 用服务容器运行。

## 依赖方向

依赖 `Caching.Abstractions` 与 `StackExchange.Redis`。

## 状态

P2：可用，通过 `Caching.Conformance` 套件。
