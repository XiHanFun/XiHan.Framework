# XiHan.Framework.Caching.InMemory

## 关于

`XiHan.Framework.Caching.InMemory` 是缓存的默认实现：没有显式提供程序时，进程内缓存与「共享」缓存都由它承担。只在单实例部署下正确，适合开发、测试与单进程应用。

## 主要特性

* 有界 LRU，满时淘汰最久未访问的条目
* 绝对或滑动过期，都不设时用缺省存活时间
* 同时提供分布式锁与幂等中间件
* 健康项在条目数达到上限时报告降级

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Caching.InMemory --prerelease
```

### 使用

启用特性（`Profiles.Full` 已包含）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<InMemoryCachingFeature>());
```

```json
{ "XiHan": { "Caching": { "InMemory": { "MaxEntries": 10000, "DefaultTimeToLiveSeconds": 300 } } } }
```

类型化缓存直接存对象引用、不序列化，调用方不得修改取回的对象。多实例部署换 `Caching.Redis`。

## 主要类型

此库提供的主要类型：

* `InMemoryCachingFeature`：启用入口
* `InMemoryCacheStore`：字节存储
* `InMemoryObjectCache`：类型化缓存

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/caching-inmemory)

## 相关包

* 缓存抽象：[XiHan.Framework.Caching.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Caching.Abstractions)
* Redis 实现：[XiHan.Framework.Caching.Redis](https://www.nuget.org/packages/XiHan.Framework.Caching.Redis)

## 反馈与贡献

`XiHan.Framework.Caching.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
