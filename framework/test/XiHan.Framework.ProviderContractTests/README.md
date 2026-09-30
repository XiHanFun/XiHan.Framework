# XiHan.Framework.ProviderContractTests

提供方（Provider）契约测试基底：只经公开契约观察行为，供仓库内各提供方的测试项目继承复用。本项目不打包、不发布，也不被任何 `framework/src` 项目引用。

## 契约

| 基类 | 被测契约 | 覆盖行为 |
| --- | --- | --- |
| `OutboxContract` | `IEventOutbox` | 入箱与按创建时间取回、上限、删除、取消、跨客户端独占领取与领取过期、并发领取不重复 |
| `InboxContract` | `IEventInbox` | 入箱与判重查询、已处理/已丢弃/延后重试的取回规则、清理不删待处理、取消、去重、完结不回退、跨客户端领取、并发领取 |
| `BackgroundJobStoreContract` | `IBackgroundJobStore` | 插入与查回、到期/放弃/应用名过滤、排序、上限、删除、失败回写后按时间重取、跨客户端领取与租约过期、并发领取 |

## 能力

提供方经 `IProviderContractFixture<T>.Capabilities` 声明能力，未声明的能力对应的用例以「提供方未声明 … 能力」跳过：

| 能力 | 含义 |
| --- | --- |
| `Persistence` | 一个客户端写入的数据可被夹具新建的其他客户端读到 |
| `ExclusiveClaim` | 一个客户端领取的记录在领取有效期内不会被其他客户端领到 |
| `ClaimExpiry` | 夹具可让领取立即过期（`ExpireClaimsAsync`） |
| `ControllableTime` | 夹具可推进提供方看到的时间（`AdvanceTime`） |
| `Deduplication` | 收件箱按消息标识去重入箱 |
| `FinalStateProtection` | 收件箱已完结的记录不会被延后重试回退 |
| `ConcurrentStorage` | 底层是支持真实并发写入的数据库，执行多工作者并发领取 |

## 接入新的提供方

1. 测试项目引用本项目。
2. 实现 `IProviderContractFixture<T>`：`CreateClientAsync` 每次返回一个连到同一份存储的新客户端；`DisposeAsync` 清理本夹具使用的存储。进程内实现可直接用 `InProcessContractFixture<T>`。
3. 新建测试类继承对应契约基类并实现 `CreateFixtureAsync`。依赖外部数据库的夹具在 `CreateFixtureAsync` 内用 `Assert.SkipWhen` 按环境变量跳过。
