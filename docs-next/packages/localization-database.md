# XiHan.Framework.Localization.Database

`localization.resources@1` 的数据库 Provider：翻译以实体 `MessageTranslation` 经仓储读写，宿主就绪后装进内存快照并按周期刷新，数据库里的翻译优先于嵌入的语言文件。需要在运行期修改译文、由运营人员维护翻译的应用使用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Localization.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `localization.resources@1`（`role: provider`）；依赖 `kernel.hosting@1`、`persistence.relational@1` |
| 特性入口 | `DatabaseLocalizationFeature` |
| 配置节 | `XiHan:Localization`、`XiHan:Localization:Database` |
| 依赖 | `XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Persistence.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包是显式 Provider，装配后顶替默认的 `Localization.InMemory`：两者同时出现时内存实现被顶替，不装配。它自己注册原语层与内核的语言文件目录，读取同样的 `LocalizationOptions`，并把 `DatabaseMessageLocalizer` 登记为 `IMessageLocalizer` 与 `IMessageResolver`。

`DatabaseMessageLocalizer` 的查找规则：沿回退链的每个区域，先查数据库快照，再按注册顺序查嵌入的目录；其余语义（回退链、按命中区域的文化格式化、缺键或缺参数时退回描述）与内存实现一致。快照是不可变的整份替换，读取不加锁。

翻译的加载由 `MessageTranslationLoader` 负责：宿主进入服务状态后经完整管道首次加载（仓储要求在 Transaction 阶段内读），之后按周期刷新。写入经作用域服务 `MessageTranslationStore`，批量逐项返回结果。

## 何时使用

- 译文需要在运行期修改，或由管理界面维护：用本包。
- 文案随程序集发布就够了：用默认的 [Localization.InMemory](./localization-inmemory)。

本包需要一个提供 `persistence.relational@1` 的持久化 Provider（EF Core、SqlSugar，开发时也可以是内存持久化）。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Localization.Database" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, options) => options.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseLocalizationFeature>());
```

用 EF Core 时把实体加进应用的 `DbContext`，并用唯一索引约束键与区域：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<MessageTranslation>(static t => t.HasIndex(static x => new { x.Key, x.Locale }).IsUnique());
}
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Localization:DefaultLocale` | `string` | `en-US` | 默认区域，同 [Localization.Abstractions](./localization-abstractions) |
| `XiHan:Localization:Locales` | `string`（逗号分隔） | 内置 10 种 | 启用的区域 |
| `XiHan:Localization:Database:RefreshIntervalSeconds` | 非负数 | 300 | 周期刷新的间隔；`0` 关闭周期刷新，只在启动后加载一次 |
| `XiHan:Localization:Database:MaxTranslations` | 正整数 | 100000 | 快照的条数上限，超出的不加载并记警告 |

```json
{
  "XiHan": {
    "Localization": {
      "DefaultLocale": "en-US",
      "Database": { "RefreshIntervalSeconds": 300, "MaxTranslations": 100000 }
    }
  }
}
```

值不合法时装配阶段抛出 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseLocalizationFeature` | 特性入口（`IHostedFeature`）；常量 `HealthName` = `localization.database.translations` |
| `DatabaseLocalizationOptions` | `RefreshInterval`（缺省 5 分钟，`null` 不周期刷新）、`MaxTranslations`；`FromConfiguration` |
| `DatabaseMessageLocalizer` | 实现 `IMessageLocalizer` 与 `IMessageResolver`；`TranslationCount` 是当前快照的条数 |
| `Translations.MessageTranslation` | 实体：`Id`（`Guid`）、`Key`（最长 256）、`Locale`（最长 16）、`Template`（长文本）、`ModifiedAt` |
| `Translations.MessageTranslationEntry` | 要写入的一条：键、区域、模板；构造时校验 |
| `Translations.MessageTranslationKey` | 自然键：键与区域 |
| `Translations.MessageTranslationResult` | 批量写入或删除的一项结果：`Key` 与 `Outcome` |
| `Translations.MessageTranslationStore` | 作用域服务：`UpsertAsync`、`RemoveAsync`、`ListAsync` |
| `Translations.MessageTranslationLoader` | 单例：`ReloadAsync`、`LastLoadedAt`、`LastError`，同时是健康项的贡献者 |
| `LocalizationMessages` | 本包的消息键与目录 |

## 使用示例

写入与删除放在操作里（`MessageTranslationStore` 只能在调用的 Transaction 阶段内使用），完整的操作写法见[国际化](../guide/localization#数据库翻译)：

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

写入的调用成功返回之后立即生效：

```csharp
var reloaded = await loader.ReloadAsync(cancellationToken);
if (reloaded.IsFailure)
{
    // 快照保持不变；健康项此时为 Degraded
    logger.LogWarning("Reloading translations failed: {Code}", reloaded.Error!.Code);
}
```

## 行为与限制

- 加载：宿主进入服务状态后首次加载，之前只有嵌入目录的文案；按 ID 分页读取，每页 1000 条，读到 `MaxTranslations` 条为止，整份替换快照。
- 刷新：按 `RefreshInterval` 周期重读；`ReloadAsync` 立即重读，只能在宿主服务期间调用。
- 失败：加载失败时保留上一份快照，失败记在 `LastError` 并写一条警告日志。
- 健康项 `localization.database.translations` 由加载器按最近一次加载的结果报告，不另起探测：还没加载过为 Started，成功为 Ready，最近一次失败为 Degraded（携带那次的错误），应用仍在服务。
- 写入：`UpsertAsync` 一次查询取回已有的行，再用仓储的批量方法写入；同一批里重复的键与区域后者覆盖前者，各项结果指向同一行。`RemoveAsync` 按顺序逐项删除，同一批里同一个键第二次出现时报告不存在。`ListAsync(locale, skip, take)` 按键排序分页，`locale` 为 `null` 时列全部，一页最多 1000 条。
- 写入在调用的事务提交后才落库，要等下一次刷新或在调用返回之后 `ReloadAsync` 才生效；在同一个操作里紧接着 `ReloadAsync` 读不到未提交的行。
- 校验：`MessageTranslationEntry` 要求键符合消息键文法、模板非空白，区域规范化大小写，不合法抛出 `ArgumentException`。`Locale` 列最长 16 个字符。
- 通过 `Localization.Conformance` 的 `MessageLocalizerConformance`。

| 错误码 | 含义 |
| --- | --- |
| `LOCALIZATION_NOTFOUND_001` | 删除时数据库里没有这条翻译 |

## 相关

- [国际化](../guide/localization)
- [持久化](../guide/persistence)
- [Localization.Abstractions](./localization-abstractions)
- [Localization.InMemory](./localization-inmemory)
- 规范：RFC-0010《本地化》
