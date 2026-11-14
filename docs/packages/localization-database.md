# XiHan.Framework.Localization.Database

`localization.resources@1` 的数据库 Provider：翻译以实体 `MessageTranslation` 经仓储读写，宿主就绪后装进内存快照，写入后经 `ICache` 里的版本戳让所有实例重新加载，数据库里的翻译优先于嵌入的语言文件。需要在运行期修改译文、由运营人员维护翻译的应用使用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Localization.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `localization.resources@1`（`role: provider`）；依赖 `kernel.hosting@1`、`persistence.relational@1`、`caching.local@1` |
| 特性入口 | `DatabaseLocalizationFeature` |
| 配置节 | `XiHan:Localization`、`XiHan:Localization:Database` |
| 依赖 | `XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Caching.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包是显式 Provider，装配后顶替默认的 `Localization.InMemory`：两者同时出现时内存实现被顶替，不装配。它自己注册原语层与内核的语言文件目录，读取同样的 `LocalizationOptions`，并把 `DatabaseMessageLocalizer` 登记为 `IMessageLocalizer` 与 `IMessageResolver`。

`DatabaseMessageLocalizer` 的查找规则：沿回退链的每个区域，先查数据库快照，再按注册顺序查嵌入的目录；其余语义（回退链、按命中区域的文化格式化、缺键或缺参数时退回描述）与内存实现一致。快照是不可变的整份替换，读取不加锁。

翻译的加载由 `MessageTranslationLoader` 负责：宿主进入服务状态后经完整管道首次加载（仓储要求在 Transaction 阶段内读）。之后三种情况重新加载：`ICache` 里的版本戳 `localization:translations:version`（常量 `VersionCacheKey`）变了，每个实例按 `ChangeCheckInterval` 核对；本实例写入后立即唤醒；按 `RefreshInterval` 周期刷新兜底。查找是同步的，翻译仍在进程内快照里，`ICache` 只承载跨实例的信号（ADR-0229）。写入经作用域服务 `MessageTranslationStore`，批量逐项返回结果，提交之后换版本戳。

## 何时使用

- 译文需要在运行期修改，或由管理界面维护：用本包。
- 文案随程序集发布就够了：用默认的 [Localization.InMemory](./localization-inmemory)。

本包需要一个提供 `persistence.relational@1` 的持久化 Provider（EF Core、SqlSugar，开发时也可以是内存持久化），以及一个提供 `caching.local@1` 的缓存 Provider（Standard / Full 组合包已带 `InMemoryCachingFeature`，自己挑特性的宿主要加上它）。多实例部署要让写入立即传到每个实例，装 `Caching.Redis` 与 `Caching.Hybrid`：只装 Redis 时 `ICache` 仍是进程内的，其他实例只能等周期刷新。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Localization.Database --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, options) => options.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseLocalizationFeature>());
```

用 EF Core 时把实体加进应用的 `DbContext` 并应用协议约定，键与区域的唯一索引由实体上的 `[EntityIndex]` 建出：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<MessageTranslation>();
    modelBuilder.ApplyPersistenceConventions(Database);   // 索引由实体上的 [EntityIndex] 建出
}
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Localization:DefaultLocale` | `string` | `en-US` | 默认区域，同 [Localization.Abstractions](./localization-abstractions) |
| `XiHan:Localization:Locales` | `string`（逗号分隔） | 内置 10 种 | 启用的区域 |
| `XiHan:Localization:Database:ChangeCheckIntervalSeconds` | 非负数 | 1 | 核对翻译版本戳的间隔；`0` 不核对，其他实例只靠周期刷新看到写入 |
| `XiHan:Localization:Database:RefreshIntervalSeconds` | 非负数 | 300 | 周期刷新的间隔，兜住绕过 `MessageTranslationStore` 直接改表与版本戳丢失；`0` 关闭周期刷新 |
| `XiHan:Localization:Database:MaxTranslations` | 正整数 | 100000 | 快照的条数上限，超出的不加载并记警告 |

```json
{
  "XiHan": {
    "Localization": {
      "DefaultLocale": "en-US",
      "Database": { "ChangeCheckIntervalSeconds": 1, "RefreshIntervalSeconds": 300, "MaxTranslations": 100000 }
    }
  }
}
```

值不合法时装配阶段抛出 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseLocalizationFeature` | 特性入口（`IHostedFeature`）；常量 `HealthName` = `localization.database.translations` |
| `DatabaseLocalizationOptions` | `RefreshInterval`（缺省 5 分钟，`null` 不周期刷新）、`ChangeCheckInterval`（缺省 1 秒，`null` 不核对版本戳）、`MaxTranslations`；`FromConfiguration` |
| `DatabaseMessageLocalizer` | 实现 `IMessageLocalizer` 与 `IMessageResolver`（`Negotiate` 按启用列表协商调用方区域，内核据此安装调用链的消息作用域）；`TranslationCount` 是当前快照的条数 |
| `Translations.MessageTranslation` | 实体：`Id`（`Guid`）、`Key`（最长 256）、`Locale`（最长 35，即 `LocaleName.MaxLength`）、`Template`（长文本）、`ModifiedAt` |
| `Translations.MessageTranslationEntry` | 要写入的一条：键、区域、模板；构造时校验 |
| `Translations.MessageTranslationKey` | 自然键：键与区域 |
| `Translations.MessageTranslationStore` | 作用域服务：`UpsertAsync`、`RemoveAsync`、`ListAsync`；实现 `IAsyncDisposable`，写过翻译就在作用域释放时（提交之后）调用 `PublishChangeAsync` |
| `Translations.MessageTranslationLoader` | 单例：`ReloadAsync`（只重新加载本实例）、`PublishChangeAsync`（换版本戳并唤醒本实例，让所有实例重新加载）、常量 `VersionCacheKey`、`LastLoadedAt`、`LastError`，同时是健康项的贡献者 |
| `LocalizationMessages` | 本包的消息键与目录 |

## 使用示例

写入与删除放在操作里（`MessageTranslationStore` 只能在调用的 Transaction 阶段内使用），完整的操作写法见[国际化](/common/localization#数据库翻译-provider)：

```csharp
// 在操作的 HandleAsync 里：一次批量写入，逐项结果与入参同序
var written = await store.UpsertAsync(
[
    new MessageTranslationEntry("orders.order.not_found", "zh-CN", "订单 '{id}' 找不到。"),
    new MessageTranslationEntry("orders.order.not_found", "zh-TW", "訂單 '{id}' 找不到。"),
], context.CancellationToken);

var removed = await store.RemoveAsync([new MessageTranslationKey("orders.order.not_found", "ko-KR")], context.CancellationToken);
// 数据库里没有这一行：removed[0].Outcome.Error?.Code == "LOCALIZATION_NOTFOUND_001"
```

经 `MessageTranslationStore` 的写入不用再手动重新加载：调用提交之后，本实例立即重新加载，其他实例在 `ChangeCheckInterval` 内重新加载。运维脚本绕过它直接改了表时，让所有实例重新加载：

```csharp
try
{
    // 换版本戳并唤醒本实例；其他实例在下一次核对时重新加载
    await loader.PublishChangeAsync(cancellationToken);
}
catch (FrameworkException exception) when (exception.Code == CachingErrors.StoreUnavailable)
{
    // 缓存后端不可达：本实例照常重新加载，其他实例等周期刷新
    logger.LogWarning(exception, "Publishing the translation change failed.");
}
```

只想重新加载本实例并拿到结果时用 `ReloadAsync`，失败时快照保持不变、健康项为 Degraded。

## 行为与限制

- 加载：宿主进入服务状态后首次加载，之前只有嵌入目录的文案；按 ID 分页读取，每页 1000 条，读到 `MaxTranslations` 条为止，整份替换快照。
- 刷新：后台循环按 `ChangeCheckInterval` 核对版本戳，变了就重读；被本实例的写入唤醒时立即重读；`RefreshInterval` 到点时不看版本戳直接重读。两者都关闭时只在被唤醒时重读。重新加载彼此串行，后开始的读到的不早于先开始的。读版本戳遇到缓存后端故障时跳过这次核对，靠周期刷新收敛。装了两级缓存时核对版本戳是本机一级缓存命中，不出进程。
- `ReloadAsync` 立即重读本实例，只能在宿主服务期间调用；`PublishChangeAsync` 换版本戳并唤醒本实例，缓存后端故障时本实例照常重新加载，然后抛出携带 `CACHING_UNAVAILABLE_001` 的 `FrameworkException`。
- 失败：加载失败时保留上一份快照，失败记在 `LastError` 并写一条警告日志。
- 健康项 `localization.database.translations` 由加载器按最近一次加载的结果报告，不另起探测：还没加载过为 Started，成功为 Ready，最近一次失败为 Degraded（携带那次的错误），应用仍在服务。
- 写入：`UpsertAsync` 先按 (键, 区域) 取回已有的行（键每 1000 个一批、结果分页读完），已有的更新、没有的新增，再用仓储的批量方法写入；同一批里重复的键与区域后者覆盖前者，各项结果指向同一行。同一键与区域再写一次是更新，不会写出第二行。表上的唯一索引 `UX_MessageTranslation_Ke_Lo`（`(Key, Locale)`）兜住并发：两个调用同时新增同一个键与区域，后插入的那一批返回 `PERSISTENCE_CONFLICT_003`、调用回滚，重试按更新处理；内存持久化不建索引、不校验唯一性。已有重复行的库，补建唯一索引前先去重（每个键与区域留一行）。`RemoveAsync` 按顺序逐项删除，同一批里同一个键第二次出现时报告不存在。`ListAsync(locale, skip, take)` 按键排序分页，`locale` 为 `null` 时列全部，一页最多 1000 条。
- 写入在调用的事务提交后才落库，所以 `MessageTranslationStore` 在作用域释放时（调用已提交）才换版本戳：本实例立即重新加载，其他实例在 `ChangeCheckInterval` 内重新加载。回滚之后换版本戳只多一次无害的重新加载。换版本戳遇到缓存后端故障只记错误日志、不让调用失败：写入已经提交，本实例照常重新加载，其他实例等周期刷新。在同一个操作里紧接着 `ReloadAsync` 读不到未提交的行。
- 直接构造 `MessageTranslationStore` 或 `MessageTranslationLoader` 的代码按新签名传参：前者多了 `MessageTranslationLoader` 与 `ILoggerFactory`，后者多了 `ICache`；经容器构造的不受影响。
- 校验：`MessageTranslationEntry` 要求键符合消息键文法、模板非空白，区域规范化大小写，不合法抛出 `ArgumentException`。`Locale` 列最长 35 个字符，与区域名文法的上限 `LocaleName.MaxLength` 一致，合法的区域名都能写入。
- 已有表的 `Locale` 列：这一列声明为 35 个字符，更短的列在校验长度的数据库上写不进 17 到 35 个字符的合法区域名（如 `sr-Latn-RS-ekavsk`）。SqlSugar 的 `InitTables` 只建缺表、加缺列，不改已有列的长度，EF Core 的 `EnsureCreated` 也不改已有表，所以这样的表要手工放宽这一列，例如 PostgreSQL 上 `ALTER TABLE <翻译表> ALTER COLUMN <区域列> TYPE varchar(35);`（表名与列名以实际建表结果为准）；用 EF Core 迁移的应用生成一次迁移即可。SQLite 不校验长度，不用处理。
- 通过 `Localization.Conformance` 的 `MessageLocalizerConformance`。

| 错误码 | 含义 |
| --- | --- |
| `LOCALIZATION_NOTFOUND_001` | 删除时数据库里没有这条翻译 |

## 相关

- [国际化](/common/localization)
- [持久化](/persistence/overview)
- [Localization.Abstractions](./localization-abstractions)
- [Localization.InMemory](./localization-inmemory)
- [两级缓存与键管理](/caching/hybrid)
- 规范：RFC-0010《本地化》；ADR-0229
