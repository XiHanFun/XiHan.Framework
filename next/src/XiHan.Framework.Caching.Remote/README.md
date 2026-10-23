# XiHan.Framework.Caching.Remote

`caching.distributed@1` 的远程 Provider 适配器（Ring 3，RFC-0019，**Experimental**）：存储由任意语言写的进程外插件承担，插件只需实现固定的操作集 `cache-entries.get / set / try-add / remove / try-remove / get-many / set-many / remove-many`（`spec/provider/caching-distributed.md`），本包把它接成 `ISharedCache`、`IDistributedLock` 与幂等中间件，和 `Caching.Redis` 一样参与能力求解。Go 实现见 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 的 `caching` 包与 `examples/cache`，示例插件见 `samples/CacheGoPlugin`。

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<OutOfProcessPluginsFeature>().UseFeature<RemoteCachingFeature>());
```

```json
{
  "XiHan": {
    "Plugins": { "OutOfProcess": { "Directories": ["plugins/cache-go"] } },
    "Caching": { "Remote": { "TimeoutMilliseconds": 5000 } }
  }
}
```

## 责任边界

- `RemoteCacheStore` 每个操作按 ID 从宿主操作登记表取代理操作并直接执行（`IOperationInvoker.ExecuteAsync`），不经过管道：存储会被幂等中间件在管道里使用，走管道会递归；也不需要授权与幂等。每次调用带 `Timeout` 的截止时间。
- 键与过期选项在 .NET 侧先按 `CacheKey` / `CacheEntryOptions` 校验，再编码成操作集的 JSON（值为 base64、过期为毫秒）。
- 插件返回的缓存域错误（`CACHING_VALIDATION_00x`）原样成为 `FrameworkException`；插件未加载、操作未登记、进程不可达、超时都映射为 `CACHING_UNAVAILABLE_001`，细节带原错误码，不伪装成未命中。
- 本包只提供能力，不启动插件进程；插件由 `Plugins.OutOfProcess` 按 `XiHan:Plugins:OutOfProcess:Directories` 发现。插件侧错误的消息键 `caching.remote.*` 的译文随本包嵌入，宿主不依赖插件自带的目录。
- 适配器 + 插件的组合必须通过 `Caching.Conformance` 全部用例；`samples/CacheGoPlugin` 在 `Caching.Remote.Tests` 里跑完整套件。

## 依赖方向

`Caching.Abstractions` → 本包；本包不引用任何插件宿主包，只依赖宿主已登记的操作。

## 状态

Experimental：操作集覆盖 RFC-0006 的五个原子操作与三个批量操作；其他子系统的远程 Provider 操作集随各自的示例补充。
