# RFC-0007 多租户

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：— | 参考实现：`XiHan.Framework.MultiTenancy.Abstractions`、`XiHan.Framework.MultiTenancy.InMemory` | 一致性：`XiHan.Framework.MultiTenancy.Conformance`

## 摘要

租户是调用上下文的一部分，由 Tenant 阶段一次解析、校验并写入安全上下文；之后的阶段（授权、工作单元、缓存键）只读它。没有环境态访问器。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `multitenancy.context@1` | 租户解析、校验与存储 | `MultiTenancy.InMemory`（从配置装载租户） |
| `multitenancy.sharding@1` | 按租户选择数据分片（预留） | — |

## 租户 ID

`^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`，区分大小写。它出现在 Baggage `xihan.tenant`、`xrn` 资源名的租户段、持久化实体的 `TenantId` 中，三处同一文法。

## 解析

1. 按注册顺序调用 `ITenantResolver`，第一个有值的结果生效。默认顺序：已认证主体的 `tenant` 声明 → Baggage `xihan.tenant`。已认证身份所属的租户优先于客户端自报的租户。
2. 没有解析到时用 `DefaultTenant`；仍没有时，`RequireTenant` 为真返回 `MULTITENANCY_VALIDATION_002`，否则无租户继续。
3. 校验格式，不合法返回 `MULTITENANCY_VALIDATION_001`。
4. `ValidateAgainstStore` 为真（默认）时核对 `ITenantStore`：不存在返回 `MULTITENANCY_NOTFOUND_001`，禁用返回 `MULTITENANCY_DENIED_001`；显示名随之写入上下文。
5. 写入 `SecurityContext.Tenant`。

宿主适配器（P3）把 HTTP 头或主机名映射为 Baggage，不直接写租户。

## 存储

`ITenantStore`：按 ID 查找、枚举。租户信息含显示名、启用标记与字符串属性（租户专属连接、配额等）。内存实现有界（默认 1000），从 `XiHan:MultiTenancy:Tenants` 装载。

## 一致性

Provider 必须继承 `TenantStoreConformance` 并通过全部用例。

## 错误码

见 `conventions/error-codes.md` 中 `MULTITENANCY_*`。
