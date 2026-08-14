# 数据访问

框架的数据访问基于 **SqlSugar**。本章讲实体怎么定义、仓储怎么用、查询过滤器做了什么、分页协议长什么样。完整 API 见 [Data 包](../packages/data)。

## 安装

```bash
dotnet add package XiHan.Framework.Data
```

```csharp
[DependsOn(typeof(XiHanDataModule))]
public class MyAppModule : XiHanModule { }
```

配置节 `XiHan:Data:SqlSugarCore`，支持 PostgreSQL / MySQL / SQL Server / Oracle / 达梦 / 人大金仓等。

## 实体基类

按「需要哪些审计能力」选：

| 基类 | 主键 | 审计 | 软删 |
| --- | --- | --- | --- |
| `SugarEntity<TKey>` | 应用生成 | 无 | 否 |
| `SugarEntityWithIdentity<TKey>` | 数据库自增 | 无 | 否 |
| `SugarCreationEntity<TKey>` | 应用生成 | 创建 | 否 |
| `SugarModificationEntity<TKey>` | 应用生成 | 创建 + 修改 | 否 |
| `SugarDeletionEntity<TKey>` | 应用生成 | 创建 + 删除 | 是 |
| **`SugarFullAuditedEntity<TKey>`** | 应用生成 | 全套 | **是** |
| `SugarAggregateRoot<TKey>` | 应用生成 | 全套 | 是 |

每个都有 `SugarMultiTenant*` 变体，**多带一个 `Tenant_Id` 列**——多租户应用一律用这一系列。

自动注入的列：

| 列 | 说明 |
| --- | --- |
| `Basic_Id` | 主键（注意不是 `Id`），默认非自增 |
| `Row_Version` | 乐观并发标识 |
| `Created_Time` / `Created_Id` / `Created_By` | 创建审计，`IsOnlyIgnoreUpdate` |
| `Modified_*` / `Deleted_*` / `Is_Deleted` | 修改与删除审计 |
| `Tenant_Id` | 多租户列 |

::: tip 审计列由 AOP 自动填
走 SqlSugar 的 `DataExecuting` AOP，**业务代码不要手动赋值**。
:::

## 仓储

```csharp
public class OrderService(IRepositoryBase<Order, long> orders) : ITransientDependency
{
    public async Task<Order?> GetAsync(long id)
        => await orders.GetByIdAsync(id);

    public async Task<Order> CreateAsync(Order order)
        => await orders.InsertReturnEntityAsync(order);
}
```

仓储基类实现 `IScopedDependency`，**约定自动注册**，不用手写 DI。

自定义仓储：

```csharp
public interface IOrderRepository : IRepositoryBase<Order, long>
{
    Task<bool> ExistsCodeAsync(string code);
}

public class OrderRepository(ISqlSugarClientResolver resolver)
    : SqlSugarRepositoryBase<Order, long>(resolver), IOrderRepository
{
    public Task<bool> ExistsCodeAsync(string code)
        => DbClient.Queryable<Order>().AnyAsync(x => x.Code == code);
}
```

## 全局查询过滤器

租户过滤与软删过滤由 `ISqlSugarClientResolver` + 全局 QueryFilter AOP 统一挂上，**业务代码不用写 `WHERE TenantId = ?` 和 `WHERE IsDeleted = false`**。

### 写操作的过滤是自动的

配置 `EnableAutoUpdateQueryFilter` / `EnableAutoDeleteQueryFilter` 默认 `true`，SqlSugar 的 `Updateable<T>()` / `Deleteable<T>()` 工厂内部**已经自动挂了一次**。

::: danger 仓储里禁止再显式调用 `.EnableQueryFilter()`
重复挂会把同一份过滤烘进 WHERE 两遍、生成同名参数 `@constant1001`；一旦叠加 Diff 的 `GetDiffTable` 重查旧值就崩（MySQL 驱动直接抛，PG 容忍但仍是冗余死条件）。

`.EnableDiffLogEvent()` 保留，它单独用是安全的。
:::

### 写路径的租户边界

**读共享 ≠ 写共享**：全局过滤器为「读共享」放行 `TenantId=0` 的平台全局行，但写路径**不复用这个口径**——租户上下文内禁止改写/删除非本租户行（含全局行）。预读守卫会校验取回行的 `TenantId`，条件写自动追加当前租户 `Where`。

维护全局数据的唯一合法入口是**平台态**（`ICurrentTenant.Change(null)`）。

## 分页与查询协议

分页请求是两段结构（`PageRequestDtoBase`）：

```json
{
  "conditions": {
    "keyword": { "value": "张", "fields": ["userName", "nickName"] },
    "filters": [
      { "field": "status", "operator": 1000, "value": "Enabled" },
      { "field": "createdTime", "operator": 4000, "values": ["2026-01-01", "2026-08-04"] }
    ],
    "sorts": [{ "field": "createdTime", "direction": 1001, "priority": 0 }]
  },
  "page": { "pageIndex": 1, "pageSize": 20 }
}
```

`QueryOperator`：`Equal`(1000) / `NotEqual`(1001) / `GreaterThan`(1002) / `GreaterThanOrEqual`(1003) / `LessThan`(1004) / `LessThanOrEqual`(1005) / `Contains`(2000) / `StartsWith`(2001) / `EndsWith`(2002) / `In`(3000) / `NotIn`(3001) / `Between`(4000) / `IsNull`(5000) / `IsNotNull`(5001)。

`SortDirection`：`Ascending`(1000) / `Descending`(1001)。

`page`：`pageIndex` 从 1 开始（小于 1 自动纠正），`pageSize` 默认 `20`、**上限 `500`**（超出自动截断）。

响应是 `PageResultDtoBase<T>`：`Items` + `Page`（`totalCount` / `totalPages` / `hasNext` / `startRecord` 等）+ 可选 `ExtendDatas`。

::: warning 分页方法要显式标 `[HttpPost]`
动态 API 会把 `GetXxxPageAsync` 推导成 GET，请求体绑不上。见 [动态 API](./dynamic-api)。
:::

## 建库建表与种子

| 配置 | 说明 |
| --- | --- |
| `EnableDbInitialization` | 启动时自动建库 |
| `EnableTableInitialization` | 启动时 CodeFirst 建表 |
| `EnableDataSeeding` | 启动时播种 |
| `TableInitialization` | 建哪些表（特性 `[TableInitialization]` + 分组/名单/委托筛选） |
| `DataSeeding` | 跑哪些种子（特性 `[DataSeeding]` + 分组/名单/委托筛选） |

开关只管开不开，范围由后两项决定：默认全量，标 `[TableInitialization(false)]` 的实体不建、标 `Target = DbInitializationTarget.Platform` 的实体不进租户独立库；要整体自己实现就 `Replace` 掉 `IDbEntityTypeProvider` / `IDataSeederSelector`。细节见 [XiHan.Framework.Data](../packages/data#选择初始化范围)。

::: danger `DbInitializer` 表存在就跳过，从不补列
给既有实体加字段后部署必报「列不存在」。要么重建数据库，要么手动 `ALTER TABLE`。**框架不是迁移工具。**
:::

## 读写分离

`ConnectionConfigs[].SlaveConnectionConfigs` 填了从库后，SELECT 自动走从库、写与事务走主库。

::: warning `HitRate` 绑不上 appsettings
它是 SqlSugar 的**字段**不是属性，配了也恒为 0。框架会把权重 0 的从库归一化为 `DefaultSlaveHitRate`（默认 10），所以不写也能等权分担读。需要差异化权重用代码钩子 `ConfigureConnectionConfigs`。
:::

## 数据变更日志

`EnableDiffLog` **默认 `false`**——不开则 Diff AOP 不挂载，收集到的差异被直接丢弃。开启后每个写操作会多一次 SELECT 用于算差异，且只覆盖走仓储的写。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 部署后报「列不存在」 | 加了字段没重建库 |
| 写操作报参数重名 | 仓储里显式调了 `.EnableQueryFilter()` |
| 查到了别的租户数据 | 实体没继承 `SugarMultiTenant*` 系列 |
| 变更日志空 | `EnableDiffLog` 没开 |
| 时间列投影崩溃 | `DateTimeOffset` 别做单列标量投影，整行取实体 |

## 下一步

- [工作单元与事务](./uow)：事务边界与提交时序
- [多租户](./multi-tenancy)：租户解析与隔离
- [Data 包](../packages/data)：完整配置项与 API
