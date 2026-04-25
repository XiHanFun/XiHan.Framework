# XiHan.Framework 重构优化计划

> 计划日期：2026-04-26  
> 适用范围：`XiHan.Framework/framework/src` 通用底层框架  
> 执行原则：只沉淀通用基础设施能力，不下沉 `XiHan.BasicApp.Saas` 的业务概念。

## 1. 目标与边界

本计划服务于 `XiHan.BasicApp` 的全面重构，但 `XiHan.Framework` 只解决可复用的底层问题：

- 实体基类、审计字段、乐观锁、软删除、多租户字段语义一致。
- SqlSugar 全局过滤器覆盖查询、更新、删除，并提供可审计的显式逃逸能力。
- 仓储、工作单元、领域事件、分表仓储能力稳定，避免上层重复写租户和软删除条件。
- DynamicApi 路由、HTTP 方法推断、参数绑定、OpenAPI 元数据稳定，应用侧禁止新增 Controller。
- 权限、ABAC、缓存、设置、审计、对象存储等基础能力保持通用，不绑定角色、菜单、租户套餐、FLS 等 SaaS 业务模型。

不在 Framework 中实现：

- 角色、菜单、权限码、租户成员、套餐版本、字段脱敏规则、SaaS 种子模板。
- 任何 `Sys*` 实体、DTO、AppService、Seeder 的业务规则。
- 前端 API 或页面逻辑。

## 2. 当前代码事实

初始扫描基于 2026-04-26 本地工作区：

- Framework 模块位于 `framework/src`，包含 Data、Domain、Application、Web.Api、MultiTenancy、Authorization、Caching、Uow、EventBus、ObjectStorage 等模块。
- `XiHan.Framework.Data` 已注册 SqlSugar 常规仓储、聚合仓储、软删除仓储、分表仓储和数据库初始化服务。
- `XiHanDataServiceCollectionExtensions` 已启用：
  - `ISoftDelete` 全局过滤：`IsDeleted = false`。
  - `IMultiTenantEntity` 全局过滤：当前租户 + `TenantId = 0` 全局数据。
  - `EnableAutoUpdateQueryFilter` / `EnableAutoDeleteQueryFilter` 默认开启。
  - `DataExecuting` AOP 自动填充雪花 ID、创建/修改/删除审计字段和 `TenantId`。
- `IMultiTenantEntity.TenantId` 已是非空 `long`，约定 `0` 为平台租户。
- `SqlSugarReadOnlyRepository` 已提供 `CreateNoTenantQueryable()` 与 `CreateWithDeletedQueryable()`，但需要纳入权限、审计和使用规范。
- `CrudApplicationServiceBase` 已提供基础 CRUD、分页、DTO 映射和校验能力。
- `DefaultDynamicApiConvention` 已按服务后缀、方法名前缀、参数名推断 Controller、Action、HTTP Method 和路由参数。
- `PermissionAuthorizeAttribute`、`AbacAuthorizeAttribute`、ABAC evaluator、缓存多租户 key 归一化等能力已存在，需要补齐一致性验证。

## 3. 总体执行顺序

Framework 重构必须先于 BasicApp 应用层重构完成关键契约确认。

| 阶段 | 主题 | 主要模块 | 对 BasicApp 的影响 |
|---|---|---|---|
| F0 | 基线与规则冻结 | 全仓 | 确认不可破坏的公开契约 |
| F1 | 实体与审计基线 | Domain, Data | BasicAppEntity 继承链稳定 |
| F2 | 多租户与软删除过滤 | Data, MultiTenancy | 租户隔离、平台记录合并查询稳定 |
| F3 | 仓储、UoW、领域事件 | Data, Domain, Uow, EventBus | 聚合根、仓储、事件发布稳定 |
| F4 | DynamicApi | Application, Web.Api, Web.Docs | AppService 自动暴露接口稳定 |
| F5 | 授权、ABAC、缓存、设置 | Authorization, Caching, Settings | RBAC/ABAC/FLS 业务侧可接入 |
| F6 | 数据初始化、审计、可观测 | Data, Logging, Observability | Seeder、审计日志、排障能力稳定 |
| F7 | 验证门禁与文档 | tests, docs | 后续应用重构有固定质量门槛 |

## 4. F0：基线与规则冻结

### 4.1 任务

- 列出 `framework/src` 所有项目和项目依赖，形成模块依赖图。
- 记录公开接口清单：实体基类、仓储接口、UoW 接口、DynamicApi Attribute/Options、授权 Attribute、缓存接口。
- 建立架构扫描脚本或固定命令：
  - 检查是否存在业务实体或 BasicApp/SaaS 命名进入 Framework。
  - 检查 Controller 生成逻辑只位于 DynamicApi 基础设施。
  - 检查多租户字段继续使用非空 `long TenantId`。
- 明确兼容策略：
  - Framework 公共接口优先保持源码兼容。
  - 确需破坏兼容时必须记录迁移步骤，并同步 BasicApp 计划。

### 4.2 验收

- `dotnet build E:\Repository\XiHanFun\XiHan.Framework\framework\XiHan.Framework.slnx`
- `rg -n "BasicApp|Saas|SysUser|SysRole|SysTenant" framework/src -g "*.cs"` 不能出现业务下沉。
- 形成可复用的扫描命令清单，后续每阶段执行。

## 5. F1：实体、审计与值对象基础

### 5.1 实体基类

当前基础：

- `EntityBase<TKey>` 提供 `BasicId` 和 `RowVersion`。
- `SugarMultiTenantEntity<TKey>` / `SugarMultiTenantAggregateRoot<TKey>` 提供非空 `TenantId`。
- 多租户审计实体链已经存在。

优化项：

- 确认 `RowVersion` 是否由 SqlSugar 乐观锁正确识别；如未识别，补充统一 Attribute 或 AOP 策略。
- 确认 `BasicId` 的 protected setter 是否满足 SqlSugar、Mapster 和反序列化场景，避免 DTO 映射修改主键。
- 明确完整继承链文档：
  - `SugarMultiTenantEntity<TKey>`
  - `SugarMultiTenantCreationEntity<TKey>`
  - `SugarMultiTenantModificationEntity<TKey>`
  - `SugarMultiTenantDeletionEntity<TKey>`
  - `SugarMultiTenantFullAuditedEntity<TKey>`
  - `SugarMultiTenantAggregateRoot<TKey>`
- 为聚合根增加使用约束文档：只有存在不变量、事务一致性边界、领域事件的类型才继承 AggregateRoot。

### 5.2 审计字段

当前 `EntityAuditExtensions` 在 `DataExecuting` 中填充创建、修改、删除审计字段和 `TenantId`。

优化项：

- 将审计时间统一为 UTC 语义，评估 `DateTimeOffset.UtcNow` 替换当前 `Now` 的影响。
- 明确无租户上下文插入时 `TenantId` 的默认行为：默认保持 `0`，不可写入 null。
- 对 `CreatedTime`、`ModifiedTime`、`DeletedTime` 的覆盖策略写入测试：
  - 创建时默认值才填充。
  - 修改时覆盖修改字段。
  - 软删除时仅 `IsDeleted = true` 后填充删除字段。
- 对敏感字段的审计 Diff 增加脱敏扩展点，避免审计日志保存密码、Token、Secret、连接串原文。

### 5.3 值对象支持

Framework 不定义 SaaS 业务值对象，但需要提供通用承载能力：

- 明确 JSON 列、可空 record、集合值对象的 SqlSugar 映射规范。
- 提供值对象序列化扩展点，统一 System.Text.Json / Newtonsoft.Json 的行为。
- 保留业务值对象定义在应用侧，例如 `ClientInfo`、`EffectivePeriod`、`BusinessReference`、`DeviceInfo`。

### 5.4 验收

- 实体基类文档与代码一致。
- RowVersion 乐观锁行为有最小测试或代码事实说明。
- 审计字段 UTC 策略明确，并有迁移说明。
- 不新增任何 SaaS 命名空间或业务常量。

## 6. F2：多租户与软删除过滤

### 6.1 多租户语义

目标语义：

- `TenantId = 0` 代表平台级/全局记录。
- 业务租户从 `1` 开始。
- 普通租户查询自动合并 `TenantId IN (0, currentTenantId)`。
- 插入时租户由 AOP 根据 `ICurrentTenant.Id` 注入；无租户上下文保持 `0`。
- 跨租户访问必须显式调用逃逸 API，并由应用侧做平台管理员权限校验和审计。

优化项：

- 明确当前无租户上下文时的查询策略。当前实现无租户上下文不过滤，需要评估改为“仅平台数据”或保留“显式运维模式”。
- 对 `ICurrentTenant.Change(null)`、`Change(0)`、`Change(tenantId)` 建立测试矩阵。
- 为 `CreateNoTenantQueryable()` 的使用建立审计约束：调用者必须是平台级服务或具备显式权限。
- 为分页查询中的 `QueryBehavior.IgnoreTenant` 增加权限保护，避免前端传参绕过租户过滤。

### 6.2 软删除

优化项：

- 确认 `ISoftDelete` 过滤器覆盖普通查询、分页、更新、删除。
- 确认 `CreateWithDeletedQueryable()` 只用于审计、恢复、清理任务。
- 确认 `RestoreAsync()` 类能力在仓储层统一实现，应用层不直接 update `IsDeleted`。
- 补充硬删除规范：只有系统清理任务可使用，必须绕过普通业务服务。

### 6.3 验收

- 查询、更新、删除均受租户和软删除过滤影响。
- 明确 `TenantId = 0` 的平台数据合并逻辑。
- 逃逸 API 有使用文档和扫描点。
- 扫描命令：
  - `rg -n "TenantId\s*==\s*null|TenantId\s+IS\s+NULL|TenantId\s*=\s*null" framework/src -g "*.cs"`
  - `rg -n "ClearFilter<IMultiTenantEntity>|CreateNoTenantQueryable|IgnoreTenant" framework/src -g "*.cs"`

## 7. F3：仓储、工作单元与领域事件

### 7.1 仓储

优化项：

- 保持仓储只做持久化、过滤边界和审计开关，不承载页面投影。
- 常规实体使用 `IRepositoryBase<TEntity,TKey>`。
- 聚合根使用 `IAggregateRootRepository<TAggregateRoot,TKey>`。
- 分表日志实体使用 `ISplitRepositoryBase<TEntity>`，禁止误用常规仓储。
- 为批量更新、批量删除统一租户安全预读，避免 `Updateable(entity)` 绕过 QueryFilter。
- 为 `CreateNoTenantQueryable()`、`CreateWithDeletedQueryable()` 增加更清晰的 protected API 文档和示例。

### 7.2 工作单元

优化项：

- 明确 AppService 默认事务边界。
- 确认嵌套 UoW、异常回滚、异步上下文传播行为。
- 确认 SqlSugar 多连接、多租户连接下的 UoW 行为。
- 将仓储内 `UseTranAsync` 与框架 UoW 的关系写清楚，避免双事务。

### 7.3 领域事件

优化项：

- 确认 AggregateRoot 的本地/分布式事件在仓储保存后进入 UoW。
- 事件应在事务提交后发布，失败时不发布。
- 明确事件清理时机，避免重复发布。
- 为 BasicApp 后续的角色权限缓存失效、Session/Token 级联撤销提供通用事件管道。

### 7.4 验收

- 仓储行为有单元测试或最小集成测试。
- UoW 提交/回滚/嵌套行为明确。
- 聚合根事件发布有最小用例。
- 分表仓储和常规仓储边界文档化。

## 8. F4：DynamicApi 稳定化

### 8.1 路由和命名

目标：

- 应用服务通过 `[DynamicApi]` 暴露 HTTP 接口。
- ControllerName 由类名去掉 `AppService` / `ApplicationService` / `Service` 后缀。
- ActionName 由方法名去掉 HTTP 前缀和 `Async` 后缀。
- HTTP 方法通过方法名前缀或显式特性推断。

优化项：

- 固定方法前缀矩阵：`Get/Find/Query/Search`、`Create/Add/Insert`、`Update/Edit/Modify`、`Delete/Remove`。
- 增加路由冲突检测，启动期发现重复路由直接报错。
- 增加参数绑定测试：
  - GET/DELETE 的 id 参数进入 route。
  - POST/PUT/PATCH 复杂对象进入 body。
  - 显式 `[FromRoute]`、`[FromQuery]`、`[FromBody]` 优先。
- OpenAPI 分组、版本、标签从 DynamicApi Attribute 稳定生成。

### 8.2 禁止手写 Controller 的边界

Framework 可以生成运行时 Controller 类型，但应用层不得新增业务 Controller。

验收扫描：

- `rg -n "class .*Controller" E:\Repository\XiHanFun\XiHan.BasicApp\backend\src -g "*.cs"`
- 允许出现 Framework DynamicApi 基础设施 Controller Factory，不允许业务模块出现业务 Controller。

### 8.3 验收

- DynamicApi 单元测试覆盖命名、HTTP 方法、参数绑定、路由冲突。
- OpenAPI 文档能反映 Group、GroupName、Version。
- BasicApp 应用服务命名不需要手写路由即可稳定暴露。

## 9. F5：授权、ABAC、缓存与设置

### 9.1 授权

Framework 只提供机制：

- `[PermissionAuthorize]`
- `[AbacAuthorize]`
- `IPermissionChecker`
- `IAbacEvaluator`
- 授权策略注册和 ASP.NET Core 接入

优化项：

- 明确权限判定结果模型：允许、拒绝、未授权、缺少上下文。
- ABAC evaluator 支持通用操作符：`eq`、`ne`、`in`、`notIn`、`gt`、`gte`、`lt`、`lte`、`all`、`any`、`not`。
- Framework 不内置 BasicApp 权限码，不内置角色名。
- 授权失败日志只记录必要上下文，不记录敏感请求体。

### 9.2 缓存

优化项：

- 多租户缓存 key 默认包含租户上下文。
- `[IgnoreMultiTenancy]` 仅用于全局定义、平台缓存、分布式锁等明确场景。
- 支持按模式失效缓存，但必须限制命名空间，避免误删其他租户缓存。
- 记录缓存 key 规范，供 BasicApp RBAC、FLS、菜单缓存使用。

### 9.3 设置

优化项：

- Settings 模块提供通用分层设置接口。
- 业务设置项、默认值、种子和页面管理由 BasicApp 实现。
- 设置缓存需要跟随租户上下文隔离。

### 9.4 验收

- 授权、ABAC、缓存、设置模块无 BasicApp/SaaS 引用。
- 缓存 key 有租户隔离测试。
- ABAC evaluator 有操作符测试。

## 10. F6：初始化、审计与可观测

### 10.1 数据初始化

优化项：

- `IDbInitializer` 只提供通用建库、建表、Seeder 编排。
- 业务 Seeder 顺序、业务默认值由 BasicApp 模块定义。
- Seeder 必须可重复执行，不依赖硬编码自增 ID。

### 10.2 审计日志

优化项：

- DiffLog AOP 与 `IEntityAuditLogWriter` 的边界文档化。
- 增加敏感字段脱敏接口。
- 记录跨租户操作上下文：执行人、源租户、目标租户、TraceId、操作类型。
- SQL 日志避免输出密码、Token、Secret、连接串。

### 10.3 可观测

优化项：

- TraceId 注入保持通用，业务实体可实现 `ITraceableEntity`。
- 慢 SQL 阈值、异常 SQL 日志可配置。
- 为 UoW、DynamicApi、授权失败、缓存击穿提供结构化日志点。

### 10.4 验收

- 审计日志不保存敏感明文。
- 跨租户逃逸操作可被追踪。
- 慢 SQL、异常 SQL 日志可配置且默认安全。

## 11. F7：质量门禁

每次 Framework 阶段完成后执行：

```powershell
dotnet build E:\Repository\XiHanFun\XiHan.Framework\framework\XiHan.Framework.slnx
rg -n "BasicApp|Saas|SysUser|SysRole|SysTenant" E:\Repository\XiHanFun\XiHan.Framework\framework\src -g "*.cs"
rg -n "TenantId\s*==\s*null|TenantId\s+IS\s+NULL|TenantId\s*=\s*null" E:\Repository\XiHanFun\XiHan.Framework\framework\src -g "*.cs"
rg -n "class .*Controller" E:\Repository\XiHanFun\XiHan.Framework\framework\src -g "*.cs"
```

质量要求：

- 编译通过。
- 无业务概念下沉。
- TenantId 不使用 null 语义。
- DynamicApi 基础设施之外不新增业务 Controller。
- 架构决策变更同步更新本文档或新增 ADR。

## 12. 与 BasicApp 的交付协作

Framework 阶段对 BasicApp 的阻塞关系：

| Framework 输出 | BasicApp 可继续的阶段 |
|---|---|
| 实体基类、审计、TenantId 语义稳定 | BasicApp.Core 和 SaaS 实体基类重构 |
| QueryFilter、逃逸 API、软删除恢复稳定 | 仓储、授权、跨租户管理功能重构 |
| UoW、领域事件稳定 | 角色权限缓存失效、Session/Token 撤销 |
| DynamicApi 约定稳定 | AppService、前端 API 路由对齐 |
| 授权/ABAC/缓存机制稳定 | RBAC、ABAC、FLS、菜单权限实现 |

## 13. 风险与回滚

- 多租户过滤策略变更风险最高。必须先补测试，再修改默认行为。
- 审计时间从本地时间改 UTC 可能影响历史数据展示，需要 BasicApp 前端按时区格式化。
- DynamicApi 路由变更会直接影响前端 API，必须在 BasicApp API 层同步。
- 仓储批量更新/删除增加预读会带来性能变化，需要对大批量操作给出批处理方案。
- 任何公共接口签名变更都要记录迁移步骤和受影响项目。

回滚策略：

- 每个阶段保持小步提交。
- Framework 公共契约变更必须有单独提交，便于回退。
- BasicApp 依赖 Framework 新能力前，先确认 Framework 阶段门禁通过。

## 14. 阶段执行记录

### 2026-04-26 F0 基线与规则冻结

本阶段只做基线验证与文档更新，不修改 Framework 业务代码。

执行结果：

- `dotnet --info`：当前 .NET SDK 为 `10.0.202`，Host 为 `10.0.7`，Framework 仓库未发现 `global.json`。
- `dotnet build E:\Repository\XiHanFun\XiHan.Framework\framework\XiHan.Framework.slnx`：失败。Restore 显示项目均最新，随后多个项目写入 `obj\Debug\net10.0\*.AssemblyInfoInputs.cache` 被拒绝，错误为 `MSB3491 Access to the path is denied`。该失败属于本地构建输出目录权限/锁定问题，非本阶段代码变更导致。
- `rg -n "\bBasicApp|\bSaas|\bSysUser|\bSysRole|\bSysTenant" framework/src -g "*.cs"`：0 个匹配，未发现 BasicApp/SaaS 业务概念下沉到 Framework。

协作状态：

- 本阶段检测到 `framework/src/XiHan.Framework.Domain/Aggregates/MultiTenantAggregateRootBase.cs` 存在外部未提交修改，内容涉及 `TenantId` 从 `long?` 改为 `long` 并实现 `IMultiTenantEntity`。该文件不是本阶段修改范围，不纳入本阶段提交。
- 后续进入 F1/F2 前，需要确认该外部修改是否保留，并补充 Framework 编译门禁。
