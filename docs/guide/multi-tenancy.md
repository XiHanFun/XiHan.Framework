# 多租户

框架提供租户上下文、租户解析中间件与数据隔离基础设施。本章讲隔离策略怎么选、上下文怎么切、写路径为什么和读路径不一样。

完整 API 见 [MultiTenancy](../packages/multitenancy) 与 [MultiTenancy.Abstractions](../packages/multitenancy-abstractions)。

## 两种隔离策略

| 策略 | 做法 | 适用 |
| --- | --- | --- |
| **字段级隔离** | 所有租户共库共表，靠 `Tenant_Id` 列 + 全局查询过滤器区分 | **默认**。租户多、单租户数据量不大 |
| 库级隔离 | 每个租户独立连接串 | 单租户数据量大、合规要求物理隔离 |

两种可以混用——同一套代码里，一部分租户走共享库、一部分走独立库，由租户的连接配置决定。

## 租户上下文

`ICurrentTenant` 是运行期的租户身份：

```csharp
public class OrderService(ICurrentTenant currentTenant) : ITransientDependency
{
    public long? TenantId => currentTenant.Id;   // null = 平台态（无租户上下文）
}
```

### 谁来设置它

请求链路里由 **`XiHanTenantResolveMiddleware`** 设置，位置在**认证之后**（要读令牌里的租户 claim）、**授权之前**（授权判定要在租户上下文里进行）。

非请求场景（定时任务、后台作业、控制台）**没有中间件**，要自己切：

```csharp
using (currentTenant.Change(tenantId))
{
    // 这个块里所有仓储查询都带上该租户的过滤
}
```

框架的任务调度与后台作业已经内建了这一步——执行时按「参数 tenantId → 任务归属租户 → 当前异步上下文」的优先级解析并切换。

### 平台态

`Change(null)` 进入**平台态**：没有租户上下文。这是**维护全局 / 跨租户数据的唯一合法入口**。

## 全局记录约定

::: tip `TenantId = 0` 而不是 NULL
框架与 BasicApp 的约定是用 **`TenantId = 0`** 表示平台级/全局记录，**不用 NULL**。业务租户 Id 从 1 开始。

好处：列非空、索引干净、`WHERE TenantId IN (0, @current)` 这类合并查询写起来自然。

需要 `IsGlobal` 语义时用**派生只读属性** `IsGlobal => TenantId == 0`，**不落库**——避免两个字段漂移。
:::

## 读写口径不对称

这是多租户里最容易出事的一点：

```text
读：全局过滤器放行 TenantId IN (0, 当前租户)    ← 租户能读到平台全局数据
写：禁止改写 / 删除非本租户行（含 TenantId=0 的全局行）
```

::: danger 「读共享」不等于「写共享」
如果写路径复用读的口径，租户就能改掉平台的全局数据——这是越权。

框架的做法：预读守卫校验取回行的 `TenantId`，条件写自动追加当前租户 `Where`。要维护全局数据，必须显式进平台态。
:::

## 实体怎么支持多租户

继承 `SugarMultiTenant*` 系列基类即可——它们带 `Tenant_Id` 列并实现 `IMultiTenantEntity`。

::: danger 只加列不实现接口 = 隔离完全失效
全局过滤器是按 `AddTableFilter<IMultiTenantEntity>` 挂的。实体只加了 `TenantId` 列却没实现 `IMultiTenantEntity`，过滤器对它**全程 no-op**——所有租户都能看到所有数据，且没有任何报错。

用框架提供的 `SugarMultiTenant*` 基类就不会踩到；手写实体时务必确认接口实现上了。
:::

## 切换租户

用户从一个租户切到另一个时，正确做法是**复用会话、轮换令牌**：

- 不发登录事件、不新增设备记录；
- 在目标租户上下文里重建授权快照（不同租户可用功能不同）。

如果实现成「登出再登录」，用户会收到两条登录通知、设备列表里多出一台设备。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 查到了别的租户数据 | 实体没实现 `IMultiTenantEntity`（只加了列） |
| 后台任务里查不到数据 | 没有租户上下文，要 `Change(tenantId)` |
| 改不了全局数据 | 这是**有意的**——去平台态改 |
| 切租户后权限没变 | 授权快照没在目标上下文重建 |
| 平台态下反而查不到租户数据 | 平台态没有租户过滤，但业务查询可能自己带了条件 |

## 下一步

- [数据访问](./data)：全局查询过滤器
- [认证与授权](./authentication)：租户解析在管道里的位置
- [MultiTenancy 包](../packages/multitenancy)：完整 API
