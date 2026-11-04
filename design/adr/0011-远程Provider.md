# ADR-0011 远程 Provider：宿主侧适配器 + 插件操作集

## 状态

已采纳

## 日期

2026-10-23

## 背景

ADR-0006 把进程外插件协议定为"跨语言插件与远程 Provider 的基础"，P4-07 要用 Go 实现 `caching.distributed` 并通过 `Caching.Conformance`。协议只搬运 XGI 载荷，没有说明插件怎样"成为 Provider"：能力求解、子系统抽象（`ICacheStore`、`ISharedCache`、`IDistributedLock`、幂等中间件）都在 .NET 侧，插件进程里只有操作。

## 备选

1. 插件清单声明 `provides`，宿主按能力自动把插件接成 Provider。
2. 每个子系统一个宿主侧适配器包 `<领域>.Remote` 声明能力并实现抽象，插件只实现一组规范规定的固定操作；适配器直接执行代理操作，不走管道。
3. 插件经回调 `Host.Invoke` 反向注册自己为 Provider。

## 决策

采用备选 2（RFC-0019）。`XiHan.Framework.Caching.Remote`（Ring 3，机制名 Remote）提供 `caching.distributed@1`，`RemoteCacheStore` 按操作集 `cache-entries.get / set / try-add / remove / try-remove` 调用宿主登记表里的代理操作（`IOperationInvoker.ExecuteAsync`），插件由 `Plugins.OutOfProcess` 发现；`framework-sdk-go` 的 `caching` 包把任何 `Store` 暴露成该操作集，`examples/cache` 与 `samples/CacheGoPlugin` 是示例。

## 理由

- 每个能力的抽象形状不同，适配器必须按子系统手写；备选 1 没有减少工作量，反而把子系统知识塞进插件宿主。
- 操作集是语言中立的规范性内容，任何语言实现一次就能替换 `Caching.Redis` 这类 .NET Provider，并用同一套一致性套件验证。
- 缓存存储会被幂等中间件在管道里使用，适配器走管道会递归；这些内部调用也不需要授权与幂等，直接执行代理操作最简单也最安全。
- 备选 3 让插件在握手之外多一次反向流程，宿主的能力求解在启动阶段已完成，运行期注册会打乱装配顺序。

## 影响

- 规范新增 `spec/provider/`（通用规则与 `caching-distributed.md`）与向量 `provider/caching-distributed.json`。
- 插件侧的错误消息键 `caching.remote.*` 由适配器包嵌入 10 种区域的译文，宿主不依赖插件自带的目录。
- 其他子系统的远程 Provider（对象存储、消息、调度）按同一模式各自定义操作集，随示例补充。
