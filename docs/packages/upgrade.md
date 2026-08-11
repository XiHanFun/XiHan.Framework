# XiHan.Framework.Upgrade

> 分布式安全升级引擎：检测版本 → 抢分布式锁 → 进维护模式 → 按序执行迁移脚本 → 回写版本 →（可选）替换文件 / 滚动重启，全流程编排 + 逐环节可插拔。

- **NuGet**：`XiHan.Framework.Upgrade`
- **模块类**：`XiHanUpgradeModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（Core / MultiTenancy.Abstractions）。版本解析用自研 `SemanticVersion`。

## 概述

XiHan.Framework.Upgrade 把分布式部署下的「安全升级」抽象成一套统一编排：应用启动或运维触发时，检测当前应用 / 数据库版本是否落后，落后则抢占分布式锁（保证集群内单节点执行），进入维护模式，按语义化版本顺序**幂等**执行 SQL 迁移脚本，回写版本记录，最后可选地替换程序文件、滚动重启。

它提供**编排骨架 + 抽象 + 一批默认实现**，但默认实现大多是 InMemory / Null 占位——**版本存储、分布式锁、迁移执行三项生产环境必须替换为持久化 / 分布式实现**（尤其迁移执行器默认直接抛异常，必须由应用层提供）。

## 何时使用

- 应用启动或运维时，需判断当前应用 / 数据库版本是否落后并自动升级。
- 多节点部署下，需用分布式锁保证同一时刻只有一个节点执行升级。
- 按语义化版本发现、排序并**幂等**执行 SQL 迁移脚本，避免重复执行。
- 升级期间需要维护模式、程序文件替换、滚动重启等运维动作的统一编排。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Upgrade
```

```csharp
[DependsOn(typeof(XiHanUpgradeModule))]
public class MyModule : XiHanModule { }
```

`XiHanUpgradeModule.ConfigureServices` 调用 `AddXiHanUpgrade(config)`，从配置节 `XiHan:Upgrade`（`XiHanUpgradeOptions.SectionName`）绑定选项，并为各扩展点注册默认实现（`TryAdd`，可被业务侧替换）：

| 接口 | 默认实现 | 生命周期 | 生产可用性 |
| --- | --- | --- | --- |
| `IUpgradeScriptProvider` | `FileSystemUpgradeScriptProvider` | Singleton | 可用（从文件系统扫描 SQL） |
| `IUpgradeVersionStore` | `InMemoryUpgradeVersionStore` | Scoped | **需替换为数据库实现** |
| `IUpgradeLockProvider` | `InMemoryUpgradeLockProvider` | Singleton | **需替换为分布式锁（进程内锁多节点无效）** |
| `IUpgradeMigrationExecutor` | `DefaultUpgradeMigrationExecutor` | Singleton | **必须替换**（默认直接抛异常，见下） |
| `IUpgradeTenantProvider` | `DefaultUpgradeTenantProvider` | Scoped | 视多租户需求替换（默认只返回当前 `ICurrentTenant`，不会遍历租户库） |
| `IUpgradeStatusService` | `UpgradeStatusService` | Scoped | 可用 |
| `IUpgradeEngine` | `UpgradeEngine` | Scoped | 可用（编排核心） |
| `IUpgradeCoordinator` | `UpgradeCoordinator` | Singleton | 可用 |
| `IUpgradeMaintenanceModeManager` | `DefaultUpgradeMaintenanceModeManager` | Singleton | 视需求替换 |
| `IUpgradeFileUpdater` | `NullUpgradeFileUpdater` | Singleton | 空实现（需要文件替换才换） |
| `IRollingRestartCoordinator` | `NullRollingRestartCoordinator` | Singleton | 空实现（需要滚动重启才换） |

### 启动自动检查

`XiHanUpgradeModule.OnPostApplicationInitializationAsync` 在应用初始化后运行，受 `XiHanUpgradeOptions.EnableAutoCheckOnStartup`（默认 `true`）门控：为 `true` 且已注册 `IUpgradeVersionStore` / `IUpgradeStatusService` 时，调用 `IUpgradeStatusService.EnsureInitializedAsync()` 完成初始化。**注意：启动阶段只做「初始化」，真正执行升级由 `IUpgradeEngine.ExecuteAsync` / `IUpgradeCoordinator.StartAsync` 触发。**

## 工作原理（UpgradeEngine 编排）

`UpgradeEngine.ExecuteAsync` 的核心流程（`ExecuteForTenantAsync`）：

1. **主节点门控**：若配置了 `PrimaryNodeName` 且当前节点非主节点，直接返回不升级。
2. **建表 + 取版本**：`EnsureTablesAsync` → `GetOrCreateAsync` 取 / 建系统版本记录。
3. **收集脚本 + 比对版本**：合并所有 `IUpgradeScriptProvider` 的脚本，取最新脚本版本；`SemanticVersion.Compare` 判定 `needDbUpgrade`（DbVersion < 最新脚本版本）或 `needAppUpgrade`（记录 AppVersion < 当前 App 版本）。都不需要则返回「无需升级」。
4. **抢锁**：`TryAcquireLockAsync(resourceKey, expiry, nodeName)` 抢占带过期时间的资源锁；抢不到返回「升级锁已被占用」。多租户下锁键带 `:Tenant_{id}` 后缀。
5. **执行升级**（锁作用域内，`IAsyncDisposable` 保证释放）：`SetUpgradingAsync` → 可选进维护模式 → `ExecuteMigrationsAsync` 迁移 → `SetUpgradeCompletedAsync` 回写版本 → 可选替换文件 → 退出维护模式 → 释放锁 → 可选滚动重启。
6. **迁移执行**：脚本按版本分组、`SemanticVersion` 排序，同版本内按脚本名有序；逐条 `HasMigrationHistoryAsync` 去重（已执行则跳过，保证**幂等**），读文件内容交 `IUpgradeMigrationExecutor.ExecuteAsync(sql)` 执行，成功 / 失败都写 `UpgradeMigrationHistory`；任一脚本失败即抛出并 `SetUpgradeFailedAsync`。
7. **多租户隔离**：`EnableMultiTenantIsolation=true` 时，遍历 `IUpgradeTenantProvider.GetTenants()`，逐租户 `ICurrentTenant.Change(...)` 后执行升级，任一租户失败即中止。**注意**：默认 `DefaultUpgradeTenantProvider` 只返回当前 `ICurrentTenant`（单个租户 / 宿主），并不会遍历租户库中的全部租户——要做「逐全体租户」批量升级需自行实现 `IUpgradeTenantProvider`，从租户仓储读取全部租户列表。

版本解析：`AppVersion` 优先取选项，否则从入口程序集版本（`ReflectionHelper.GetEntryAssemblyVersion`）；`NodeName` 优先取选项，否则 `机器名-实例Id`。

## 脚本组织约定（FileSystemUpgradeScriptProvider）

默认脚本提供者从 `MigrationsRootPath`（默认 `migrations`，相对路径基于应用根目录 `AppContext.BaseDirectory`）扫描：

- 根目录下每个**子目录名即版本号**（须能被 `SemanticVersion.TryParse` 解析，非法目录名跳过）。
- 每个版本目录内的 `*.sql` 文件为该版本的迁移脚本，按文件名有序执行。
- 整体先按版本语义排序、再按脚本名排序。

```
migrations/
├── 1.0.0/
│   ├── 001_init.sql
│   └── 002_seed.sql
├── 1.1.0/
│   └── 001_add_column.sql
└── 2.0.0/
    └── 001_refactor.sql
```

## 核心能力

- **版本存储与状态管理**：`IUpgradeVersionStore` 建表、取 / 建版本记录、写升级中 / 完成 / 失败状态、追加并去重迁移历史。
- **迁移执行（幂等）**：按语义化版本分组排序脚本，逐条去重执行，历史留痕；执行经 `IUpgradeMigrationExecutor`（应保证事务）。
- **分布式锁**：`IUpgradeLockProvider` 抢占带过期的资源锁，`IUpgradeLockToken`（`IAsyncDisposable`）负责释放，确保集群内单节点升级。
- **启动自动检查**：受 `EnableAutoCheckOnStartup` 门控，在 `OnPostApplicationInitializationAsync` 初始化。
- **语义化版本比较**：`SemanticVersion` 提供解析与比较，用于强制升级判定与脚本排序。
- **主节点 / 多租户编排**：可配置仅主节点执行升级，支持逐租户隔离升级。
- **运维扩展**：维护模式、程序文件替换、滚动重启均为可插拔扩展点（默认 Null 空实现）。

## 主要 API / 类型

| 类型 | 关键成员 / 说明 |
| --- | --- |
| `IUpgradeEngine` / `UpgradeEngine` | 升级引擎（编排核心）：`Task<UpgradeStartResult> ExecuteAsync(CancellationToken)` |
| `IUpgradeCoordinator` / `UpgradeCoordinator` | 升级协调器（后台启动）：`Task<UpgradeStartResult> StartAsync()`；内部用 `AsyncLock` 防重入，若上一次任务尚未完成，再次调用直接返回 `Started=false`、`Status=Upgrading` 而不会并发再起一个任务 |
| `IUpgradeStatusService` / `UpgradeStatusService` | 状态服务：`EnsureInitializedAsync()`、`GetVersionSnapshotAsync(clientVersion?, ...)`、`GetUpgradeStatusAsync(...)` |
| `IUpgradeVersionStore` | 版本 / 状态存储、迁移历史：`EnsureTablesAsync`、`GetOrCreateAsync`、`GetLatestHistoryAsync`、`SetUpgradingAsync`、`SetUpgradeCompletedAsync`、`SetUpgradeFailedAsync`、`UpdateDbVersionAsync`、`AddMigrationHistoryAsync`、`HasMigrationHistoryAsync` |
| `IUpgradeMigrationExecutor` | 迁移脚本执行器（内部保证事务）：`Task ExecuteAsync(string sql, CancellationToken)` |
| `IUpgradeScriptProvider` | 脚本发现来源：`Task<IReadOnlyList<UpgradeScript>> GetScriptsAsync(...)`（默认 `FileSystemUpgradeScriptProvider`） |
| `IUpgradeLockProvider` / `IUpgradeLockToken` | 分布式锁：`TryAcquireLockAsync(resourceKey, expiry, nodeName, ...)`；令牌含 `ResourceKey`、`LockId`、`IsReleased`、`ReleaseAsync()`（`IAsyncDisposable`） |
| `IUpgradeTenantProvider` | 多租户升级的租户来源：`GetTenants()` |
| `IUpgradeMaintenanceModeManager` | 维护模式：`EnterAsync(...)` / `ExitAsync(...)` |
| `IUpgradeFileUpdater` / `IRollingRestartCoordinator` | 程序文件替换 / 滚动重启（默认 Null 空实现） |
| `SemanticVersion` | 语义化版本解析（`TryParse`）与比较（`Compare`） |
| `UpgradeScript` | `record UpgradeScript(string Version, string ScriptName, string ScriptPath)` |
| `UpgradeStartResult` | `record`：`Started`、`Status`、`Message` |
| `UpgradeStatus` | 枚举：`Normal=0`、`Upgrading=1`、`Completed=2`、`Failed=3` |
| `UpgradeVersionState` / `UpgradeVersionSnapshot` / `UpgradeMigrationHistory` | 版本状态 / 快照 / 迁移历史模型 |
| `XiHanUpgradeOptions` | 升级选项（配置节 `XiHan:Upgrade`） |

## 配置

配置节：`XiHan:Upgrade`（`XiHanUpgradeOptions.SectionName`）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `MinSupportVersion` | `string` | `"0.0.0"` | 最小支持版本（低于则强制升级） |
| `AppVersion` | `string?` | `null` | 当前应用版本（为空时从入口程序集获取） |
| `MigrationsRootPath` | `string` | `"migrations"` | 迁移脚本根目录（相对路径基于应用根目录） |
| `LockResourceKey` | `string` | `"SystemUpgrade"` | 分布式锁资源键 |
| `LockExpirySeconds` | `int` | `600` | 分布式锁过期时间（秒） |
| `EnableAutoCheckOnStartup` | `bool` | `true` | 启动时自动检查（初始化） |
| `NodeName` | `string?` | `null` | 当前节点名（为空用「机器名-实例Id」） |
| `PrimaryNodeName` | `string?` | `null` | 主节点名（配置后仅主节点可执行升级） |
| `EnableMultiTenantIsolation` | `bool` | `false` | 是否逐租户隔离升级 |
| `ConnectionConfigId` | `string?` | `null` | 升级使用的数据库配置 Id（为空用默认连接） |
| `EnableMaintenanceMode` | `bool` | `true` | 升级期间是否进入维护模式 |
| `EnableFileUpdate` | `bool` | `false` | 是否替换程序文件 |
| `EnableRollingRestart` | `bool` | `false` | 是否滚动重启 |

示例 `appsettings.json`：

```json
{
  "XiHan": {
    "Upgrade": {
      "MinSupportVersion": "1.0.0",
      "MigrationsRootPath": "migrations",
      "LockResourceKey": "SystemUpgrade",
      "LockExpirySeconds": 600,
      "EnableAutoCheckOnStartup": true,
      "PrimaryNodeName": "node-01",
      "EnableMaintenanceMode": true
    }
  }
}
```

## 使用示例

### 定义迁移步骤（SQL 脚本，标准做法）

框架的迁移「步骤」以文件系统 SQL 脚本承载——在 `migrations/<版本号>/<序号_名称>.sql` 放脚本即可（见上文脚本组织约定），引擎会自动发现、按序幂等执行。无需写代码类，脚本即步骤：

```
migrations/1.2.0/001_add_index.sql
migrations/1.2.0/002_backfill.sql
```

### 应用层替换生产必需实现并手动触发升级

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    var services = context.Services;

    // 三项生产必须替换：数据库版本存储 / 分布式锁 / 迁移执行器
    services.Replace(ServiceDescriptor.Scoped<IUpgradeVersionStore, SqlSugarUpgradeVersionStore>());
    services.Replace(ServiceDescriptor.Singleton<IUpgradeLockProvider, RedisUpgradeLockProvider>());
    services.Replace(ServiceDescriptor.Singleton<IUpgradeMigrationExecutor, SqlSugarMigrationExecutor>());
}

// 运维触发一次升级（后台执行）
public async Task<UpgradeStartResult> TriggerUpgrade(IUpgradeCoordinator coordinator)
    => await coordinator.StartAsync();
```

## 扩展点 / 自定义

- **数据库版本存储**：实现 `IUpgradeVersionStore`（建表 / 版本记录 / 状态 / 迁移历史），替换默认 `InMemoryUpgradeVersionStore`。
- **分布式锁**：实现 `IUpgradeLockProvider` + `IUpgradeLockToken`（如基于 Redis），替换默认进程内实现——**多节点部署下这是必换项**。
- **迁移执行器**：实现 `IUpgradeMigrationExecutor.ExecuteAsync(sql)` 并保证事务——**必换**（默认实现直接抛 `InvalidOperationException`）。
- **脚本来源**：多来源可用 `AddUpgradeScriptProvider<T>()` 追加（引擎会合并所有 provider 的脚本）；也可实现 `IUpgradeScriptProvider` 从数据库 / 嵌入资源读取。
- **运维动作**：需要维护模式 / 文件替换 / 滚动重启时，替换对应默认 / Null 实现（`IUpgradeMaintenanceModeManager` / `IUpgradeFileUpdater` / `IRollingRestartCoordinator`）。

## 注意事项与最佳实践

- **默认 `IUpgradeMigrationExecutor` 会直接抛异常**（"未配置 IUpgradeMigrationExecutor 实现"），未替换就启用数据库升级必失败——务必由应用层注册真实执行器。
- **默认锁是进程内内存锁**，多节点部署下形同虚设，无法保证「集群内单节点升级」——生产必须换成 Redis 等分布式锁。
- **默认 `InMemoryUpgradeVersionStore` 虽注册为 Scoped，但内部用 `static` 字典（按租户键分区）保存版本状态与迁移历史**，本质是进程级共享存储：同进程内跨请求 / 跨 Scope 均可见，但进程重启即丢失——这也是为什么它「生产环境必须换成数据库实现」。
- 迁移**幂等**依赖 `HasMigrationHistoryAsync` 去重，去重键是 `(version, scriptName)`——**不要在已发布版本目录里改动已执行脚本的内容**（内容变了但脚本名没变会被判为已执行而跳过）。
- 迁移脚本失败即中止并置失败状态，历史记录会留下 `ErrorMessage`；单版本内多脚本非全事务，注意脚本自身的可回滚 / 可重入设计。
- 单节点升级：不配 `PrimaryNodeName` 时每个节点都视为主节点，靠分布式锁串行化；配了则仅指定节点执行。
- 与本仓库既定策略一致：**升级面向前向、部署重建数据库**，不写向后兼容 / 迁移旧数据的兜底逻辑，遇异常态 fail-closed。

## 依赖模块

- [XiHan.Framework.Core](./core) — 模块化生命周期与依赖注入基础（`IApplicationInfoAccessor` 提供实例 Id）。
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions) — 多租户抽象，支撑逐租户隔离升级（`ICurrentTenant.Change`）。

## 相关模块

- [XiHan.Framework.Data](./data) — 数据访问层，业务侧通常在此实现版本存储与迁移执行器。
- [XiHan.Framework.Caching](./caching) — 分布式锁底座，生产环境的升级锁常基于此实现。
- [XiHan.Framework.Timing](./timing) — 时间处理，升级历史与状态记录常用。
