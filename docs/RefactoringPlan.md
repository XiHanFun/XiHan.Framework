# XiHan.Framework 重构优化计划

> 版本：v1.0 | 日期：2026-04-26 | 状态：待实施
>
> 本计划聚焦 XiHan.Framework 底层框架中与 BasicApp 全栈重构直接相关的模块优化。
> 按优先级排序，每个任务标注前置依赖、影响范围和验证标准。

---

## 一、现状分析

### 1.1 模块清单（47 个模块）

框架已完成第一阶段核心包开发（Utils、Core、Serialization、ObjectMapping、Logging、Web.Core、Web.Api、Web.Docs），
第二至九阶段模块大部分处于接口定义 + 骨架实现阶段。

### 1.2 与 BasicApp 重构直接相关的模块

| 模块 | 当前状态 | 重构需求 |
|------|----------|----------|
| `XiHan.Framework.Data` (SqlSugar) | 实体基类已定义，全局过滤器已实现 | 补全值对象支持、优化过滤器一致性 |
| `XiHan.Framework.Domain` | 核心接口已定义 | 补全 ISoftDelete/IMultiTenant 接口链 |
| `XiHan.Framework.Application` | CrudApplicationServiceBase 已实现 | 补全 QueryService 基类、DTO 投影 |
| `XiHan.Framework.Web.Api` (DynamicApi) | 动态 API 生成已实现 | 验证约定一致性、补全文档 |
| `XiHan.Framework.Uow` | 骨架已实现 | 确保与 SqlSugar 事务集成 |
| `XiHan.Framework.MultiTenancy` | 抽象已定义 | 补全 TenantId=0 平台语义 |
| `XiHan.Framework.Authorization` | 接口已定义 | 补全 PermissionAuthorize 特性 |
| `XiHan.Framework.Caching` | 骨架已实现 | 确保缓存失效事件机制 |

### 1.3 已识别的关键问题

**代码审查确认的 Bug 级问题：**

1. **[BUG] `MultiTenantAggregateRootBase<TKey>.TenantId` 为 `long?`（可空）** — 与所有其他多租户基类（`long` 非空，0=平台）不一致。该基类未实现 `IMultiTenantEntity`，导致全局 `QueryFilter.AddTableFilter<IMultiTenantEntity>()` 不会命中，聚合根数据将跨租户泄露。
   - 文件：`XiHan.Framework.Domain/Aggregates/MultiTenantAggregateRootBase.cs:27`
2. **[BUG] `CrudApplicationServiceBase.DeleteAsync` 执行硬删除** — 基类使用 `IRepositoryBase`（非 `ISoftDeleteRepositoryBase`），`DeleteAsync` 直接物理删除。如果实体实现了 `ISoftDelete`，不会自动走软删除路径，除非子类覆写。
3. **[BUG] `BatchCrudApplicationServiceBase.BatchDeleteAsync` 绕过 AOP** — 手动设置 `IsDeleted`/`DeletedTime`，绕过了 `SqlSugarSoftDeleteRepository.SoftDeleteAsync()` 和 `DataExecuting` AOP，导致 `DeletedId`/`DeletedBy` 字段为空。
4. **[BUG] `SqlSugarAggregateRepository` 在无 UoW 上下文时抛异常** — `UnitOfWork` 属性在 `_unitOfWorkManager.Current == null` 时抛 `InvalidOperationException`，后台任务等无 `[UnitOfWork]` 场景下领域事件丢失或崩溃。

**设计级问题：**

5. **`AuditedAggregateRoot` 与 `AggregateRootBase` 并行存在** — 两套聚合根实现，`AuditedAggregateRoot` 使用原始 `ICollection<DomainEventRecord>`，`AggregateRootBase` 委托 `DomainEventsManagerBase`。SqlSugar 分支只用 `AggregateRootBase`，`AuditedAggregateRoot` 成为孤儿代码。
6. **`ISplitTableEntity` 多租户过滤未验证** — `SplitTable()` 调用绕过标准 queryable 构建，全局 `QueryFilter` 是否在分表查询中生效需要验证。
7. **实体基类继承链完整但缺少文档约束** — 开发者不清楚何时选择哪个基类
8. **值对象在 SqlSugar 中缺少标准化支持** — 无 IsJson 列映射约定
9. **DynamicApi 方法名前缀映射未文档化** — 开发者容易写出不符合约定的方法名
10. **Application 层缺少 QueryService 基类** — CQRS 读侧无标准化支持

---

## 二、重构任务清单

### 第 0 层：紧急 Bug 修复（阻塞 BasicApp 重构）

#### 任务 F-0.1：修复 MultiTenantAggregateRootBase TenantId 可空问题

- **严重级别**：P0 — 数据安全漏洞
- **目标**：将 `MultiTenantAggregateRootBase<TKey>.TenantId` 从 `long?` 改为 `long`，并实现 `IMultiTenantEntity`
- **涉及文件**：
  - `XiHan.Framework.Domain/Aggregates/MultiTenantAggregateRootBase.cs`
- **具体工作**：
  1. 将 `public virtual long? TenantId { get; set; }` 改为 `public virtual long TenantId { get; set; }`
  2. 确保类实现 `IMultiTenantEntity` 接口
  3. 验证 `SugarMultiTenantAggregateRoot<TKey>` 继承链正确传递
  4. 验证全局 `QueryFilter.AddTableFilter<IMultiTenantEntity>()` 能命中聚合根
- **前置依赖**：无
- **验证标准**：聚合根实体受租户过滤器保护，跨租户查询被正确拦截

#### 任务 F-0.2：修复 CrudApplicationServiceBase 硬删除问题

- **严重级别**：P0 — 数据丢失风险
- **目标**：CRUD 基类的 DeleteAsync 应自动检测 ISoftDelete 并走软删除路径
- **涉及文件**：
  - `XiHan.Framework.Application/Services/CrudApplicationServiceBase.cs`
- **具体工作**：
  1. 在 `DeleteAsync` 中检测 `TEntity` 是否实现 `ISoftDelete`
  2. 如果是，调用 `ISoftDeleteRepositoryBase.SoftDeleteAsync()` 而非 `IRepositoryBase.DeleteAsync()`
  3. 或者将 CRUD 基类的仓储依赖从 `IRepositoryBase` 升级为 `ISoftDeleteRepositoryBase`
  4. 同步修复 `BatchCrudApplicationServiceBase.BatchDeleteAsync`，不再手动设置字段，改为调用仓储软删除方法
- **前置依赖**：无
- **验证标准**：继承 CRUD 基类的服务对 ISoftDelete 实体执行软删除，审计字段正确填充

#### 任务 F-0.3：修复 SqlSugarAggregateRepository 无 UoW 崩溃问题

- **严重级别**：P1 — 运行时崩溃
- **目标**：在无 UoW 上下文时优雅降级，而非抛异常
- **涉及文件**：
  - `XiHan.Framework.Data/SqlSugar/Repositories/SqlSugarAggregateRepository.cs`
- **具体工作**：
  1. 将 `UnitOfWork` 属性的 `InvalidOperationException` 改为 null 检查
  2. 在 Add/Update/Delete 中，如果 `_unitOfWorkManager.Current == null`，直接发布领域事件（不走 outbox）或记录警告日志
  3. 确保后台任务场景下聚合根仓储可正常使用
- **前置依赖**：无
- **验证标准**：无 UoW 上下文时仓储操作不崩溃，领域事件不丢失

#### 任务 F-0.4：清理 AuditedAggregateRoot 孤儿代码

- **严重级别**：P2 — 代码质量
- **目标**：移除或统一 `AuditedAggregateRoot` 与 `AggregateRootBase` 的并行实现
- **涉及文件**：
  - `XiHan.Framework.Domain/Aggregates/AuditedAggregateRoot.cs`
  - `XiHan.Framework.Domain/Aggregates/AggregateRootBase.cs`
- **具体工作**：
  1. 确认 `AuditedAggregateRoot` 无外部引用
  2. 如无引用，标记为 `[Obsolete]` 或直接移除
  3. 如有引用，统一到 `AggregateRootBase` 实现
- **前置依赖**：无
- **验证标准**：只有一套聚合根基类实现

---

### 第 1 层：实体基类与接口补全

#### 任务 F-1.1：实体基类选择规范文档化

- **目标**：在框架代码中通过 XML 注释和 README 明确每个基类的适用场景
- **涉及文件**：
  - `XiHan.Framework.Data/SqlSugar/Entities/SugarMultiTenantEntity.cs` 及同级所有基类
  - `XiHan.Framework.Data/SqlSugar/Aggregates/SugarMultiTenantAggregateRoot.cs`
- **具体工作**：
  1. 为每个基类添加 `<remarks>` 标注适用场景（关联表、日志、普通业务、聚合根）
  2. 添加聚合根判定标准注释（不变量、事务边界、领域事件三条件）
  3. 在 `SugarMultiTenantAggregateRoot` 上标注：不满足三条件的实体禁止继承
- **前置依赖**：无
- **验证标准**：`dotnet build` 通过；XML 文档生成无警告

#### 任务 F-1.2：ISoftDelete 接口链验证与补全

- **目标**：确保 ISoftDelete → IDeletionEntity → IDeletionEntity<TKey> 接口链完整且一致
- **涉及文件**：
  - `XiHan.Framework.Domain/Entities/` 下的软删除相关接口
  - `XiHan.Framework.Data/SqlSugar/` 下的全局过滤器注册
- **具体工作**：
  1. 验证 `ISoftDelete` 接口定义包含 `bool IsDeleted { get; set; }`
  2. 验证 `IDeletionEntity` 扩展包含 `DateTimeOffset? DeletedTime`、`long? DeletedId`、`string? DeletedBy`
  3. 确认 SqlSugar `QueryFilter<ISoftDelete>()` 自动附加 `WHERE IsDeleted = false`
  4. 确认 `EnableAutoUpdateQueryFilter = true` 和 `EnableAutoDeleteQueryFilter = true` 已配置
  5. 如缺失，补全配置
- **前置依赖**：无
- **验证标准**：全局过滤器在 UPDATE/DELETE 场景下也生效

#### 任务 F-1.3：IMultiTenant 接口与 TenantId=0 平台语义

- **目标**：在框架层明确 TenantId=0 为平台级数据的语义
- **涉及文件**：
  - `XiHan.Framework.MultiTenancy/` 或 `XiHan.Framework.MultiTenancy.Abstractions/`
  - `XiHan.Framework.Data/SqlSugar/` 全局租户过滤器
- **具体工作**：
  1. 在 `IMultiTenant` 接口文档中明确：`TenantId = 0` 表示平台级全局数据，业务租户从 1 开始
  2. 确认租户过滤器查询逻辑为 `WHERE TenantId IN (0, {currentTenantId})`（合并全局 + 当前租户）
  3. 添加 `PlatformTenantId` 常量 = 0
  4. 确认 `SysTenant` 等平台元数据实体不被租户过滤器误隐藏
  5. 验证 `IgnoreMultiTenancyAttribute` 特性可用于平台管理功能
- **前置依赖**：无
- **验证标准**：平台级数据（TenantId=0）在任何租户上下文下均可查询到

#### 任务 F-1.4：值对象 SqlSugar 映射支持

- **目标**：为 SqlSugar 提供标准化的值对象列映射方案
- **涉及文件**：
  - `XiHan.Framework.Data/SqlSugar/` 新增值对象映射约定
- **具体工作**：
  1. 定义值对象 JSON 列映射约定：`[SugarColumn(IsJson = true)]`
  2. 提供 `record` 类型值对象的序列化/反序列化支持验证
  3. 文档化两种映射方式：JSON 列（复杂值对象）vs 展开字段（简单值对象）
  4. 示例值对象：`ClientInfo`、`EffectivePeriod`、`DeviceInfo`
- **前置依赖**：F-1.1
- **验证标准**：值对象可正确序列化到 JSON 列并反序列化回来

---

### 第 2 层：全局过滤器一致性

#### 任务 F-2.1：租户过滤器与软删除过滤器交叉验证

- **目标**：确保两个过滤器在所有 CRUD 场景下协同工作
- **涉及文件**：
  - `XiHan.Framework.Data/SqlSugar/` 过滤器注册代码
  - `XiHan.Framework.Data/SqlSugar/` AOP 数据执行拦截
- **具体工作**：
  1. 编写验证场景矩阵：
     - SELECT：`WHERE IsDeleted = false AND TenantId IN (0, {tid})`
     - UPDATE：同上条件自动附加
     - DELETE（逻辑删除）：同上条件自动附加
     - INSERT：TenantId 由 AOP 自动填充
  2. 验证 `CreateNoTenantQueryable()` 正确清除租户过滤器
  3. 验证 `CreateWithDeletedQueryable()` 正确清除软删除过滤器
  4. 确认两个逃逸开关可独立使用
- **前置依赖**：F-1.2, F-1.3
- **验证标准**：所有场景矩阵通过

#### 任务 F-2.2：AOP 数据执行拦截补全

- **目标**：确保 `Aop.DataExecuting` 正确自动填充审计字段
- **涉及文件**：
  - `XiHan.Framework.Data/SqlSugar/` AOP 配置
- **具体工作**：
  1. 验证自动填充字段清单：
     - INSERT：`BasicId`（雪花算法）、`TenantId`、`CreatedTime`、`CreatedId`、`CreatedBy`
     - UPDATE：`ModifiedTime`、`ModifiedId`、`ModifiedBy`
     - 软删除：`IsDeleted=true`、`DeletedTime`、`DeletedId`、`DeletedBy`
  2. 确认 `RowVersion` 乐观锁自动递增
  3. 确认 `TenantId` 写入时由框架注入，禁止业务代码手动赋值
- **前置依赖**：F-1.1
- **验证标准**：审计字段在所有写操作中正确填充

---

### 第 3 层：仓储与工作单元

#### 任务 F-3.1：仓储基类接口规范化

- **目标**：确保仓储基类接口清晰、方法命名一致
- **涉及文件**：
  - `XiHan.Framework.Domain/Repositories/` 仓储接口
  - `XiHan.Framework.Data/SqlSugar/Repositories/` 仓储实现基类
- **具体工作**：
  1. 验证接口层次：
     - `IRepository<TEntity, TKey>` — 基础 CRUD
     - `IAggregateRootRepository<TEntity, TKey>` — 聚合根仓储（含领域事件发布）
     - `ISplitTableRepository<TEntity, TKey>` — 分表仓储
  2. 确认方法命名规范：`GetByXxxAsync`、`FindByXxxAsync`、`ExistsXxxAsync`
  3. 确认 `RestoreAsync()` 方法存在于支持软删除的仓储中
  4. 验证 `CreateNoTenantQueryable()` 和 `CreateWithDeletedQueryable()` 在仓储基类中可用
- **前置依赖**：F-2.1
- **验证标准**：仓储接口与实现一致，编译通过

#### 任务 F-3.2：工作单元与领域事件集成

- **目标**：确保 UoW 提交时自动发布领域事件
- **涉及文件**：
  - `XiHan.Framework.Uow/` 工作单元实现
  - `XiHan.Framework.Domain/Events/` 领域事件基础设施
- **具体工作**：
  1. 验证 `IUnitOfWorkManager` 在 `CommitAsync()` 时收集聚合根的领域事件
  2. 确认事件在事务提交后发布（outbox 模式或直接发布）
  3. 确认事务回滚时事件不发布
  4. 验证嵌套 UoW 场景下事件只在最外层提交时发布
- **前置依赖**：F-3.1
- **验证标准**：领域事件在事务提交后正确发布

---

### 第 4 层：应用服务基础设施

#### 任务 F-4.1：CrudApplicationServiceBase 验证

- **目标**：确认 CRUD 应用服务基类满足 BasicApp 需求
- **涉及文件**：
  - `XiHan.Framework.Application/` 或 `XiHan.Framework.Application.Contracts/`
- **具体工作**：
  1. 验证泛型签名：`CrudApplicationServiceBase<TEntity, TDto, TKey, TCreateDto, TUpdateDto, TPageRequestDto>`
  2. 确认内置方法：`GetAsync`、`GetPageAsync`、`CreateAsync`、`UpdateAsync`、`DeleteAsync`
  3. 确认方法命名符合 DynamicApi 前缀映射规则
  4. 验证 DTO 映射集成（Mapster）
  5. 确认权限特性可标注在方法上
- **前置依赖**：F-3.2
- **验证标准**：继承 CrudApplicationServiceBase 的服务可通过 DynamicApi 正确暴露

#### 任务 F-4.2：QueryService 基类补全

- **目标**：为 CQRS 读侧提供标准化的 QueryService 基类
- **涉及文件**：
  - `XiHan.Framework.Application/` 新增 QueryServiceBase
- **具体工作**：
  1. 定义 `QueryServiceBase<TEntity, TDto, TKey>` 基类
  2. 内置分页查询、条件过滤、排序支持
  3. 支持缓存集成（可选）
  4. 确认 QueryService 不参与事务（只读）
- **前置依赖**：F-4.1
- **验证标准**：QueryService 可独立于 AppService 使用

---

### 第 5 层：DynamicApi 与授权

#### 任务 F-5.1：DynamicApi 约定文档化与验证

- **目标**：确保 DynamicApi 方法名前缀映射规则清晰且稳定
- **涉及文件**：
  - `XiHan.Framework.Web.Api/DynamicApi/` 约定类
- **具体工作**：
  1. 文档化 HTTP 方法映射规则：
     - GET: `Get*`, `Find*`, `Query*`, `Search*`
     - POST: `Create*`, `Add*`, `Insert*`
     - PUT: `Update*`, `Edit*`, `Modify*`
     - DELETE: `Delete*`, `Remove*`
  2. 文档化路由生成规则：`api/{ControllerName}/{ActionName}`
  3. 验证 `[DynamicApi(Group, GroupName)]` 特性正确工作
  4. 确认 `Async` 后缀在路由中被正确去除
- **前置依赖**：无
- **验证标准**：DynamicApi 生成的路由与文档一致

#### 任务 F-5.2：PermissionAuthorize 特性实现

- **目标**：提供声明式权限授权特性
- **涉及文件**：
  - `XiHan.Framework.Authorization/` 授权特性
- **具体工作**：
  1. 实现 `[PermissionAuthorize("module:resource:action")]` 特性
  2. 集成 ASP.NET Core 授权管道
  3. 支持多权限码（AND/OR 逻辑）
  4. 支持 `[AbacAuthorize("policy.id")]` 特性（ABAC 策略）
- **前置依赖**：F-5.1
- **验证标准**：权限特性在 DynamicApi 生成的端点上正确生效

---

### 第 6 层：缓存与事件

#### 任务 F-6.1：缓存失效事件机制

- **目标**：确保领域事件可触发缓存失效
- **涉及文件**：
  - `XiHan.Framework.Caching/` 缓存抽象
  - `XiHan.Framework.EventBus/` 事件总线
- **具体工作**：
  1. 定义 `ICacheInvalidationHandler<TEvent>` 接口
  2. 确认领域事件发布后可自动触发缓存失效
  3. 支持按 key pattern 批量失效
  4. 支持多级缓存（本地 + Redis）同步失效
- **前置依赖**：F-3.2
- **验证标准**：实体变更后相关缓存自动失效

---

## 三、实施优先级与时间线

| 优先级 | 任务编号 | 任务名称 | 预估工时 | 阻塞关系 |
|--------|----------|----------|----------|----------|
| P0-紧急 | F-0.1 | 修复 MultiTenantAggregateRootBase TenantId 可空 | 2h | 无 |
| P0-紧急 | F-0.2 | 修复 CrudApplicationServiceBase 硬删除问题 | 4h | 无 |
| P0-紧急 | F-0.3 | 修复 SqlSugarAggregateRepository 无 UoW 崩溃 | 3h | 无 |
| P0 | F-0.4 | 清理 AuditedAggregateRoot 孤儿代码 | 2h | 无 |
| P0 | F-1.1 | 实体基类选择规范文档化 | 2h | 无 |
| P0 | F-1.2 | ISoftDelete 接口链验证与补全 | 3h | 无 |
| P0 | F-1.3 | IMultiTenant 接口与 TenantId=0 平台语义 | 3h | F-0.1 |
| P0 | F-1.4 | 值对象 SqlSugar 映射支持 | 4h | F-1.1 |
| P1 | F-2.1 | 租户过滤器与软删除过滤器交叉验证 | 4h | F-1.2, F-1.3 |
| P1 | F-2.2 | AOP 数据执行拦截补全 | 3h | F-1.1 |
| P1 | F-3.1 | 仓储基类接口规范化 | 3h | F-2.1 |
| P1 | F-3.2 | 工作单元与领域事件集成 | 4h | F-3.1, F-0.3 |
| P2 | F-4.1 | CrudApplicationServiceBase 验证 | 2h | F-3.2, F-0.2 |
| P2 | F-4.2 | QueryService 基类补全 | 4h | F-4.1 |
| P2 | F-5.1 | DynamicApi 约定文档化与验证 | 2h | 无 |
| P2 | F-5.2 | PermissionAuthorize 特性实现 | 6h | F-5.1 |
| P3 | F-6.1 | 缓存失效事件机制 | 4h | F-3.2 |

---

## 四、验证门禁

每个任务完成后必须通过：

1. `dotnet build` 整个 Framework 解决方案无错误
2. 修改的模块 XML 文档生成无警告
3. 如涉及过滤器/AOP 变更，需编写或运行集成测试验证
4. 代码扫描：
   - `rg "TenantId IS NULL"` — 不应出现
   - `rg "PlatformTenantId = 1"` — 不应出现（应为 0）
   - `rg "class.*Controller"` — Framework 中不应有 Controller

---

## 五、风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| SqlSugar 全局过滤器在 UPDATE/DELETE 场景下不生效 | 数据泄露/越权 | F-2.1 中显式验证，必要时在 AOP 层补充 |
| 值对象 JSON 序列化与 SqlSugar 不兼容 | 数据丢失 | F-1.4 中编写序列化往返测试 |
| DynamicApi 方法名前缀映射不稳定 | 接口路由错误 | F-5.1 中编写约定测试 |
| 领域事件在事务回滚后仍被发布 | 数据不一致 | F-3.2 中验证回滚场景 |

---

## 六、与 BasicApp 重构的衔接

本计划完成后，BasicApp 重构可安全依赖以下框架能力：

- 实体基类选择有明确规范 → BasicApp 实体降级/升级有据可依
- 全局过滤器在所有 CRUD 场景下一致 → 多租户隔离和软删除可信赖
- 值对象映射标准化 → BasicApp 值对象可直接使用
- 仓储基类规范化 → BasicApp 仓储实现有标准模板
- DynamicApi 约定明确 → BasicApp AppService 命名有规则可循
- PermissionAuthorize 特性可用 → BasicApp 权限控制有声明式支持
- 缓存失效事件机制 → BasicApp 缓存策略有框架支撑
