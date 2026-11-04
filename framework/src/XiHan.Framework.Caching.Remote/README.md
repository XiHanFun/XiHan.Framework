# XiHan.Framework.Caching.Remote

## 关于

`XiHan.Framework.Caching.Remote` 让缓存存储由任意语言写的进程外插件承担（实验性）：插件实现固定的操作集，本包把它接成 `ICacheStore`、`ISharedCache`、`IDistributedLock` 与幂等中间件，和 Redis 提供程序一样参与装配。

## 主要特性

* 存储在插件里，语言不限（Go 有现成 SDK 与示例）
* 每个调用带超时，不可达映射为统一的不可用错误
* 通过同一套缓存一致性用例

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Caching.Remote --prerelease
```

### 使用

本包只提供能力，不启动插件进程；插件由 `Plugins.OutOfProcess` 按目录发现：

```csharp
builder.AddXiHan(x => x
    .UseStandardProfile()
    .UseFeature<OutOfProcessPluginsFeature>()
    .UseFeature<RemoteCachingFeature>());
```

```json
{
  "XiHan": {
    "Plugins": { "OutOfProcess": { "Directories": ["plugins/cache-go"] } },
    "Caching": { "Remote": { "TimeoutMilliseconds": 5000 } }
  }
}
```

## 主要类型

此库提供的主要类型：

* `RemoteCachingFeature`：启用入口
* `RemoteCachingOptions`：每次调用的超时
* `RemoteCacheStore`：经操作集实现的 `ICacheStore`
* `RemoteCacheOperations`：操作 ID 与请求、响应类型

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/caching-remote)

## 相关包

* 缓存抽象：[XiHan.Framework.Caching.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Caching.Abstractions)
* 进程外插件：[XiHan.Framework.Plugins.OutOfProcess](https://www.nuget.org/packages/XiHan.Framework.Plugins.OutOfProcess)
* Redis 实现：[XiHan.Framework.Caching.Redis](https://www.nuget.org/packages/XiHan.Framework.Caching.Redis)

## 反馈与贡献

`XiHan.Framework.Caching.Remote` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
