# XiHan.Framework.Localization.Database

`localization.resources@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0010）。翻译以 `MessageTranslation`（键、区域、模板）经 `persistence.relational@1` 的仓储读写；数据库里的翻译优先于嵌入的语言文件，没有的键仍走嵌入目录与回退链。装配后顶替默认的 `Localization.InMemory`。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, o) => o.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseLocalizationFeature>());

// AppDbContext.OnModelCreating：登记实体后应用协议约定，键与区域的唯一索引由实体上的 [EntityIndex] 建出
modelBuilder.Entity<MessageTranslation>();
modelBuilder.ApplyPersistenceConventions();

// 管理操作的 HandleAsync 里（Transaction 阶段内）批量写入；事务在这次调用返回时才提交
var results = await store.UpsertAsync([new MessageTranslationEntry("orders.order.not_found", "zh-CN", "订单 '{id}' 找不到。")], context.CancellationToken);

// 写入的调用成功返回之后（事务已提交）再重载，快照立即生效；在同一个操作里紧接着重载读不到未提交的行
var reloaded = await loader.ReloadAsync(cancellationToken);
```

```json
{ "XiHan": { "Localization": { "DefaultLocale": "en-US", "Database": { "RefreshIntervalSeconds": 300, "MaxTranslations": 100000 } } } }
```

## 规则

- `DatabaseMessageLocalizer`：每个回退区域先查数据库快照再查目录，其余语义与内存实现一致（通过 `Localization.Conformance`）。
- `MessageTranslationLoader`：宿主进入服务状态后经完整管道分页加载（按 ID，每页 1000，最多 `MaxTranslations` 条，超出记警告），整份替换快照；按 `RefreshInterval` 刷新（0 关闭）；失败保留上一份快照并记到 `LastError`。
- 健康项 `localization.database.translations`（加载器自己实现，以最近一次加载的结果上报，不另起探测循环）：还没加载过为 Started，加载成功为 Ready，最近一次加载失败为 Degraded（错误是那次加载的失败），应用仍在服务。
- `MessageTranslationStore`（作用域）：`UpsertAsync` / `RemoveAsync` 批量逐项结果（不存在 → `LOCALIZATION_NOTFOUND_001`），`ListAsync` 分页；只能在调用的 Transaction 阶段内使用。`UpsertAsync` 先按 (键, 区域) 查已有的行（键每 1000 个一批、结果按标识分页读完），已有的更新、没有的新增——同一键与区域再写一次是更新，不会写出第二行（早先只读第一页，已有行超过 1000 条时超出的被当成新行再插一遍）。两个调用同时新增同一个键与区域：后插入的撞上唯一索引，那一批新增的项返回 `PERSISTENCE_CONFLICT_003`、调用回滚，重试即按更新处理（ADR-0072）；内存持久化不建索引、不校验唯一性。
- 键须符合消息键文法，区域规范化大小写；重复的键与区域由唯一索引 `UX_MessageTranslation_Ke_Lo`（`(Key, Locale)`，实体上的 `[EntityIndex]`，ADR-0072）约束，SqlSugar 的 `InitTables` 与 EF Core 的 `ApplyPersistenceConventions()` 都按它建；已有的库按此名字补建（表里有重复行时先去重）。
- `Locale` 列长取 `LocaleName.MaxLength`（35），合法的区域名都能写入。早先的版本声明为 16：SqlSugar 的 `InitTables` 与 EF Core 的 `EnsureCreated` 都不改已有列的长度，按旧版本建过表的库要手工放宽（如 PostgreSQL `ALTER TABLE <翻译表> ALTER COLUMN <区域列> TYPE varchar(35);`）或走一次迁移；SQLite 不校验长度。

## 依赖方向

依赖 `XiHan.Framework.Localization.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`。

## 状态

P3：可用，通过 `Localization.Conformance`。
