# XiHan.Framework.Security.Database

## 关于

`XiHan.Framework.Security.Database` 是授权的数据库提供程序：权限授予存放在数据库里，经 `ICache` 缓存。只提供授权，认证仍由 `Security.InMemory`、`Security.Jwt` 或 `Security.OpenIdConnect` 提供。

## 主要特性

* 授予由管理后台增删，多实例共享
* 按（主体、租户）一次查询并经 `ICache` 缓存，授予与撤销后所有共用缓存的实例立即失效；多实例部署装 `Caching.Redis` 与 `Caching.Hybrid` 即一致，判定命中在本机一级缓存
* 数据库不可达时返回 503，不会落成 403 或「没有权限」；缓存后端故障时直接查库，写入时失效失败则调用失败、事务回滚
* 不引用数据库驱动，数据库由所装配的持久化提供程序决定

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.Database --prerelease
```

### 使用

与一个持久化提供程序、一个认证提供程序、一个提供 `caching.local` 的缓存提供程序（如 `InMemoryCachingFeature`，Standard 组合包已带）一起装配；连接串来自绑定 `XiHan:Bindings:db`：

```csharp
builder.AddXiHan(xihan => xihan
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<JwtSecurityFeature>()
    .UseFeature<DatabaseSecurityFeature>());
```

存活时间缺省 30 秒、必须为正，只是兜底（授予与撤销经失效立即生效）；缓存容量由缓存提供程序管：

```json
{ "XiHan": { "Security": { "Database": { "CacheTimeToLiveSeconds": 30 } } } }
```

## 主要类型

此库提供的主要类型：

* `DatabaseSecurityFeature`：启用数据库授权的特性
* `DatabasePermissionGrantStore`：带缓存的授予存储，`InvalidateAsync` / `InvalidateAllAsync` 失效所有实例的授予缓存
* `PermissionGrantRecordStore`：实现授予管理 `IPermissionGrantAdministration`，在业务操作里批量授予、撤销与分页列出
* `PermissionGrantRecord`：需要建表的授予实体

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-database)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* 内存实现：[XiHan.Framework.Security.InMemory](https://www.nuget.org/packages/XiHan.Framework.Security.InMemory)
* SqlSugar 持久化：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)

## 反馈与贡献

`XiHan.Framework.Security.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
