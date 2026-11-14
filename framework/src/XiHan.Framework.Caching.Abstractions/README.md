# XiHan.Framework.Caching.Abstractions

## 关于

`XiHan.Framework.Caching.Abstractions` 定义缓存的契约：字节存储 `ICacheStore`、类型化缓存 `ICache` / `ISharedCache`、序列化器、分布式锁，以及按幂等键去重的 Idempotency 中间件。不包含存储实现。

## 主要特性

* 进程内缓存与跨进程共享缓存两套门面，支持批量读写与 `GetOrCreateAsync`
* 绝对或滑动过期
* 基于存储的分布式租约锁
* `IDistributedCache` 适配与操作调用的幂等去重
* `GenerationCache<T>`：按范围失效、丢弃失效之前开始的读取的读穿缓存区，装了两级缓存时失效作用于所有实例
* 组件经 `CacheSerializerRegistration.AddTypeInfo` 登记自己放进缓存的类型，应用的序列化上下文不必认识它们

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Caching.Abstractions --prerelease
```

### 使用

通常由实现包带入；写业务代码或自定义缓存提供程序时才直接引用。实现包有 `Caching.InMemory`、`Caching.Redis`、`Caching.Remote`、`Caching.Hybrid`。业务代码里这样用：

```csharp
var cache = context.Services.GetRequiredService<ISharedCache>();
var profile = await cache.GetOrCreateAsync(
    new CacheKey($"profile:{id}"),
    ct => LoadAsync(id, ct),
    CacheEntryOptions.Expires(TimeSpan.FromMinutes(5)),
    ct);
```

存储不可达时抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`，不会伪装成未命中。

数据会被写入改变、写入后要让一批条目一起失效时，用 `GenerationCache<T>`：

```csharp
var prices = new GenerationCache<PriceList>(cache, "acme:prices", TimeSpan.FromMinutes(5), logger);
// 范围是租户、条目是类别；未命中的位置一次交给 loadMany 查源，查源失败不写缓存
var read = await prices.GetOrLoadManyAsync([new GenerationCacheKey(tenantId, "books")], loadMany, ct);
// 租户调价：这个租户下的条目一起作废，失效之前开始的读取写回的旧值不会被当作命中
await prices.InvalidateAsync([tenantId], ct);
```

缓存后端故障时读取直接查源、不写回；失效把异常抛给调用方，由它决定拒绝写入还是记日志。

## 主要类型

此库提供的主要类型：

* `ICache`、`ISharedCache`：类型化缓存门面
* `ICacheStore`：字节级存储，提供程序实现它
* `CacheKey`、`CacheEntryOptions`：键与过期选项
* `IDistributedLock`、`LockHandle`：分布式锁
* `ICacheSerializer`、`JsonCacheSerializer`、`CacheSerializerRegistration`、`CacheTypeInfoSource`：共享缓存的值序列化与组件类型的登记
* `GenerationCache<T>`、`GenerationCacheKey`、`GenerationCacheEntry<T>`：按范围失效的缓存区
* `IdempotencyMiddleware`：按幂等键去重

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/caching-abstractions)


## 相关包

* 内存实现：[XiHan.Framework.Caching.InMemory](https://www.nuget.org/packages/XiHan.Framework.Caching.InMemory)
* Redis 实现：[XiHan.Framework.Caching.Redis](https://www.nuget.org/packages/XiHan.Framework.Caching.Redis)
* 两级缓存：[XiHan.Framework.Caching.Hybrid](https://www.nuget.org/packages/XiHan.Framework.Caching.Hybrid)
* 一致性套件：[XiHan.Framework.Caching.Conformance](https://www.nuget.org/packages/XiHan.Framework.Caching.Conformance)

## 反馈与贡献

`XiHan.Framework.Caching.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
