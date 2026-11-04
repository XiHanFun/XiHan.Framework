# XiHan.Framework.MultiTenancy.InMemory

## 关于

`XiHan.Framework.MultiTenancy.InMemory` 是多租户的默认实现：租户从配置装载到内存。适合开发、测试与租户数量少且固定的应用，进程重启后以配置为准。

## 主要特性

* 租户来自 `XiHan:MultiTenancy:Tenants`，运行时可用 `InMemoryTenantStore.Set` / `Remove` 增删
* 先从主体声明、再从 Baggage 解析租户
* 属性 `ConnectionString` 有值即分库

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.MultiTenancy.InMemory --prerelease
```

### 使用

`Profiles.Standard` 与 `Profiles.Full` 已包含本包；单独装配时：

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryMultiTenancyFeature>());
```

```json
{ "XiHan": { "MultiTenancy": { "RequireTenant": false, "Tenants": {
  "acme": { "DisplayName": "Acme", "IsEnabled": true, "Properties": { "ConnectionString": "<connection-string>", "DatabaseProvider": "postgresql" } },
  "beta": { "IsEnabled": false } } } } }
```

## 主要类型

此库提供的主要类型：

* `InMemoryMultiTenancyFeature`：特性入口
* `InMemoryMultiTenancyOptions`：租户列表与容量上限
* `InMemoryTenantStore`：内存租户存储

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/multitenancy-inmemory)

## 相关包

* 多租户契约：[XiHan.Framework.MultiTenancy.Abstractions](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Abstractions)
* 数据库实现：[XiHan.Framework.MultiTenancy.Database](https://www.nuget.org/packages/XiHan.Framework.MultiTenancy.Database)

## 反馈与贡献

`XiHan.Framework.MultiTenancy.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
