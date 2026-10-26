# ADR-0027 Security.Database：授予进数据库，全局授予以空字符串租户列落库，逐项结果的批量授予 / 撤销 API

## 状态

已采纳。

## 日期

2026-10-27

## 背景

RFC-0008 定义了 RBAC 授予的契约 `IPermissionGrantStore`（主体 + 租户 → 有效权限码集合），但只有 `Security.InMemory` 一个实现：授予从配置装载、运行时的增删只在进程内。BasicApp 6.0 的 RBAC 授予在数据库里（角色 / 用户的权限分配是业务数据，随租户管理界面增删），应用要么自己实现 `IPermissionGrantStore` 绕过框架，要么框架提供数据库 Provider。用户 2026-10-27 决定在框架里实现，与 `Localization.Database`、`Scheduling.Database`、`Workflow.Database` 同一条线：抽象包定义契约，数据库 Provider 用 `persistence.relational@1` 的仓储读写，不绑定具体数据库。

授予被 Authorize 阶段查询，与其他数据库 Provider 不同的是它处在管道中段、每次调用都可能触发，因此如何读库、如何缓存、全局授予如何落库都要定下来。

## 备选

1. **授予存储经 `App.InvokeAsync` 再走一遍管道读库**（`Scheduling.Database` / `Workflow.Database` 的做法）：仓储只在 Transaction 阶段内可用，嵌套一次调用最省事。但授予是在 Authorize 阶段被查询的，嵌套调用会再次经过 Tenant / Authorize 阶段——`XiHan:MultiTenancy:RequireTenant=true` 时嵌套调用没有租户就失败；而且要求应用处于服务状态。
2. **直接在新作用域开工作单元读仓储**（`OutboxDispatcher.RunOnceAsync` 的做法）：`CreateAsyncScope` → `IUnitOfWork.BeginAsync` → 仓储 → `CommitAsync`，不经管道，启动期也能读。
3. **全局授予的 `TenantId` 列可空**：与契约 `PermissionGrant.TenantId == null` 一一对应，但 `(SubjectKind, SubjectId, Permission, TenantId)` 的唯一索引对 NULL 的语义各数据库不一致（PostgreSQL / SQLite 视 NULL 互不相等，SQL Server 只允许一个 NULL），同一全局授予可能重复落库。
4. **全局授予以空字符串落库**：列非空，唯一索引在各数据库上都成立；在 API 边界换算（`PermissionGrantRecord.ToGrant()` 空字符串 → `null`）。
5. **授予缓存放在 `PermissionChecker` 里**：所有 Provider 共享，但要改抽象包的契约（检查器要知道失效时机），内存实现不需要缓存。
6. **授予缓存放在数据库存储里**：契约不变，`IPermissionGrantStore` 的语义仍是"取有效授予"，缓存与失效是实现细节；写入 API 与存储在同一个包里，写入后可以直接失效。

## 决策

选 2、4、6。新包 `XiHan.Framework.Security.Database`（Ring 3，`role: provider`，清单 `xihan.security.database`）：

- `DatabaseSecurityFeature` 只提供 `security.authorization@1`，依赖 `kernel.hosting` 与 `persistence.relational@1`，贡献 Authorize 阶段的 `AuthorizationMiddleware`；认证仍由 InMemory（ApiKey）/ Jwt / OpenIdConnect 提供。不声明健康项。
- `PermissionGrantRecord`（`IEntity<Guid>`、`IHasCreationTime`，不实现 `IMultiTenant`，不软删除）：`SubjectKind` / `SubjectId` / `Permission` / `TenantId`，全局授予的 `TenantId` 为空字符串；应用在 `(SubjectKind, SubjectId, Permission, TenantId)` 上建唯一索引。
- `DatabasePermissionGrantStore`（单例）：按主体与租户一次查询 `SubjectKind = ? AND SubjectId = ? AND (TenantId = '' OR TenantId = ?)`，在新作用域的只读工作单元里读仓储；结果进有界缓存，键（主体种类、主体 ID、租户），容量 `CacheSize`（默认 10000，满时整体清空），TTL `CacheTimeToLive`（默认 30 秒，0 不过期），`Invalidate(subject?)` 按主体或全部失效。基础设施异常原样抛出，由内核转成 Unexpected 结果。
- `PermissionGrantRecordStore`（作用域，调用的 Transaction 阶段内、用调用方的工作单元）：`GrantAsync` / `RevokeAsync` 批量逐项 `Result`（已存在 → 成功不重复写；不存在 → 新错误码 `SECURITY_NOTFOUND_002`、消息键 `security.grant.not_found`），一次查询取回已有行、一次 `AddRangeAsync` / `RemoveRangeAsync` 写入，写入后对涉及主体 `Invalidate`；`ListAsync` 分页。
- 配置节 `XiHan:Security:Database`（`DatabaseSecurityOptions`）。

## 理由

- 直接开工作单元：授权判定不能依赖"调用能嵌套"，也不能依赖应用已在服务状态；只读工作单元不加入调用方的事务，读的是已提交数据。
- 空字符串租户列：唯一索引是防重复授予的最后一道闸，必须在所有支持的数据库上语义一致；换算只发生在一个地方（`ToGrant()` / `TenantColumnOf()`），契约不变。
- 缓存放存储：`IPermissionGrantStore` 契约与 `PermissionChecker` 不动，内存实现不需要缓存；写入 API 与存储同包，失效是直接的方法调用；多实例部署靠 TTL 收敛，这与 `Localization.Database` 的周期刷新同一思路。
- 批量优先（ADR-0012）：授予增删都是集合入参、逐项结果，不在循环里单条写入。

## 影响

- `Security.Abstractions`：新错误码常量 `SecurityErrors.GrantNotFound`（`SECURITY_NOTFOUND_002`）与消息键 `SecurityMessages.GrantNotFound`（`security.grant.not_found`，参数 `subject` / `permission` / `tenant`），10 种区域的语言文件各加一行；RFC-0008 与 `spec/conventions/error-codes.md` 由主会话修订登记。
- `Security.InMemory` 不改：与本包同时装配时解析器选显式 Provider 提供授权，内存实现只剩认证。
- BasicApp 6.0 的 RBAC 授予表按 `PermissionGrantRecord` 建模，授予的增删经 `PermissionGrantRecordStore`；多实例部署按可接受的生效延迟设 `CacheTimeToLiveSeconds`。
