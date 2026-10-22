# XiHan.Framework.Localization.Database

`localization.resources@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0010）。翻译以 `MessageTranslation`（键、区域、模板）经 `persistence.relational@1` 的仓储读写；数据库里的翻译优先于嵌入的语言文件，没有的键仍走嵌入目录与回退链。装配后顶替默认的 `Localization.InMemory`。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, o) => o.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseLocalizationFeature>());

// AppDbContext.OnModelCreating
modelBuilder.Entity<MessageTranslation>(static t => t.HasIndex(static x => new { x.Key, x.Locale }).IsUnique());

// 管理操作里（Transaction 阶段内）批量写入，然后让快照立即生效
var results = await store.UpsertAsync([new MessageTranslationEntry("orders.order.not_found", "zh-CN", "订单 '{id}' 找不到。")], ct);
await loader.ReloadAsync(ct);
```

```json
{ "XiHan": { "Localization": { "DefaultLocale": "en-US", "Database": { "RefreshIntervalSeconds": 300, "MaxTranslations": 100000 } } } }
```

## 规则

- `DatabaseMessageLocalizer`：每个回退区域先查数据库快照再查目录，其余语义与内存实现一致（通过 `Localization.Conformance`）。
- `MessageTranslationLoader`：宿主进入服务状态后经完整管道分页加载（按 ID，每页 1000，最多 `MaxTranslations` 条，超出记警告），整份替换快照；按 `RefreshInterval` 刷新（0 关闭）；失败保留上一份快照。
- `MessageTranslationStore`（作用域）：`UpsertAsync` / `RemoveAsync` 批量逐项结果（不存在 → `LOCALIZATION_NOTFOUND_001`），`ListAsync` 分页；只能在调用的 Transaction 阶段内使用。
- 键须符合消息键文法，区域规范化大小写；重复的键与区域在数据库里应由唯一索引约束。

## 依赖方向

依赖 `XiHan.Framework.Localization.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`。

## 状态

P3：可用，通过 `Localization.Conformance`。
