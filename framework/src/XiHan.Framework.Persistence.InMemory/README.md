# XiHan.Framework.Persistence.InMemory

## 关于

`XiHan.Framework.Persistence.InMemory` 持久化的内存实现，没有显式 Provider 时被选中。数据只在进程内，重启即丢，只用于开发、原型与测试。

## 主要特性

* 零外部依赖，`UseStandardProfile()` 已包含
* 工作单元读己之写，回滚丢弃
* 每种实体有容量上限，提交时校验并发冲突

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Persistence.InMemory --prerelease
```

### 使用

单独使用时登记实体并装配特性；生产环境再装配 EF Core 或 SqlSugar Provider，内存实现即被顶替。

```csharp
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseFeature<InMemoryPersistenceFeature>());
```

可选配置每种实体的容量上限（缺省 100000）：

```json
{ "XiHan": { "Persistence": { "InMemory": { "MaxEntitiesPerType": 10000 } } } }
```

## 主要类型

此库提供的主要类型：

* `InMemoryPersistenceFeature`：特性入口
* `InMemoryPersistenceOptions`：容量上限选项
* `InMemoryStore`：进程内存储，`Clear()` 可在测试之间清空

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/persistence-inmemory)

## 相关包

* 持久化抽象：[XiHan.Framework.Persistence.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Persistence.Abstractions)
* EF Core 实现：[XiHan.Framework.Persistence.EntityFrameworkCore](https://www.nuget.org/packages/XiHan.Framework.Persistence.EntityFrameworkCore)
* SqlSugar 实现：[XiHan.Framework.Persistence.SqlSugar](https://www.nuget.org/packages/XiHan.Framework.Persistence.SqlSugar)

## 反馈与贡献

`XiHan.Framework.Persistence.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
