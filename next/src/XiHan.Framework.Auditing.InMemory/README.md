# XiHan.Framework.Auditing.InMemory

内存审计（Ring 3，默认实现，`role: default`）。提供 `auditing.trail@1`（RFC-0015）。

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryAuditingFeature>());
```

```json
{ "XiHan": { "Auditing": { "Selection": "Protected", "IncludeSuccesses": true, "InMemory": { "Capacity": 10000, "LogEvents": true } } } }
```

## 规则

- 环形缓冲保留最近 `Capacity` 条事件（默认 10000），满了丢最旧的；续页令牌是序号，淘汰后仍然有效。
- 按写入顺序升序查询；过滤项见 `AuditQuery`；不合法的查询 → `AUDITING_VALIDATION_001`。
- `LogEvents` 为真时每条事件以 Information 级写日志（内核日志薄桥），字段：动作、结果、主体、租户、资源、trace-id。
- 被选中时挂 `AuditMiddleware`（Observe 末尾）；另有显式 Provider 提供 `auditing.trail` 时本包不绑定也不挂中间件。
- 单进程、重启即失；生产持久化换 `Auditing.Database` 或 `Auditing.Messaging`。

## 依赖方向

依赖 `XiHan.Framework.Auditing.Abstractions`。

## 状态

P3：可用，通过 `Auditing.Conformance`。
