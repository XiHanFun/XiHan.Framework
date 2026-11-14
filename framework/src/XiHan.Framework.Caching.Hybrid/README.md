# XiHan.Framework.Caching.Hybrid

## 关于

`XiHan.Framework.Caching.Hybrid` 提供两级缓存：`ICache` 变成「L1 进程内对象缓存 + L2 共享缓存」，读路径少一次网络往返，适合多实例部署下读多写少的数据。`ISharedCache` 仍直接指向 L2。

## 主要特性

* L1 命中直接返回，未命中读 L2 并回填；读 L2 的途中这个键失效过就不回填，旧值不会留在 L1
* 写与删先 L2 再 L1
* L2 带变更通知（Redis）时，其他实例的写入让本实例 L1 立即失效
* L1 有界 LRU，条目存活不超过 `LocalTimeToLive`
* 框架的授予缓存、租户目录缓存与翻译版本戳都走 `ICache`，多实例部署装上本包与 Redis 后，写入立即在所有实例上生效

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Caching.Hybrid --prerelease
```

### 使用

需要一个共享缓存提供程序作 L2（下例用 Redis；单实例开发时 L2 可以是内存实现）：

```csharp
builder.AddXiHan(x => x
    .UseFeature<RedisCachingFeature>()
    .UseFeature<HybridCachingFeature>());
```

```json
{ "XiHan": { "Caching": { "Hybrid": { "LocalCapacity": 10000, "LocalTimeToLiveSeconds": 30 } } } }
```

L1 存对象引用，调用方不得修改取回的对象；需要更强一致的键直接用 `ISharedCache`。

## 主要类型

此库提供的主要类型：

* `HybridCachingFeature`：启用入口
* `HybridCachingOptions`：`LocalCapacity`、`LocalTimeToLive`
* `HybridObjectCache`：两级 `ICache` 实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/caching-hybrid)

## 相关包

* 缓存抽象：[XiHan.Framework.Caching.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Caching.Abstractions)
* Redis 实现：[XiHan.Framework.Caching.Redis](https://www.nuget.org/packages/XiHan.Framework.Caching.Redis)
* 内存实现：[XiHan.Framework.Caching.InMemory](https://www.nuget.org/packages/XiHan.Framework.Caching.InMemory)

## 反馈与贡献

`XiHan.Framework.Caching.Hybrid` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
