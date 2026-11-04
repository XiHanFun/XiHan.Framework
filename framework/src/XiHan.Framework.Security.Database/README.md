# XiHan.Framework.Security.Database

## 关于

`XiHan.Framework.Security.Database` 是授权的数据库提供程序：权限授予存放在数据库里，带有界的 TTL 缓存。只提供授权，认证仍由 `Security.InMemory`、`Security.Jwt` 或 `Security.OpenIdConnect` 提供。

## 主要特性

* 授予由管理后台增删，多实例共享
* 按（主体、租户）一次查询并缓存，写入后相关缓存立即失效
* 数据库不可达时返回 503，不会落成 403 或「没有权限」
* 不引用数据库驱动，数据库由所装配的持久化提供程序决定

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.Database --prerelease
```

### 使用

与一个持久化提供程序、一个认证提供程序一起装配；连接串来自绑定 `XiHan:Bindings:db`：

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

缓存可调（缺省 10000 条、30 秒）：

```json
{ "XiHan": { "Security": { "Database": { "CacheSize": 10000, "CacheTimeToLiveSeconds": 30 } } } }
```

## 主要类型

此库提供的主要类型：

* `DatabaseSecurityFeature`：启用数据库授权的特性
* `DatabasePermissionGrantStore`：带缓存的授予存储
* `PermissionGrantRecordStore`：在业务操作里批量授予、撤销与分页列出
* `PermissionGrantRecord`：需要建表的授予实体

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-database)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* 内存实现：[XiHan.Framework.Security.InMemory](https://www.nuget.org/packages/XiHan.Framework.Security.InMemory)
* SqlSugar 持久化：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)

## 反馈与贡献

`XiHan.Framework.Security.Database` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
