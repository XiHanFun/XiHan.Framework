# XiHan.Framework.Persistence.InMemory

`persistence.relational@1` 的内存默认实现：每种实体一张有界的表，工作单元用覆盖层实现读己之写。只用于开发、原型与测试，没有显式 Provider 时被选中。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `persistence.relational@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryPersistenceFeature` |
| 配置节 | `XiHan:Persistence:InMemory` |
| 依赖 | `XiHan.Framework.Persistence.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

存储 `InMemoryStore` 是进程内单例，按实体类型各有一张 `InMemoryTable<TEntity, TKey>`，容量有上限。工作单元 `InMemoryUnitOfWork` 是作用域服务：仓储的新增、更新、删除先写进它的覆盖层，同一工作单元里的查找与查询把覆盖层叠在表的快照上，所以能读到自己的写入。表与覆盖层只存副本，读取只交出副本。提交时给涉及的表依次加锁，校验通过后应用本工作单元的变更，跨表原子；回滚只丢弃覆盖层。

创建时间、修改时间、租户、并发戳与软删除按 `PersistenceConventions` 执行，语义与 EF Core、SqlSugar Provider 一致，三者通过同一套一致性用例。特性同时注册 `IIdGenerator<Guid>`（`GuidV7IdGenerator`）、Transaction 阶段的 `UnitOfWorkMiddleware` 与持久化消息目录。

## 何时使用

- 本地开发、原型、演示，以及单元测试与集成测试（如 `Testing.TestHost`）。
- 需要零外部依赖地跑通整条管道时；`Profiles.Standard` 缺省装配它。

生产环境改用 [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore) 或 [Persistence.SqlSugar](./persistence-sqlsugar)：再 `UseFeature` 一个显式 Provider，内存实现即被顶替，不装配、不启动。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Persistence.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Standard` 已包含本包，`UseStandardProfile()` 即装配；单独使用时：

```csharp
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;

builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseFeature<InMemoryPersistenceFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:InMemory:MaxEntitiesPerType` | 正整数 | `100000` | 每种实体的容量上限（含已软删除的行）；只接受纯数字，非法值启动时抛出 `ArgumentException` |

```json
{
  "XiHan": {
    "Persistence": {
      "InMemory": {
        "MaxEntitiesPerType": 10000
      }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryPersistenceFeature` | 特性入口；常量 `HealthName` 为 `persistence.inmemory.capacity` |
| `InMemoryPersistenceOptions` | 选项：`MaxEntitiesPerType`；`FromConfiguration(configuration)` |
| `InMemoryStore` | 单例存储；`Table<TEntity, TKey>()` 取表，`Clear()` 清空全部表，同时是健康项贡献者 |
| `InMemoryTable<TEntity, TKey>` | 一种实体的有界表：`Find`、`Snapshot`（交出副本）、`Count`、`Capacity` |
| `InMemoryUnitOfWork` | 作用域工作单元，带覆盖层 |
| `InMemoryRepository<TEntity, TKey>` | 仓储实现，由特性按登记注册 |

应用代码只通过 `IRepository<TEntity, TKey>` 与 `IUnitOfWork` 使用它们；`InMemoryStore.Clear()` 可在测试之间重置数据。

## 使用示例

测试里清空存储，保证用例之间互不影响：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.InMemory;

// host 是测试启动的宿主；存储是单例，清空后所有作用域都看到空表
host.Services.GetRequiredService<InMemoryStore>().Clear();
```

仓储的读写与其他 Provider 完全相同，见[持久化](../guide/persistence)。

## 行为与限制

- 容量：新增使某张表超过 `MaxEntitiesPerType` 时，提交返回 `PERSISTENCE_UNAVAILABLE_002`，该表的整批变更都不写入；覆盖已有行不占容量。
- 读写都复制：查找、`FindManyAsync`、查询交出的是副本，新增、更新存的是调用方实例的副本。在读出的实例上的修改，不经 `UpdateAsync` 写回就不会落库，其他调用看不到，回滚后也不留痕迹。仓储写入的修改时间、并发戳与删除标记同样只进覆盖层。
- 副本是字段逐一的浅复制：标量与协议字段互不影响；集合、嵌套对象等引用类型字段（含聚合的领域事件缓冲）仍与表里的版本共享，不在隔离范围内，不要在读出的实体上原地改集合来期待它不影响别处。
- 提交时校验：覆盖层记下每个写过的标识首次读到的已提交状态。提交时按建表先后给涉及的表依次加锁，在全部锁内逐表校验，任一失败所有表都不改，工作单元记为回滚：
  - 两个工作单元并发新增同一标识，后提交的返回 `PERSISTENCE_CONFLICT_002`。
  - 更新或删除期间，别的工作单元已删掉那一行，或改过带并发戳的那一行，后提交的返回 `PERSISTENCE_CONFLICT_001`，不会把先提交的更新盖掉或把删掉的行写回来。
  - 不带并发戳的实体只校验行还在。
- 更新沿用当前行的创建时间、租户与删除标记；软删除在当前行的副本上打标（调用方实例上的其他改动不写入），删除标记再同步回调用方实例。
- 只读工作单元（只读操作或 `UnitOfWorkOptions(IsReadOnly: true)`）：写入在进覆盖层之前就返回 `PERSISTENCE_UNEXPECTED_001`（批量逐项都是它，空集合仍返回空列表），并把工作单元记为失败，提交回滚并返回同一错误。
- 进程退出数据即丢失；多个进程之间不共享。
- 健康项 `persistence.inmemory.capacity`：任一张表达到容量即 Degraded（`PERSISTENCE_UNAVAILABLE_002`，报告按实体名排序的第一个满表），否则 Ready。
- 实体目录（ADR-0078）：表名取实体类型名，列取有公开 setter 的非集合属性、列名即属性名；库结构读取不支持，返回 `PERSISTENCE_UNAVAILABLE_008`。
- 运行时数据源（ADR-0078）不支持：内存实现没有数据库。注册表 `DataSourceRegistry` 照样登记，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008`。

| 错误码 | 何时出现 |
| --- | --- |
| `PERSISTENCE_CONFLICT_002` | 新增的标识已在表或覆盖层里（含已软删除、其他租户）；提交时发现新增的标识已被别的工作单元提交 |
| `PERSISTENCE_CONFLICT_001` | 更新的游离实例并发戳与当前值不同；提交时发现更新或删除的行已被别的工作单元删除或改过 |
| `PERSISTENCE_NOTFOUND_001` | 更新或删除的实体不存在或不可见 |
| `PERSISTENCE_VALIDATION_001` | 多租户实体没有当前租户 |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许（抛异常） |
| `PERSISTENCE_UNEXPECTED_001` | 只读工作单元里新增、更新、删除；提交时返回写入阶段被拒的同一错误 |
| `PERSISTENCE_UNAVAILABLE_002` | 表已满（提交结果与健康项） |
| `PERSISTENCE_UNAVAILABLE_008` | 运行时数据源与库结构读取：内存实现不支持 |

## 相关

- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [测试与一致性套件](../guide/testing)
- [Persistence.Abstractions](./persistence-abstractions)
- [Profiles.Standard](./profiles-standard)
- 规范：RFC-0005《领域协议与持久化》
