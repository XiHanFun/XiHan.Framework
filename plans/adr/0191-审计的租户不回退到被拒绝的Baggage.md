# ADR-0191 审计的租户不回退到被拒绝的 Baggage

## 状态

已采纳（RFC-0015「审计」事件字段一条、RFC-0007「解析」修订）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-34（`plans/5.0-安全审查.md`），低危。

- `AuditMiddleware` 取事件的租户时先看安全上下文，没有就回退到 Baggage 的 `xihan.tenant`。回退原本是为没有 Tenant 阶段的宿主准备的：插件协议、消息派发、命令行只带 Baggage。
- 装配了多租户时，Tenant 阶段拒绝调用（租户不存在、已禁用、文法不对、解析器拒绝）后安全上下文没有租户，审计照样回退，把调用方自报的租户记成事件的租户。
- 后果：
  - 失败的调用落进它自报的租户的审计，那个租户的管理员看到一条从未在本租户发生的调用。
  - 不存在的租户 ID 也写进审计存储。
- Baggage 的租户只来自内部通道（HTTP 入口不收它），但消息的发送方、插件都能给出任意值。

复现：`AuditTenantTests`。修正前，自报不存在的租户 `ghost` 与已禁用的租户 `frozen` 的调用都被拒绝，审计事件的 `TenantId` 却分别是 `ghost`、`frozen`。

## 备选

1. **按结果的错误码判断是不是被 Tenant 阶段拒绝**：否决。审计要认得多租户的错误码，换一个解析器的拒绝码就漏掉。
2. **一律不回退**：否决。没有 Tenant 阶段的宿主就没有别的租户来源，插件与消息调用的审计会全部丢掉租户。
3. **Tenant 阶段记下它裁定过租户，审计据此不回退**：采纳。

## 决策

1. 内核 `SecurityContextExtensions` 新增 `MarkTenantResolved()` / `IsTenantResolved()`，是调用上下文条目，不跨进程传播。
2. Tenant 阶段一进入就调用 `MarkTenantResolved()`，不论之后解析出租户、裁定为无租户还是拒绝调用。此后安全上下文里的租户就是这次调用唯一可信的租户。
3. `AuditMiddleware`：租户先取安全上下文；只有 `IsTenantResolved()` 为假（没有 Tenant 阶段）时才回退到 Baggage。被拒绝的调用记为没有租户，自报的租户已在结果的错误参数 `tenantId` 里，事件照样带着它。
4. 主体的回退不变：Baggage 的主体由插件协议改写、由消息派发从扩展属性带入，属于 ADR-0121 已接受的设计。

## 影响

- 行为：装配了多租户时，被 Tenant 阶段拒绝的调用的审计事件没有租户。按租户查询审计的管理员看不到它们，按全局查询仍可见。
- 公开 API：`SecurityContextExtensions.MarkTenantResolved`、`IsTenantResolved`（内核，新增）。
- 应用自己的观察者（指标、日志）要按租户归类时同样以 `IsTenantResolved()` 判断能不能回退。
