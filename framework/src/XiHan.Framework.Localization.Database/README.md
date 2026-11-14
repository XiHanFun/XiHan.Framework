# XiHan.Framework.Localization.Database

## 关于

`XiHan.Framework.Localization.Database` 是本地化的数据库提供程序：翻译以实体经仓储读写，宿主就绪后装进内存快照，写入提交后经 `ICache` 里的版本戳让所有实例重新加载，数据库里的翻译优先于嵌入的语言文件。需要在运行期修改译文、由运营人员维护翻译时使用，装配后顶替默认的内存实现。

## 主要特性

* 数据库翻译优先，没有的键仍走嵌入目录与回退链
* `MessageTranslationStore` 写入提交后换版本戳：本实例立即重新加载，其他实例按 `ChangeCheckIntervalSeconds`（缺省 1 秒）核对后重新加载；多实例部署装 `Caching.Redis` 与 `Caching.Hybrid` 才能跨实例传递
* 周期刷新兜底（缺省 5 分钟），加载失败保留上一份并上报健康状态
* `MessageTranslationStore` 批量写入、删除与分页列举

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Localization.Database --prerelease
```

### 使用

需要同时装配一个持久化提供程序（EF Core、SqlSugar，开发时也可以是内存持久化），以及提供 `caching.local` 的缓存提供程序（Standard 组合包已带内存缓存，自己挑特性时加 `InMemoryCachingFeature`）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, options) => options.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseLocalizationFeature>());

// AppDbContext.OnModelCreating
modelBuilder.Entity<MessageTranslation>();
modelBuilder.ApplyPersistenceConventions(Database);
```

```json
{ "XiHan": { "Localization": { "DefaultLocale": "en-US", "Database": { "ChangeCheckIntervalSeconds": 1, "RefreshIntervalSeconds": 300, "MaxTranslations": 100000 } } } }
```

绕过 `MessageTranslationStore` 直接改表后，调用 `MessageTranslationLoader.PublishChangeAsync` 让所有实例重新加载；`ReloadAsync` 只重新加载本实例。

## 主要类型

此库提供的主要类型：

* `DatabaseLocalizationFeature`：特性入口
* `DatabaseLocalizationOptions`：版本核对间隔、刷新间隔与条数上限
* `MessageTranslation`：翻译实体
* `MessageTranslationStore`：作用域内的写入、删除、列举，提交后换版本戳
* `MessageTranslationLoader`：加载与重载快照，`PublishChangeAsync` 通知所有实例

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/localization-database)

## 相关包

* 本地化契约：[XiHan.Framework.Localization.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Localization.Abstractions)
* 内存实现：[XiHan.Framework.Localization.InMemory](https://www.nuget.org/packages/XiHan.Framework.Localization.InMemory)
* 持久化契约：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)

## 反馈与贡献

`XiHan.Framework.Localization.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
