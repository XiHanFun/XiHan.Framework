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
    public long? TenantId => currentTenant.Id;   // null 与 0 同义：平台（0 号租户）
}
```

### 谁来设置它

请求链路里由 **`XiHanTenantResolveMiddleware`** 设置，位置在**认证之后**（要读令牌里的租户 claim）、**授权之前**（授权判定要在租户上下文里进行）。租户来源分两种信任级别：

- **令牌**：已认证请求一律以令牌为准——带租户声明即该租户，不带即平台。请求头、查询串不能替已认证身份另选租户，否则宿主令牌可以逐请求自选任意租户、绕过成员关系。
- **外部输入**：未认证请求才看 `X-Tenant-Id` 请求头、`tenantId` 查询串与 `FallbackTenant`。给出的租户必须在 `ITenantStore` 中存在且激活，否则请求以 400 拒绝；靠匿名请求头识别租户的场景（如租户专属登录页），要把租户登记进 `ITenantStore`。

非请求场景（定时任务、后台作业、控制台）**没有中间件**，要自己切：

```csharp
using (currentTenant.Change(tenantId))
{
    // 这个块里所有仓储查询都带上该租户的过滤
}
```

框架的任务调度与后台作业已经内建了这一步——执行时按「参数 tenantId → 任务归属租户 → 当前异步上下文」的优先级解析并切换。

### 平台就是 0 号租户

没有租户上下文（`Id == null`）与 `Change(0)` 同义，都是**平台**，也就是 0 号租户。平台和任何一个租户一样，只看、只写自己的数据：

- 读：平台只看 `TenantId = 0` 的行，**看不到任何租户的数据**；
- 写：新行落 `TenantId = 0`；预置了别的租户标识的插入直接拒绝；只能改写 / 删除 `TenantId = 0` 的行。

不存在「没有租户上下文就看全部、写全部」的隐式口径。跨租户只有显式通道：

| 需求 | 做法 |
| --- | --- |
| 跨租户读取（按全局唯一邮箱定位账号、按会话标识找会话等） | 仓储里 `CreateNoTenantQueryable()`，或在查询上 `.ClearTenantFilter()`（读共享与严格隔离的过滤一并清除） |
| 写某个租户的数据（开通租户、逐租户维护） | `using (currentTenant.Change(tenantId))` 切入该租户后再写 |
| 用户自有行（账号、会话、个人设置） | `TenantWriteGuard.Suppress()`，见 [数据访问](./data) |

`IsAvailable` 表示「当前处于某个业务租户」，平台（`null` 或 `0`）为 `false`。

::: warning 不要直接写 `ClearFilter<IMultiTenantEntity>()`
租户过滤按读共享（`IMultiTenantEntity`）与严格隔离（`IStrictMultiTenantEntity`）两个类型登记，只清前者时严格隔离实体仍被收紧在当前作用域，跨租户读取会静默缺数据。统一用 `ClearTenantFilter()` / `ClearTenantAndSoftDeleteFilter()`。
:::

## 全局记录约定

::: tip `TenantId = 0` 而不是 NULL
框架与 BasicApp 的约定是用 **`TenantId = 0`** 表示平台级/全局记录，**不用 NULL**。业务租户 Id 从 1 开始。

好处：列非空、索引干净、`WHERE TenantId IN (0, @current)` 这类合并查询写起来自然。

需要 `IsGlobal` 语义时用**派生只读属性** `IsGlobal => TenantId == 0`，**不落库**——避免两个字段漂移。
:::

## 读写口径不对称

这是多租户里最容易出事的一点：

```text
读：全局过滤器放行 TenantId IN (0, 当前租户)    ← 租户能读到平台全局数据；平台只读 0
写：只能改写 / 删除当前作用域的行              ← 租户不能改 0 号全局行，平台也不能改租户行
```

平台与租户各自独有、不存在共用的数据（运行数据、日志、会话等），实体再实现 `IStrictMultiTenantEntity` 收紧为严格相等：租户态只看本租户行，不再读共享平台行。

::: danger 「读共享」不等于「写共享」
如果写路径复用读的口径，租户就能改掉平台的全局数据——这是越权。

框架的做法：预读守卫校验取回行的 `TenantId`，条件写自动追加当前作用域的 `Where`。要维护全局数据，在平台（0 号租户）作用域里改。
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
| 后台任务里查不到租户数据 | 后台没有租户上下文即平台，只看得到平台数据；逐个 `Change(tenantId)` 切入租户处理 |
| 改不了全局数据 | 这是**有意的**——在平台（0 号租户）作用域里改 |
| 切租户后权限没变 | 授权快照没在目标上下文重建 |
| 平台下查不到租户数据 | 这是**有意的**——平台只看自己的数据；确需跨租户读取用 `ClearTenantFilter()` / `CreateNoTenantQueryable()` |
| 平台插入报「平台上下文只能写入平台数据」 | 在平台里给行预置了租户标识；写某个租户的数据要先切入该租户 |
| 跨租户读取漏了一部分表 | 只清了 `IMultiTenantEntity` 过滤，严格隔离实体仍被收紧；改用 `ClearTenantFilter()` |

## 下一步

- [数据访问](./data)：全局查询过滤器
- [认证与授权](./authentication)：租户解析在管道里的位置
- [MultiTenancy 包](../packages/multitenancy)：完整 API
