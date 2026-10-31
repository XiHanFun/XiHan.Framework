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

存储 `InMemoryStore` 是进程内单例，按实体类型各有一张 `InMemoryTable<TEntity, TKey>`，容量有上限。工作单元 `InMemoryUnitOfWork` 是作用域服务：仓储的新增、更新、删除先写进它的覆盖层，同一工作单元里的查找与查询把覆盖层叠在表的快照上，所以能读到自己的写入；提交时每张表在锁内原子应用本工作单元的变更，回滚只丢弃覆盖层。

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
| `InMemoryTable<TEntity, TKey>` | 一种实体的有界表：`Find`、`Snapshot`、`Count`、`Capacity` |
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
- 原子性只到单张表：一次提交涉及多张表时，各表依次应用，后一张表失败时前面已应用的表不会撤销，工作单元记为回滚。
- 存储的是对象引用，不做深拷贝：查找返回的就是表里的那个实例。在工作单元里修改它（包括更新时写入的修改时间与并发戳、软删除时写入的删除标记），其他调用立即看得到，调用失败回滚也撤销不了；只有新增与物理删除完全经过覆盖层。
- 不检测跨工作单元的并发：两个工作单元同时新增同一标识或同时更新同一实体，后提交的覆盖先提交的。同一工作单元内的重复标识与游离实例的过期并发戳照常按项报告。
- 只读工作单元允许写进覆盖层，提交时统一拒绝并返回 `PERSISTENCE_VALIDATION_002`。
- 进程退出数据即丢失；多个进程之间不共享。
- 健康项 `persistence.inmemory.capacity`：任一张表达到容量即 Degraded（`PERSISTENCE_UNAVAILABLE_002`，报告按实体名排序的第一个满表），否则 Ready。

| 错误码 | 何时出现 |
| --- | --- |
| `PERSISTENCE_CONFLICT_002` | 新增的标识已在表或覆盖层里（含已软删除、其他租户） |
| `PERSISTENCE_CONFLICT_001` | 更新的游离实例并发戳与当前值不同 |
| `PERSISTENCE_NOTFOUND_001` | 更新或删除的实体不存在或不可见 |
| `PERSISTENCE_VALIDATION_001` | 多租户实体没有当前租户 |
| `PERSISTENCE_VALIDATION_002` | 只读工作单元有变更；工作单元状态不允许（抛异常） |
| `PERSISTENCE_UNAVAILABLE_002` | 表已满（提交结果与健康项） |

## 相关

- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [测试与一致性套件](../guide/testing)
- [Persistence.Abstractions](./persistence-abstractions)
- [Profiles.Standard](./profiles-standard)
- 规范：RFC-0005《领域协议与持久化》
