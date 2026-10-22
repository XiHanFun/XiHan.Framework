# RFC-0006 缓存、分布式锁与幂等

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：— | 参考实现：`XiHan.Framework.Caching.Abstractions`、`XiHan.Framework.Caching.InMemory`、`XiHan.Framework.Caching.Redis` | 一致性：`XiHan.Framework.Caching.Conformance`

## 摘要

缓存子系统只定义一个字节级存储契约，类型化缓存、分布式锁与幂等去重都建立在它之上。Provider 只需实现五个原子操作，就自动获得锁与幂等。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `caching.local@1` | 进程内缓存，`ICache` | `Caching.InMemory` |
| `caching.distributed@1` | 跨进程共享缓存，`ISharedCache`、`IDistributedLock`、幂等中间件 | `Caching.InMemory`（单实例下正确；多实例换 `Caching.Redis`） |

内存实现同时声明两种能力；另有显式 Provider 提供 `caching.distributed` 时，内存实现只为被选中的 `caching.local` 注册服务（RFC-0002 第 4 条）。

## 存储契约

| 操作 | 语义 |
| --- | --- |
| `Get` | 不存在或已过期为无值；滑动过期的条目在读取时延长 |
| `Set` | 写入或覆盖 |
| `TryAdd` | 不存在（或已过期）才写入，原子 |
| `Remove` | 删除并报告是否存在 |
| `TryRemove(expected)` | 当前值等于期望值才删除，原子 |

- 键：非空、无空白与控制字符、不超过 512 字符、区分大小写。
- 过期：绝对存活或滑动过期，二选一；都不设时用 Provider 的默认存活时间。
- 基础设施失败抛出 `CACHING_UNAVAILABLE_001`，不伪装成未命中。
- 返回的字节不可修改；实现要么复制，要么保证不被改写。

## 类型化缓存

`ICache`：`GetAsync<T>`、`SetAsync<T>`、`RemoveAsync`、`GetOrCreateAsync<T>`（未命中时各自执行工厂，不合并并发）。进程内实现直接存对象引用，不序列化；跨进程实现用 `SerializingCache` 把存储与 `ICacheSerializer` 组合。默认序列化器是 System.Text.Json，要求选项带 `TypeInfoResolver`，通过 `JsonSerializerOptions.GetTypeInfo` 取类型信息，AOT 下无反射；反射版只在允许反射的运行时可用。

## 分布式锁

租约锁：`TryAcquire(resource, ttl)` 即 `TryAdd("lock:" + resource, token, ttl)`；释放即 `TryRemove(key, token)`。不可重入、不续约；到期后他人可获取，原持有者释放返回 `false`。

## 幂等

Idempotency 阶段的中间件按 Baggage `xihan.idempotency-key` 去重，作用域为（租户、主体、键）：

1. 首次调用用 `TryAdd` 写入"处理中"标记（`InProgressTimeToLive`，默认 60 秒）并执行处理器。
2. 处理器完成后用调用结果（成功，或错误码与消息）替换标记，保留 `TimeToLive`（默认 24 小时）。
3. 处理器抛出异常时比较删标记，允许重试。
4. 重复调用：标记仍为"处理中"返回 `CACHING_CONFLICT_002`；已完成则回放结果、不执行处理器，并在上下文标记为回放。

回放的是调用结果，不是响应体；操作契约（P3）在此之上回放响应。

## 一致性

Provider 必须继承 `CacheStoreConformance` 并通过全部用例（含基于存储的锁）。过期用例用真实时间等待。

## 错误码

见 `conventions/error-codes.md` 中 `CACHING_*`。
