# 升级与迁移

新版本部署上去之后，谁来跑那批 SQL、多个节点怎么保证只有一个跑、跑的时候要不要挡住请求——`XiHan.Framework.Upgrade` 把这套动作编排成一条固定流水线。这章讲怎么把它接起来，以及哪几个环节**框架只给了抽象、必须你自己实现**。

## 它做什么，不做什么

升级模块是**编排骨架 + 扩展点**，不是开箱即用的迁移工具。

| 环节 | 框架提供 | 生产可用性 |
| --- | --- | --- |
| 版本比对与流程编排 | `UpgradeEngine` | 可用 |
| 脚本发现（按版本目录扫 `*.sql`） | `FileSystemUpgradeScriptProvider` | 可用 |
| 后台触发与进程内防重入 | `UpgradeCoordinator` | 可用 |
| 状态查询 | `UpgradeStatusService` | 可用 |
| **版本存储** | `InMemoryUpgradeVersionStore`（进程级静态字典） | **必须换成数据库实现** |
| **分布式锁** | `InMemoryUpgradeLockProvider`（进程内） | **多节点必须换** |
| **迁移执行器** | `DefaultUpgradeMigrationExecutor` | **必须换**，默认直接抛异常 |
| 维护模式 | `DefaultUpgradeMaintenanceModeManager` | 只写日志，不拦请求 |
| 程序文件替换 / 滚动重启 | `NullUpgradeFileUpdater` / `NullRollingRestartCoordinator` | 空实现 |
| 多租户遍历 | `DefaultUpgradeTenantProvider` | 只返回**当前**租户，不遍历租户库 |

::: danger 不实现迁移执行器就等于没启用
`DefaultUpgradeMigrationExecutor.ExecuteAsync` 的唯一行为是抛 `InvalidOperationException`（"未配置 IUpgradeMigrationExecutor 实现…"）。只要有一个待执行脚本，升级就会失败并置为 `Failed`。
:::

它和 `DbInitializer` 的分工要分清：建库建表用 `DbInitializer`（表存在就跳过、**从不补列**，见 [数据访问](./data)），**结构变更只能靠这里的迁移脚本**。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Upgrade
```

```csharp
[DependsOn(typeof(XiHanUpgradeModule))]
public class MyAppModule : XiHanModule { }
```

模块的 `ConfigureServices` 调 `AddXiHanUpgrade(config)`，绑定配置节 `XiHan:Upgrade`，并对全部扩展点做 `TryAdd` 注册——所以你只需要 `Replace` 想换的那几个。

## 组织迁移脚本

默认脚本源扫描 `MigrationsRootPath`（默认 `migrations`；相对路径基于 `AppContext.BaseDirectory`，绝对路径原样使用）：

```text
migrations/
├── 1.0.0/
│   ├── 001_init.sql
│   └── 002_seed.sql
├── 1.1.0/
│   └── 001_add_column.sql
└── 2.0.0/
    └── 001_refactor.sql
```

约定与判定规则：

| 规则 | 说明 |
| --- | --- |
| 一级子目录名 = 版本号 | 必须能被 `SemanticVersion.TryParse` 解析，**解析不了的目录直接跳过，不报错** |
| 只取版本目录下的 `*.sql` | `TopDirectoryOnly`，版本目录内的**再下一级子目录不会被扫描** |
| 排序 | 先按版本语义排，同版本内按文件名 `OrdinalIgnoreCase` 排 |
| 事务粒度 | 一个脚本一次 `IUpgradeMigrationExecutor.ExecuteAsync`，**同版本内多个脚本不是同一个事务** |

::: warning 目录名不能带 `v` 前缀
`SemanticVersion.TryParse` 只认 `主.次.修订` 三段数字，缺省段补 0（`1.2` → `1.2.0`），`-` 后的预发布后缀被截断丢弃。`v1.2.0` 里的 `v1` 解析失败 → 整个目录被静默跳过。
:::

需要从数据库、嵌入资源等别处取脚本时，实现 `IUpgradeScriptProvider` 并用 `AddUpgradeScriptProvider<T>()` 追加——引擎会**合并所有 provider** 的脚本一起排序。

## 触发一次升级

两种入口，语义不同：

| 入口 | 行为 | 用在哪 |
| --- | --- | --- |
| `IUpgradeEngine.ExecuteAsync(ct)` | **同步等完**，返回真实结果 | 运维脚本、可控的启动流程 |
| `IUpgradeCoordinator.StartAsync()` | `Task.Run` 后台跑，**立即返回** `Started=true` / `Status=Upgrading` | 管理端接口（HTTP 不能挂住等迁移） |

```csharp
public class UpgradeAppService(
    IUpgradeCoordinator coordinator,
    IUpgradeStatusService statusService)
{
    // 触发：立即返回，真实结果只写日志
    public Task<UpgradeStartResult> StartAsync() => coordinator.StartAsync();

    // 轮询：前端拿这个查进度
    public Task<UpgradeVersionSnapshot> GetSnapshotAsync(string? clientVersion = null)
        => statusService.GetVersionSnapshotAsync(clientVersion);
}
```

::: tip 协调器的返回值不代表升级成功
`StartAsync` 只告诉你「任务起没起来」。引擎的执行结果（完成 / 失败 / 无需升级）只落到日志，**调用方必须靠 `IUpgradeStatusService` 轮询**。上一次任务未完成时再调，返回 `Started=false` / `Message="升级任务正在执行"`，不会并发起第二个。这是**进程内**防重入，跨节点靠分布式锁。
:::

## 关键机制

### 三个版本，两个判定

| 名字 | 来源 |
| --- | --- |
| 当前应用版本 | `AppVersion` 选项；为空取入口程序集版本 `ToString(3)`，再取不到用 `0.0.0` |
| 记录的应用版本 / 数据库版本 | `IUpgradeVersionStore.GetOrCreateAsync` 返回的 `UpgradeVersionState` |
| 最新脚本版本 | 所有 provider 脚本里的最大版本；无脚本时为 `0.0.0` |

```text
needDbUpgrade  = 记录DbVersion  < 最新脚本版本
needAppUpgrade = 记录AppVersion < 当前应用版本
需要升级 = 二者任一为真
```

两者都为假就直接返回 `Started=false` / `Status=Normal` / `"无需升级"`，连锁都不抢。只有应用版本变、没有新脚本时，流程照走一遍，只是不执行迁移、单纯把记录的 `AppVersion` 推上去。

`MinSupportVersion` **不参与**上面的判定。它只有两个用处：写进版本记录；以及 `GetVersionSnapshotAsync(clientVersion)` 里拿 `clientVersion` 跟它比，得出 `ForceUpgrade` / `IsCompatible`——那是给**客户端**（前端、App）做版本门禁的，和服务端自身升不升级无关。

### 分布式锁与多节点协调

节点进入升级前要过两道门：

1. **主节点门控**：配了 `PrimaryNodeName` 且当前节点名不匹配 → 直接返回 `Normal` / `"当前节点非主节点，等待升级"`。不配则每个节点都视为主节点。
2. **抢锁**：`TryAcquireLockAsync(resourceKey, LockExpirySeconds, nodeName)`。资源键为 `LockResourceKey`；版本记录的 `TenantId` 非空（即当前处于某个租户上下文）时追加 `:Tenant_{租户Id}` 后缀，与 `EnableMultiTenantIsolation` 开没开无关。

节点名的解析顺序是 `NodeName` 选项 → `机器名-实例Id`。实例 Id 每次进程启动都会变，所以**要用 `PrimaryNodeName` 就必须同时显式配 `NodeName`**，否则永远匹配不上，谁都不升级。

::: warning 「其余节点等待」是不执行，不是阻塞等
抢锁是**单次尝试、不阻塞、不重试**。抢不到的节点立刻返回 `Started=false` / `Status=Upgrading` / `"升级锁已被占用"` 就结束了——它不会等迁移完再继续，也没有任何就绪门禁拦着它对着尚未迁移的库提供服务。要「等」得自己在启动流程里轮询 `IUpgradeStatusService.GetUpgradeStatusAsync()`。
:::

锁令牌是 `IAsyncDisposable`：引擎用 `await using` 兜底，正常路径和异常路径都会显式 `ReleaseAsync()`（幂等）。`LockExpirySeconds`（默认 600）是防止持有者崩溃后死锁的兜底过期，**迁移比它慢就会被别的节点抢走**——大批量脚本要调大。

### 幂等靠迁移历史，不靠版本号

引擎先按 `脚本版本 > 当前DbVersion` 粗筛，再逐条问 `HasMigrationHistoryAsync(version, scriptName)`，命中就跳过。执行完成或失败**都会**写一条 `UpgradeMigrationHistory`（失败的带 `ErrorMessage`），随后按版本分组推进 `UpdateDbVersionAsync`。

::: danger 已发布版本目录里的脚本内容不能改
去重键是 `(版本, 脚本名)`，**不看内容**。改了内容不改文件名，只会被判定为已执行而跳过。要补改动就加新脚本、新版本目录。
:::

内置内存实现只把 `Success = true` 的记录算作「已执行」，所以失败的脚本重跑时会**重来一遍**——自定义存储实现应保持同一语义，同时脚本本身要写成可重入的。

一个脚本抛异常即整体中止：置 `Failed` 状态、退出维护模式、释放锁。已成功的脚本靠历史去重不会重跑，`DbVersion` 停在最后一个**完整完成**的版本组上。

### 实现 `IUpgradeVersionStore` 的隐性契约

引擎在迁移结束后是这样回写完成状态的：

```csharp
await _versionStore.SetUpgradeCompletedAsync(version, currentAppVersion, version.DbVersion, cancellationToken);
```

第三个参数取自**传进去的那个 `UpgradeVersionState` 实例**。

::: danger 存储实现必须把新值写回入参实例
如果 `UpdateDbVersionAsync` 只更新了数据库、没有同步修改传入的 `version` 对象，那么 `version.DbVersion` 仍是本轮开始时的旧值，完成时会被原样写回去——数据库版本永远推不上去，每次启动都判定「需要升级」。内置实现通过把状态复制回入参来满足这一点，自定义实现照做。

同理，`GetOrCreateAsync` 必须**按当前租户上下文分区**（内置实现用 `tenant:{id}` / `host` 作键），否则多租户下版本记录会串。
:::

### 维护模式

`EnableMaintenanceMode`（默认 `true`）决定是否在迁移前后调 `EnterAsync` / `ExitAsync`。默认实现只打一行日志，**没有任何请求被拦下**——真要挡流量得自己实现管理器加中间件：

```csharp
public sealed class MaintenanceFlag
{
    private int _value;
    public bool IsOn => Volatile.Read(ref _value) == 1;
    public void Set(bool on) => Volatile.Write(ref _value, on ? 1 : 0);
}

public class FlagMaintenanceModeManager(MaintenanceFlag flag) : IUpgradeMaintenanceModeManager
{
    public Task EnterAsync(CancellationToken cancellationToken = default)
    {
        flag.Set(true);
        return Task.CompletedTask;
    }

    public Task ExitAsync(CancellationToken cancellationToken = default)
    {
        flag.Set(false);
        return Task.CompletedTask;
    }
}
```

::: warning 进程内标志只挡得住执行升级的那个节点
`EnterAsync` 只在抢到锁的节点上被调用。要全集群进维护模式，标志必须放共享存储（缓存 / 数据库），并让每个节点自己读。
:::

退出时机固定在「迁移完成、版本回写、可选文件替换之后，释放锁之前」；异常路径也会退出维护模式。`EnableFileUpdate`（默认 `false`）和 `EnableRollingRestart`（默认 `false`）对应的两个默认实现都是空的，开了也不会发生任何事——需要就自己实现 `IUpgradeFileUpdater` / `IRollingRestartCoordinator`。

### 启动自动检查只是建记录

`EnableAutoCheckOnStartup`（默认 `true`）门控的是 `XiHanUpgradeModule.OnPostApplicationInitializationAsync`：它建一个 Scope，解析 `IUpgradeVersionStore` 与 `IUpgradeStatusService`（任一缺失即返回），调 `EnsureInitializedAsync()`。

::: warning 启动阶段不会执行任何迁移
`EnsureInitializedAsync` 只做一件事：`GetOrCreateAsync` 把版本记录建出来。**真正的升级必须显式调 `IUpgradeEngine.ExecuteAsync` 或 `IUpgradeCoordinator.StartAsync`。**
:::

### 多租户隔离升级

`EnableMultiTenantIsolation`（默认 `false`）为真时，引擎遍历 `IUpgradeTenantProvider.GetTenants()`，逐个 `ICurrentTenant.Change(TenantId, Name)` 后跑完整流程，任一租户 `Failed` 立即中止并返回该结果。

两个要注意的地方：

- 默认 `DefaultUpgradeTenantProvider` 返回的是**一条**记录——当前 `ICurrentTenant` 的 Id 与名称（宿主态即 `(null, null)`）。要「逐全体租户」批量升级，必须自己实现 `IUpgradeTenantProvider` 从租户仓储读全量列表。
- 全部租户跑完后返回的固定是 `Started=true` / `Completed` / `"多租户升级完成"`，**即使每个租户实际都是「无需升级」或「锁被占用」**。要判断实情看日志与各租户状态。

## 配置

配置节 `XiHan:Upgrade`（`XiHanUpgradeOptions.SectionName`）。按决策看，只有这几组需要你想清楚：

| 决策 | 相关键 | 说明 |
| --- | --- | --- |
| 版本从哪来 | `AppVersion` | 留空则取入口程序集版本；CI 里想精确控制就显式配 |
| 脚本放哪 | `MigrationsRootPath` | 默认 `migrations`，相对于 `AppContext.BaseDirectory`。记得让 `.sql` 随发布输出 |
| 谁来升 | `NodeName` + `PrimaryNodeName` | 要指定主节点就**两个一起配**；不配 `PrimaryNodeName` 则全员候选、靠锁串行 |
| 锁多久过期 | `LockResourceKey`、`LockExpirySeconds` | 默认 `SystemUpgrade` / 600 秒；迁移可能更久就调大 |
| 升级时的运维动作 | `EnableMaintenanceMode`、`EnableFileUpdate`、`EnableRollingRestart` | 后两个默认关；开之前先确认对应实现已替换 |
| 多租户 | `EnableMultiTenantIsolation` | 开之前先换掉 `IUpgradeTenantProvider` |

```json
{
  "XiHan": {
    "Upgrade": {
      "MigrationsRootPath": "migrations",
      "NodeName": "node-01",
      "PrimaryNodeName": "node-01",
      "LockExpirySeconds": 1800,
      "EnableAutoCheckOnStartup": true,
      "EnableMaintenanceMode": true
    }
  }
}
```

::: tip `ConnectionConfigId` 是留给你读的
这个键在升级包内部**没有任何消费方**。它的用途是让你在自己的 `IUpgradeMigrationExecutor` / `IUpgradeVersionStore` 实现里读出来，决定连哪个库。
:::

完整配置项与 API 清单见 [Upgrade 包](../packages/upgrade)。

## 接上生产必需的三件

### 迁移执行器

默认注册是 **Singleton**，而实现通常要依赖 Scoped 的数据访问服务——用 Scoped 描述符替换即可（`IUpgradeEngine` 本身是 Scoped，能消费）：

```csharp
public class SqlSugarUpgradeMigrationExecutor(
    ISqlSugarClientResolver resolver,
    IOptions<XiHanUpgradeOptions> options) : IUpgradeMigrationExecutor
{
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        var configId = options.Value.ConnectionConfigId;
        var client = string.IsNullOrWhiteSpace(configId)
            ? resolver.GetCurrentClient()
            : resolver.GetClient(configId);

        await client.Ado.BeginTranAsync();
        try
        {
            await client.Ado.ExecuteCommandAsync(sql);
            await client.Ado.CommitTranAsync();
        }
        catch
        {
            await client.Ado.RollbackTranAsync();
            throw;
        }
    }
}
```

```csharp
services.Replace(ServiceDescriptor.Scoped<IUpgradeMigrationExecutor, SqlSugarUpgradeMigrationExecutor>());
```

### 分布式锁

`IUpgradeLockProvider` 和缓存包的 `IDistributedLock` 形状几乎一致，写个适配器即可（升级包不依赖缓存包，适配器放应用层）：

```csharp
public class DistributedUpgradeLockProvider(IDistributedLock distributedLock) : IUpgradeLockProvider
{
    public async Task<IUpgradeLockToken?> TryAcquireLockAsync(
        string resourceKey, TimeSpan expiry, string nodeName, CancellationToken cancellationToken = default)
    {
        var handle = await distributedLock.TryAcquireAsync(resourceKey, expiry, cancellationToken);
        return handle is null ? null : new Token(handle);
    }

    private sealed class Token(IDistributedLockHandle handle) : IUpgradeLockToken
    {
        public string ResourceKey => handle.ResourceKey;
        public string LockId => handle.LockId;
        public bool IsReleased => handle.IsReleased;
        public Task ReleaseAsync() => handle.ReleaseAsync();
        public ValueTask DisposeAsync() => handle.DisposeAsync();
    }
}
```

```csharp
services.Replace(ServiceDescriptor.Singleton<IUpgradeLockProvider, DistributedUpgradeLockProvider>());
```

::: warning 没接 Redis 时 `IDistributedLock` 本身也会退化成进程内锁
换了适配器不等于就有分布式锁了，底座得真接上 Redis。见 [缓存与分布式锁](./caching)。
:::

### 版本存储

实现 `IUpgradeVersionStore` 的 9 个方法，把版本状态和迁移历史落库，并遵守上面那条「写回入参实例 + 按租户分区」的契约：

```csharp
services.Replace(ServiceDescriptor.Scoped<IUpgradeVersionStore, SqlSugarUpgradeVersionStore>());
```

::: danger 默认存储进程重启即丢
`InMemoryUpgradeVersionStore` 虽然注册为 Scoped，内部却是 `static` 字典（按 `tenant:{id}` / `host` 分区）——同进程内跨请求可见，但进程一停全部归零，下次启动会把所有脚本当成没跑过（此时只有脚本自身的可重入性兜底）。
:::

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 升级失败，日志写「未配置 IUpgradeMigrationExecutor 实现」 | 没替换迁移执行器 |
| 启动没报错，但库结构没变 | 启动自动检查只建版本记录，得显式触发升级 |
| 脚本目录明明有文件却一条都不执行 | 版本目录名解析不了（带 `v` 前缀、非数字段）而被静默跳过；或脚本没随发布输出到 `AppContext.BaseDirectory` |
| 每次启动都判定「需要升级」 | 存储实现的 `UpdateDbVersionAsync` 没把新版本写回入参 `UpgradeVersionState` |
| 改了已发布脚本的内容，重新部署没生效 | 去重键是 `(版本, 脚本名)`，不看内容。加新脚本 |
| 配了 `PrimaryNodeName` 后谁都不升级 | 没同时配 `NodeName`，默认节点名里的实例 Id 每次启动都变 |
| 多节点同时启动，几个节点都在跑迁移 | 锁还是默认的进程内实现 |
| 迁移跑到一半被另一个节点抢了锁 | 迁移耗时超过 `LockExpirySeconds` |
| 维护模式开了但请求照进 | 默认管理器只写日志；且进程内标志只对执行升级的那个节点生效 |
| 状态一直是 `Failed` | 最新一条迁移历史是失败记录，需要新的成功记录才会翻转 |
| 多租户模式返回「多租户升级完成」但什么都没做 | 该返回值是固定的，逐租户实情看日志；默认租户提供者也只返回当前一个租户 |

## 下一步

- [数据访问](./data)：`DbInitializer` 建表边界，为什么结构变更要走脚本
- [多租户](./multi-tenancy)：`ICurrentTenant.Change` 与租户上下文
- [缓存与分布式锁](./caching)：`IDistributedLock` 的行为与退化条件
- [模块生命周期](./lifecycle)：`OnPostApplicationInitializationAsync` 的执行时机
- [Upgrade 包](../packages/upgrade)：完整 API 与配置表
