# XiHan.Framework.MultiTenancy.Abstractions

## 关于

`XiHan.Framework.MultiTenancy.Abstractions` 定义多租户契约：租户信息与存储、租户解析、Tenant 阶段中间件，以及按租户选择数据连接的分片解析。不含存储实现，由内存或数据库提供程序提供。

## 主要特性

* 从已认证主体的声明与 Baggage 解析租户，按注册顺序先到先得
* Tenant 阶段校验格式、核对存储（存在且启用）并写入安全上下文
* 租户属性 `ConnectionString` 有值即分库，解析失败不降级为共库
* `TenantShardScope` 在管道外显式绑定分片

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.MultiTenancy.Abstractions --prerelease
```

### 使用

一般由实现包带入；写自定义租户存储或需要在管道外绑定分片时直接引用。当前租户经安全上下文读取：

```csharp
var tenant = context.GetSecurity().Tenant;

// 管道外（如后台任务）绑定租户的分库
await shardScope.BindTenantAsync("acme", cancellationToken);
```

```json
{ "XiHan": { "MultiTenancy": { "RequireTenant": false, "ShardCacheSize": 10000 } } }
```

## 主要类型

此库提供的主要类型：

* `TenantInfo`：租户 ID、显示名、启用标记与属性
* `ITenantStore`：按 ID 查找与分页列出租户（`TenantQuery`、`TenantPage`）
* `ITenantResolver`：从调用上下文解析租户 ID
* `MultiTenancyOptions`：默认租户、是否必须有租户、是否核对存储
* `ITenantShardResolver`、`TenantShardScope`：租户分片

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/multitenancy-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.MultiTenancy.InMemory](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.InMemory)
* 数据库实现：[XiHan.Framework.MultiTenancy.Database](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Database)
* 一致性套件：[XiHan.Framework.MultiTenancy.Conformance](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Conformance)

## 反馈与贡献

`XiHan.Framework.MultiTenancy.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
