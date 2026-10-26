# XiHan.Framework.Caching.InMemory

内存缓存。清单 `role: default`，提供 `caching.local@1` 与 `caching.distributed@1`：没有显式 Provider 时，进程内缓存与"共享"缓存都由它承担（只在单实例部署下正确）。

## 语义

- 有界 LRU：`XiHan:Caching:InMemory:MaxEntries`（默认 10000），满时淘汰最久未访问的条目。
- 过期：绝对存活或滑动过期；都不设时用 `XiHan:Caching:InMemory:DefaultTimeToLiveSeconds`（默认 300）。过期条目在访问时清理。
- 字节存储写入复制字节，读取返回不可修改的视图；类型化缓存（`ICache` / `ISharedCache`）直接存对象引用、不序列化，调用方不得修改取回的对象。
- 被选中提供 `caching.distributed` 时注册 `ISharedCache`、`ICacheStore`（字节级契约，指向同一个存储）、分布式锁与幂等中间件；当另一个显式 Provider（如 `Caching.Redis`）被选中提供 `caching.distributed` 时，本包只注册 `ICache`。

## 依赖方向

依赖 `XiHan.Framework.Caching.Abstractions`。

## 状态

P2：可用，通过 `Caching.Conformance` 套件。
