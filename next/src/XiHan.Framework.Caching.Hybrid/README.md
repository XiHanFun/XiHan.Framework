# XiHan.Framework.Caching.Hybrid

两级缓存（Ring 3，`role: provider`，RFC-0006「两级缓存」）。提供 `caching.local@1`、依赖 `caching.distributed@1`：装配后 `ICache` 变成「L1 进程内对象缓存 + L2 被选中的共享缓存」，`ISharedCache` 仍是 L2 本身。

```csharp
builder.AddXiHan(x => x.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>());
```

```json
{ "XiHan": { "Caching": { "Hybrid": { "LocalCapacity": 10000, "LocalTimeToLiveSeconds": 30 } } } }
```

## 语义

- 读：L1 命中直接返回；未命中读 L2，命中后回填 L1。批量读先在 L1 取全部，只把未命中的键一次交给 L2。
- 写与删：先 L2 再 L1，L2 失败时 L1 不留下别的实例看不到的值；`GetOrCreate` 两级都未命中才执行工厂（由 L2 判定与写入），结果回填 L1。删除报告两级任一存在过。
- L1 的存活时间是条目过期（绝对或滑动时长）与 `LocalTimeToLive`（默认 30 秒）的较小者，滑动过期在 L1 里按绝对时间处理；`LocalCapacity`（默认 10000）满时淘汰最久未访问的条目。
- 跨实例失效（ADR-0055）：L2 的存储实现了 `ICacheChangeFeed`（Redis）时订阅它，本实例与其他实例的写入与删除、管理界面按键或按前缀的删除，都让 L1 里的这些键立即失效；`InvalidatesAcrossInstances` 表示是否订阅到了，`HybridCache` 释放即退订。
- 没有变更通知时（远程插件等），别的实例的写入不会主动失效本实例的 L1：读到旧值的时间最长为 `LocalTimeToLive`，通知丢失时也以此为上限；需要更强一致的键直接用 `ISharedCache`。
- `ICache.Store` 是 L2 的字节存储；L1 直接存对象引用，调用方不得修改取回的对象。
- 单实例开发环境下 L2 可以是内存实现（`Caching.InMemory` 的 `caching.distributed`），测试零外部基础设施。

## 依赖方向

依赖 `XiHan.Framework.Caching.Abstractions`，无第三方依赖。

## 状态

P5：可用。
