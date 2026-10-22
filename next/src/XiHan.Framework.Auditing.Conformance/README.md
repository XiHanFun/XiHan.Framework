# XiHan.Framework.Auditing.Conformance

审计一致性套件（RFC-0015）。Provider 的测试项目继承 `AuditTrailConformance` 并实现 `CreateFixtureAsync()` 返回 `AuditTrailFixture`（被测 `IAuditSink` 与 `IAuditTrail`），全部用例随之执行：写入后按写入顺序读回且字段完整往返、各过滤项取交集、分页与续页令牌、不合法查询是 VALIDATION。用例以 trace-id 隔离数据，夹具可以共用一个存储。

## 依赖方向

依赖 `XiHan.Framework.Auditing.Abstractions` 与 xunit 的断言与扩展核心包。

## 状态

P3：可用。
