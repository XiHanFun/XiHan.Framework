# ADR-0265 仓储与工作单元的异步方法返回 ValueTask

## 状态

已采纳（`Persistence.Abstractions` 的 `IRepository<TEntity, TKey>` 九个方法与 `IUnitOfWork` 三个方法改返回 `ValueTask` / `ValueTask<T>`，两个接口都是 `Stable`，在 5.0 正式版之前破坏；内存、EF Core、SqlSugar 三个实现、`Testing` 的两个替身与一致性套件随之改；`MessageTranslationStore.ListAsync` 是直接转出仓储返回值的公开方法，一并改；规范正文不变）。

## 日期

2026-11-24

## 背景

- 框架的存储契约都返回 `ValueTask`：`ICacheStore`（9 个方法）、`IBlobStore`（6 个）、`IJobStore`（21 个）、`IWorkflowInstanceStore`（9 个）、`IKnowledgeStore`、`ISecretStore` 等。各 `.Abstractions` 包的接口里，返回 `ValueTask` 的异步方法有 192 个，返回 `Task` 的 16 个，其中 12 个在仓储与工作单元上。
- 这两个接口自己也不一致：
  - `IRepository<TEntity, TKey>.FindAsync` 返回 `ValueTask<Maybe<TEntity>>`，其余九个方法返回 `Task<T>`。
  - `IUnitOfWork` 的三个方法返回 `Task`，而 Provider 的直接写入入口 `EnsureTransactionAsync`、同包的 `IDomainEventDispatcher.DispatchAsync`、`IDataSourceProvider`、`IEntityCatalog`、`DataSourceRegistry` 与 `UnitOfWorkScope.RunAsync` 都是 `ValueTask`。
- 同步完成的路径很多，返回 `Task<T>` 每次都要分配一个任务对象：
  - 内存实现的每个操作（它是 `role: default` 的默认实现，Standard 装配的就是它）。
  - 只读工作单元里被拒绝的写入、空集合的批量方法：两个关系型 Provider 在这两条路径上都不触后端。
  - 没有写入的工作单元的提交与回滚（事务延迟开启，只读调用没有可提交的）。
- 调用方已经在绕：`DatabaseTenantStore` 把仓储返回的 `Task` 包进 `new ValueTask<T>(…)` 才能交给收 `ValueTask` 委托的 `CatalogInvocation.ReadAsync`。
- 两个接口是 `Stable`。照 ADR-0131，`Stable` 在同一大版本内不破坏；5.0.0 发版时设包验证基线，之后这个返回类型要到 6.0 才能动。现在 `PublicAPI.Shipped.txt` 全部为空、没有基线，是最后能改的时候。

## 备选

1. **不改，留到 6.0**：否决。分配留在最热的路径上（每个调用至少一次开始与一次提交或回滚），接口内部与同包契约之间的不一致也跟着冻结一个大版本。
2. **只改同步完成概率高的那几个**（提交、回滚、批量方法）：否决。一个接口里两种返回类型正是现在的问题；哪条路径同步完成是实现的事，契约不该按某个实现的现状来分。
3. **加一组返回 `ValueTask` 的新成员或新接口，旧的标弃用并存**：否决。正式版之前没有已发布的形状要兼容，并存只会把两种返回类型都冻结进 `Stable`；仓库不保留推测性的兼容别名。
4. **九个加三个全部改成 `ValueTask`**：采纳。
5. **同包的 `ISchemaReader` 一并改**：不在这次范围，见「决策」第 5 条。
6. **实现保持 `Task`，在接口的显式实现里用 `new ValueTask<T>(task)` 转一层**：否决。同步路径照样分配任务对象，改了签名却没有换来任何东西。
7. **一致性套件的 `RepositoryConformance.RunAsync` 保持收 `Func<InvocationContext, Task<Result>>`，调用点补 `.AsTask()`**：否决。这个辅助方法就是用来在一次调用里执行仓储动作的：套件里有 23 处直接把仓储调用交给它或与它同形的脚本委托，EF Core 与 SqlSugar 的测试里照它写的私有辅助方法还有 22 处。动作改收 `ValueTask<Result>` 之后这些调用与全部 `async` lambda 都不用改，写 Provider 的人照套件补充用例时也不用改。套件是 `Preview`。

## 决策

1. **签名**（`cancellationToken` 等参数不变）：

   | 成员 | 原返回类型 | 现返回类型 |
   | --- | --- | --- |
   | `IRepository<TEntity, TKey>.FindManyAsync` | `Task<IReadOnlyList<TEntity>>` | `ValueTask<IReadOnlyList<TEntity>>` |
   | `IRepository<TEntity, TKey>.ListAsync` | `Task<IReadOnlyList<TEntity>>` | `ValueTask<IReadOnlyList<TEntity>>` |
   | `IRepository<TEntity, TKey>.CountAsync` | `Task<long>` | `ValueTask<long>` |
   | `IRepository<TEntity, TKey>.AddAsync` | `Task<Result>` | `ValueTask<Result>` |
   | `IRepository<TEntity, TKey>.AddRangeAsync` | `Task<IReadOnlyList<Result>>` | `ValueTask<IReadOnlyList<Result>>` |
   | `IRepository<TEntity, TKey>.UpdateAsync` | `Task<Result>` | `ValueTask<Result>` |
   | `IRepository<TEntity, TKey>.UpdateRangeAsync` | `Task<IReadOnlyList<Result>>` | `ValueTask<IReadOnlyList<Result>>` |
   | `IRepository<TEntity, TKey>.RemoveAsync` | `Task<Result>` | `ValueTask<Result>` |
   | `IRepository<TEntity, TKey>.RemoveRangeAsync` | `Task<IReadOnlyList<Result>>` | `ValueTask<IReadOnlyList<Result>>` |
   | `IUnitOfWork.BeginAsync` | `Task` | `ValueTask` |
   | `IUnitOfWork.CommitAsync` | `Task<Result>` | `ValueTask<Result>` |
   | `IUnitOfWork.RollbackAsync` | `Task` | `ValueTask` |

   `IRepository<TEntity, TKey>.FindAsync` 本来就是 `ValueTask<Maybe<TEntity>>`，不变。
2. **实现**：
   - 同步完成的路径直接返回已完成的 `ValueTask`（`ValueTask.CompletedTask`、`ValueTask.FromResult(…)`，或 `async ValueTask` 方法在第一个真正挂起的 `await` 之前返回），不包 `Task.FromResult` 再转。
   - 内存实现的全部操作、两个关系型 Provider 的只读拒绝与空集合批量都是这样的路径。
   - 手里只有第三方库给的 `Task` 时用 `new ValueTask<T>(task)` 交出（EF Core 仓储的 `CountAsync` 转出 `LongCountAsync` 的任务），不为它多写一层状态机。
   - 异常的时机不变：原先同步抛出的参数校验与「没有活动的工作单元」仍同步抛出，原先进任务的仍进任务。
3. **调用方**：
   - 直接 `await` 的不用改。
   - `ValueTask` 只能消费一次。要交给只收 `Task` 的接口（`Task.WhenAll`、`Task.WhenAny`、`Func<Task>` 形参）、存进变量或集合稍后再等、或等待多次时，在调用点先 `.AsTask()`。
   - 仓库的构建把 CA2012（`ValueTask` 的用法）当错误，存进局部变量、不经等待读 `.Result` 这类写法过不了构建。
4. **随之变化的公开签名**：
   - `EntityFrameworkCoreUnitOfWork`、`SqlSugarUnitOfWork`（`Preview`）的 `BeginAsync` / `CommitAsync` / `RollbackAsync`，实现接口。
   - `Testing` 的 `ScriptedUnitOfWork` 与 `ScriptedRepository<TEntity, TKey>`（`Preview`），实现接口。
   - `RepositoryConformance.RunAsync(host, action, tenantId)`（`Preview`）的 `action` 由 `Func<InvocationContext, Task<Result>>` 改为 `Func<InvocationContext, ValueTask<Result>>`，见备选 7。
   - `Localization.Database` 的 `MessageTranslationStore.ListAsync`（`Preview`）由 `Task<IReadOnlyList<MessageTranslation>>` 改为 `ValueTask<IReadOnlyList<MessageTranslation>>`：它不带 `async`，直接把仓储的返回值转出去，同一个类的 `UpsertAsync` 与 `RemoveAsync` 本来就是 `ValueTask`。保持 `Task` 就得在每次调用上 `.AsTask()`。
5. **不在范围**：
   - `ISchemaReader.ListTablesAsync` / `GetTablesAsync` 与扩展方法 `SchemaReaderExtensions.GetTableAsync` 仍返回 `Task`。它读库结构，不是仓储与工作单元这一族；是 `Preview`，照 ADR-0131 正式版之后仍可在小版本改；每次调用都要查系统表，没有同步完成的路径。要不要统一另行决定。
   - 生命周期方法 `SnowflakeIdFeature.StartAsync` / `StopAsync`（内核 `IHostedFeature` 的形状，与 `IHostedService` 一致）不动。
   - 同包的 `IDomainEventDispatcher`、`IDataSourceProvider`、`IEntityCatalog`、`DataSourceRegistry`、`UnitOfWorkScope` 已经是 `ValueTask`；`IIdGenerator<TKey>.NewId` 是同步的。
   - 各 `.Database` 包里自己带 `async` 的方法（`GrantRows.ListAllAsync`、`RecordQueries.FindManyAsync` 等）返回什么与仓储的返回类型无关，不动。
6. **PublicAPI 与包验证**：`PublicAPI.Shipped.txt` 为空，受影响的行都在 `PublicAPI.Unshipped.txt`，由 `Sync-PublicApi.ps1` 原位替换；还没有包验证基线，不需要也无从登记豁免。

## 理由

- 存储契约统一成一种返回类型，写 Provider 的人不用逐个接口记「这个是 `Task` 还是 `ValueTask`」，在存储之间转调（把仓储调用交给收 `ValueTask` 委托的外壳）也不用再包一层。
- 默认实现是内存实现，它的每个操作都同步完成；测试、示例与不装数据库的应用走的都是它，返回 `ValueTask` 之后这些路径不再为返回值分配。
- `ValueTask` 的代价是「只能消费一次」。仓储与工作单元的调用几乎都是就地 `await`：全仓库正式代码里依赖 `Task` 的只有三处（见「影响」），都是把调用交给委托或直接转出。同一个工作单元里的仓储调用本来也不并行（EF Core 的 `DbContext` 不支持并发使用），`Task.WhenAll` 这类组合在这里不是常见写法。
- 留到正式版之后就只能等 6.0。

## 影响

- **写持久化 Provider 的人**：`IRepository<TEntity, TKey>` 除 `FindAsync` 之外的九个方法与 `IUnitOfWork` 的三个方法改返回类型。`async Task<T>` 方法把 `Task` 换成 `ValueTask` 即可；不带 `async` 的方法把 `Task.FromResult(x)` 换成 `ValueTask.FromResult(x)`、`Task.CompletedTask` 换成 `ValueTask.CompletedTask`，直接返回第三方任务的包成 `new ValueTask<T>(task)`。
- **应用代码**：
  - 直接 `await` 的调用重新编译即可。
  - 不带 `async`、把仓储或工作单元的返回值直接 `return` 出去的方法（`public Task<IReadOnlyList<Order>> LargestAsync(…) => orders.ListAsync(…)`）：把自己的返回类型改成对应的 `ValueTask`，或在调用上加 `.AsTask()`。
  - 把调用交给 `Task.WhenAll` / `Task.WhenAny`、`Func<Task>` / `Func<Task<T>>` 形参（含方法组 `repository.AddRangeAsync`）、`Task.Run(() => repository.…)` 的：在调用上加 `.AsTask()`，或把委托类型改成返回 `ValueTask`。
  - 把返回值存进变量或集合稍后再等、等待多次、`ContinueWith`：先 `.AsTask()`。
  - 测试里 `Assert.ThrowsAsync<T>(() => unitOfWork.CommitAsync(ct))` 改成 `Assert.ThrowsAsync<T>(async () => await unitOfWork.CommitAsync(ct))`。
  - 自写的 `IRepository<TEntity, TKey>` / `IUnitOfWork` 替身同「写持久化 Provider 的人」。
  - 二进制不兼容：按旧签名编译的程序集要重新编译。
- **仓库里依赖 `Task` 的调用方**：
  - `MultiTenancy.Database` 的 `DatabaseTenantStore`：两处 `new ValueTask<IReadOnlyList<TenantRecord>>(repository.…)` 去掉包装。
  - `Scheduling.Database` 的 `RecordQueries.WriteAsync`：收方法组 `repository.UpdateRangeAsync` / `repository.AddRangeAsync` 的委托形参改成返回 `ValueTask<IReadOnlyList<Result>>`。
  - `Localization.Database` 的 `MessageTranslationStore.ListAsync`：见决策 4。
  - 一致性套件：`RunAsync`、内部的 `InvokeScriptAsync` 与 `ConformanceScripts` 的脚本委托改成返回 `ValueTask<Result>`；三处 `Assert.ThrowsAsync(() => …)` 改成 `async () => await …`。
  - 测试项目：七个测试类的私有辅助方法（六个与套件 `RunAsync` 同形，一个收仓储与取消令牌）照样改委托类型；18 处 `Assert.ThrowsAsync(() => …)` 改成 `async () => await …`；`GrantInvocationTests` 把仓储调用交给收 `Task<T>` 委托的 `GrantInvocation.ReadAsync`，在调用上 `.AsTask()`；`CatalogInvocationTests` 去掉一处 `new ValueTask<T>(…)` 包装；两个测试项目里的工作单元与仓储替身改签名。
  - 其余调用（各 `.Database` 包、发件箱、`UnitOfWorkMiddleware`、`UnitOfWorkScope`、示例与基准）都是就地 `await`，没有改动。`Persistence.Generators` 只生成实体登记，不涉及这些返回类型。
- **行为**：不变。结果、错误码、状态机、事务开启的时机与异常的时机都与原先相同，没有新增或改动的一致性用例；签名由 PublicAPI 文件钉住。
- **规范**：RFC-0005 用语言无关的方式描述仓储与工作单元的操作，没有写返回类型，不改；没有新的一致性向量。
- **文档**：`Persistence.Abstractions` 的 README 示例改成返回 `ValueTask` 并写明只能消费一次；文档站的包参考页 `packages/persistence-abstractions.md` 有同一段示例，仓储页、工作单元页、一致性套件页与 `packages/localization-database.md` 随后跟上，更新日志在「升级须知」写迁移方式。
- **验证的范围**：两个关系型 Provider 在 PostgreSQL、MySQL、SQL Server、Oracle 上的门控用例只在 CI 跑；本机覆盖的是内存实现与两个 Provider 在 SQLite 上的用例。这两个 Provider 的改动只有签名、`BeginAsync` 的已完成返回值（EF Core 另有它转调的私有方法 `BeginOnShardAsync` 的返回类型）与 EF Core `CountAsync` 的一处包装。
