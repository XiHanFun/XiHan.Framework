# XiHan.Framework.Scheduling.Kubernetes

`scheduling.leader@1` 的 Kubernetes Provider（Ring 3，`role: provider`，RFC-0013）。以 `coordination.k8s.io/v1` 的 Lease 做跨 Pod 的领导选举，直接调用 API Server，不依赖客户端库。装配后顶替 `Scheduling.InMemory` 的进程内选举；作业调度仍由抽象包的引擎 + 内存作业存储承担，不共享的存储下只有持有 Lease 的 Pod 按计划运行集群唯一（`RunOnEveryInstance = false`）的作业。多实例需要持久化与分发时换 `Scheduling.Database`。

```csharp
builder.AddXiHan(x => x.UseFullProfile().UseFeature<KubernetesSchedulingFeature>());
```

```json
{ "XiHan": { "Scheduling": { "Kubernetes": { "Lease": "orders-scheduler" } } } }
```

## 规则

- 调度器的默认资源 `scheduling.jobs` 映射到 `Lease`；其他资源映射到 `<Lease>-<资源>`。`xihan pack` 的 `leader.lease` 写进 `XiHan__Scheduling__Kubernetes__Lease`，并生成只含该 Lease 的 Role。
- 获取：Lease 不存在就创建；持有者为空或 `renewTime + leaseDurationSeconds` 已过就接管（`leaseTransitions` 加一）；否则 `SCHEDULING_CONFLICT_002`。并发由 resourceVersion 裁决（409 → 被占用）。续约写 `renewTime`，释放清空 `holderIdentity`。
- 存活时间按秒向上取整写进 Lease（Kubernetes 只有整数秒），本地判定仍用精确值。
- 命名空间、持有者与 API Server 按 Pod 内约定解析（ServiceAccount 挂载、`POD_NAMESPACE`、`XIHAN_POD_NAME` / `HOSTNAME`、`KUBERNETES_SERVICE_HOST`）；令牌每次请求从挂载文件读取以跟上轮换；CA 存在时作为唯一信任根。
- API Server 不可达或超时 → `SCHEDULING_UNAVAILABLE_002`，引擎下个周期重试。

RBAC 最小权限：`coordination.k8s.io` 的 `leases` 资源 `get` / `update` / `patch`（限定 `resourceNames`）与 `create`。

## 依赖方向

依赖 `XiHan.Framework.Scheduling.Abstractions`。

## 状态

P3：可用，通过 `Scheduling.Conformance` 的 `LeaderElectionConformance`（以内存 API Server 桩验证）。
