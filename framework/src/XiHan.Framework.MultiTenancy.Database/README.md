# XiHan.Framework.MultiTenancy.Database

## 关于

`XiHan.Framework.MultiTenancy.Database` 是多租户的数据库提供程序：租户目录以实体经仓储读写，经 `ICache` 按租户缓存，分片从租户属性推出。装配后顶替默认的内存实现，需要同时装配一个持久化提供程序与一个提供 `caching.local` 的缓存提供程序（Standard 组合包已带内存缓存）。

## 主要特性

* 租户目录存在数据库里，`TenantRecordStore` 批量写入与删除，写后让所有共用缓存的实例立即失效
* 多实例部署装 `Caching.Redis` 与 `Caching.Hybrid` 即一致：命中在本机一级缓存，新增、停用、改连接串、删除立即传到每个实例
* 缓存后端故障时直接查库；写入时失效失败则调用失败、事务回滚
* 与分库配合：租户属性 `ConnectionString` 有值即分库

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.MultiTenancy.Database --prerelease
```

### 使用

显式提供程序，和持久化提供程序一起装配（下面用 SqlSugar，按约定映射 `TenantRecord`）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseMultiTenancyFeature>());
```

```json
{ "XiHan": { "MultiTenancy": { "Database": { "CacheTimeToLiveSeconds": 30 } } } }
```

存活时间必须为正（写入经失效立即生效，存活时间只是兜底）；缓存容量由缓存提供程序管。表结构由应用管理；`PropertiesJson` 列用长文本类型。

## 主要类型

此库提供的主要类型：

* `DatabaseMultiTenancyFeature`：特性入口
* `DatabaseMultiTenancyOptions`：目录缓存的存活时间
* `TenantRecord`：租户存储行
* `DatabaseTenantStore`：带缓存的 `ITenantStore`，`InvalidateAsync` / `InvalidateAllAsync` 失效所有实例的目录缓存
* `TenantRecordStore`：实现租户管理 `ITenantAdministration`，作用域内的批量写入与删除；列举用 `ITenantStore.ListAsync`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/multitenancy-database)

## 相关包

* 多租户契约：[XiHan.Framework.MultiTenancy.Abstractions](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Abstractions)
* 内存实现：[XiHan.Framework.MultiTenancy.InMemory](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.InMemory)
* 持久化契约：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)

## 反馈与贡献

`XiHan.Framework.MultiTenancy.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
