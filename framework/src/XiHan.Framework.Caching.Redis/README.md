# XiHan.Framework.Caching.Redis

## 关于

`XiHan.Framework.Caching.Redis` 是缓存的 Redis 提供程序，基于 StackExchange.Redis。装配后顶替内存实现的共享缓存（`ISharedCache`、`ICacheStore`、`IDistributedLock` 与幂等中间件），适合多实例部署。

## 主要特性

* 跨实例共享的缓存与分布式锁
* 绝对与滑动过期，键前缀隔离
* 支持按前缀列键与删除，并向其他实例广播变更
* 健康项定期 PING，Redis 不可达时降级而不中断应用

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Caching.Redis --prerelease
```

### 使用

启用特性并配置连接（`ICache` 仍由 `Caching.InMemory` 提供，可再叠加 `Caching.Hybrid`）：

```csharp
builder.AddXiHan(x => x
    .UseFeature<InMemoryCachingFeature>()
    .UseFeature<RedisCachingFeature>());
```

```json
{ "XiHan": { "Caching": { "Redis": { "Configuration": "localhost:6379", "KeyPrefix": "xihan:", "DefaultTimeToLiveSeconds": 300 } } } }
```

连接也可来自资源绑定 `cache`；两者都没有时装配失败。

## 主要类型

此库提供的主要类型：

* `RedisCachingFeature`：启用入口
* `RedisCachingOptions`：连接、`KeyPrefix`、缺省存活时间等选项
* `RedisCacheStore`：`ICacheStore` 的 Redis 实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/caching-redis)

## 相关包

* 缓存抽象：[XiHan.Framework.Caching.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Caching.Abstractions)
* 内存实现：[XiHan.Framework.Caching.InMemory](https://www.nuget.org/packages/XiHan.Framework.Caching.InMemory)
* 两级缓存：[XiHan.Framework.Caching.Hybrid](https://www.nuget.org/packages/XiHan.Framework.Caching.Hybrid)

## 反馈与贡献

`XiHan.Framework.Caching.Redis` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
