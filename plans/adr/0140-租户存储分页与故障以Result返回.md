# ADR-0140 租户存储分页与故障以 Result 返回

## 状态

已采纳（规范 `rfcs/0007-multitenancy.md` 修订；错误码登记进 `conventions/error-codes.md`）。

## 日期

2026-11-11

## 背景

`ITenantStore`（`Stable`）有两个问题：

1. **`ListAsync()` 不分页**。数据库实现读到 `MaxListedTenants`（缺省 10000）就停，只记一条警告，调用方无从得知。工作流的到期书签轮询靠这个枚举覆盖所有租户的分区，超出上限的租户永远不会被轮询。
2. **故障的表现不一致**。契约写的是「基础设施失败抛出异常」：Tenant 阶段不捕获，调用以 500 结束；分片解析器却把同一个异常转成 `MULTITENANCY_UNAVAILABLE_002`。这也和框架的约定不合——可恢复的失败返回 `Result`。授予存储的「读不到不是没有」（ADR-0071）就是以 `Result` 表达的。

## 决策

- `FindAsync(tenantId)` 返回 `Result<Maybe<TenantInfo>>`：
  - 没有这个租户是成功的无值；
  - 存储不可达（连接、超时、事务失败）返回新错误码 `MULTITENANCY_UNAVAILABLE_003`（消息键 `multitenancy.store.unavailable`）。
- `ListAsync(TenantQuery)` 返回 `Result<TenantPage>`：
  - `TenantQuery` 有 `PageSize`（1～`MaxPageSize` 1000，缺省 100）、不透明的 `ContinuationToken`、`EnabledOnly`，`Validate()` 校验每页条数。
  - `TenantPage` 有 `Items` 和 `ContinuationToken`，令牌为 `null` 表示读完。
  - 每页条数越界或令牌不是本存储给出的，返回新错误码 `MULTITENANCY_VALIDATION_003`（消息键 `multitenancy.tenant.query_page_invalid` / `query_token_invalid`）。
  - 顺序按 ID 升序，比较规则由存储决定：内存实现按序数，关系库按库的排序规则。
- **Tenant 阶段**：存储失败原样返回（503），不当作租户不存在（404）。
- **分片解析器**：存储的错误作为内部错误包进 `MULTITENANCY_UNAVAILABLE_002`，`reason` 写存储的错误码。
- **内存实现**：续页令牌是上一页最后一个 ID（键集）。
- **数据库实现**：
  - 续页令牌是偏移，对调用方不透明，以后可以换成键集。
  - 读取经 `CatalogInvocation` 统一转成 `Result`，失败不进缓存。
  - 选项 `MaxListedTenants` 删除，不再有截断。
- **工作流轮询**：以 `EnabledOnly` 逐页读完。中途失败时本周期只覆盖平台。
- **一致性套件**：用例 4、5 核对分页、启用过滤、越界与外来令牌。另加一条中间件测试：存储不可达时调用失败，并且不报「不存在」。

## 影响

- 实现 `ITenantStore` 的 Provider 要改两个方法的签名，原来以异常报告的故障改为返回 `MULTITENANCY_UNAVAILABLE_003`。
- 调用方：
  - `FindAsync` 多一层 `Result`。
  - `ListAsync()` 改为逐页读到令牌为空。
- 配置 `XiHan:MultiTenancy:Database:MaxListedTenants` 删除；`FromConfiguration` 不再读它，配置了它的应用删掉这个键。
- `MultiTenancy.Abstractions` 的契约是 `Stable`，GA 前修改，不需要登记豁免（ADR-0131）。
