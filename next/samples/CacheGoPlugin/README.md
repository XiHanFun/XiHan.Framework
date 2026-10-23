# CacheGoPlugin

用 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 写的 `caching.distributed@1` 远程 Provider 插件（RFC-0019，P4-07）：实现操作集 `cache-entries.get / set / try-add / remove / try-remove`，由 `Plugins.OutOfProcess` 加载后，`Caching.Remote` 适配器把它接成 .NET 侧的共享缓存、分布式锁与幂等去重。实现来自 SDK 的 `examples/cache`（有界内存存储，清单 ID `xihan.samples.cache-go`），本目录只有可执行入口 `main.go`、同一份 `xihan.manifest.json` 与 Go 模块文件。

| 配置节 `XiHan:Cache` | 含义 |
| --- | --- |
| `DefaultTimeToLiveSeconds` | 没有指定过期时的存活时间，默认 300 |
| `MaxEntries` | 条目数上限，默认 100000；满了先清过期条目，仍满则 `CACHING_UNAVAILABLE_001` |

## 构建

```powershell
./build.ps1          # 产物在 bin/：cache-go(.exe) + xihan.manifest.json
```

需要 Go 1.26+。`go.mod` 以伪版本锁定 SDK 的提交；升级 SDK 用 `go get github.com/xihanfun/framework-sdk-go@<提交或标签>`。

## 测试

`XiHan.Framework.Caching.Remote.Tests` 的 `RemoteCacheStoreConformanceTests` 让"适配器 + 本插件"跑完整套 `Caching.Conformance`，`RemoteCachingTests` 验证它胜过内存默认实现并服务共享缓存与分布式锁；测试运行时用本机的 `go` 构建本目录，没有 `go` 时跳过。协议层可以直接用规范仓库的运行器：

```bash
xihan-conformance plugin --dir bin --sample 'cache-entries.set={"key":"k","value":"aGk="}'
```
