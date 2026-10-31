# XiHan.Framework.Scheduling.Kubernetes

`scheduling.leader@1` 的 Kubernetes Provider：用 `coordination.k8s.io/v1` 的 Lease 对象在 Pod 之间选出领导，直接调用 API Server，不依赖 Kubernetes 客户端库。适用于部署在 Kubernetes 上、只要求集群唯一的作业在一个 Pod 上运行的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Scheduling.Kubernetes` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `scheduling.leader@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `KubernetesSchedulingFeature`（清单 ID `xihan.scheduling.kubernetes`） |
| 配置节 | `XiHan:Scheduling:Kubernetes` |
| 依赖 | `XiHan.Framework.Scheduling.Abstractions`；无第三方依赖 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包只提供领导选举，不提供作业存储。它与 `Scheduling.InMemory` 一起使用：作业调度仍由抽象包的引擎加内存作业存储承担，`KubernetesLeaderElection` 顶替进程内选举。内存存储不共享，引擎因此只让持有 Lease 的 Pod 按计划运行集群唯一（`RunOnEveryInstance = false`）的作业，其他 Pod 只运行本地作业、手动触发与入队的运行。

选举的规则：

- 获取：Lease 不存在就创建；持有者为空，或 `renewTime + leaseDurationSeconds` 已过，就接管并把 `leaseTransitions` 加一；否则返回 `SCHEDULING_CONFLICT_002`。并发竞争由 resourceVersion 裁决，409 也视为被占用。
- 续约写 `renewTime`，释放清空 `holderIdentity`。引擎每个轮询周期续约一次。
- 存活时间按秒向上取整写进 Lease（Kubernetes 只有整数秒），本地判断是否持有仍用精确值。
- API Server 不可达或超时（客户端超时 10 秒）→ `SCHEDULING_UNAVAILABLE_002`，引擎下个周期重试。

调度器的默认资源 `scheduling.jobs` 映射到配置的 Lease 名；其他资源映射到 `<Lease>-<资源>`，点与下划线换成连字符，截到 63 个字符（`KubernetesSchedulingOptions.LeaseNameFor`）。

命名空间、持有者与 API Server 按 Pod 内的约定解析：

| 项 | 解析顺序 |
| --- | --- |
| 命名空间 | 配置 `Namespace` → ServiceAccount 挂载的 `namespace` 文件 → 环境变量 `POD_NAMESPACE` → `default` |
| 持有者 | 配置 `Holder` → 环境变量 `XIHAN_POD_NAME` → `HOSTNAME` → 机器名 |
| API Server | 配置 `ApiServer` → `KUBERNETES_SERVICE_HOST` / `KUBERNETES_SERVICE_PORT` → `https://kubernetes.default.svc` |

令牌每次请求都从挂载文件读取，跟得上投射令牌的轮换；CA 证书文件存在时作为唯一的信任根，不存在时用系统信任。

## 何时使用

- Kubernetes 多副本，后台作业只需要在一个 Pod 上跑，运行时作业与历史不必在副本间共享。
- 需要共享存储、节点失联恢复或在副本间分发作业时用 [Scheduling.Database](./scheduling-database)；它同时提供领导选举，不必再装配本包。
- 本地开发没有 API Server，不装配本包，退回进程内选举。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Scheduling.Kubernetes" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。示例应用 `next/samples/KubernetesApp` 只在 Pod 内装配本包：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Scheduling.Kubernetes;

var builder = WebApplication.CreateBuilder(args);
// API Server 的环境变量存在即运行在 Pod 里
var inCluster = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;
builder.AddXiHan(xihan =>
{
    xihan.UseFullProfile();
    if (inCluster)
    {
        xihan.UseFeature<KubernetesSchedulingFeature>();
    }
});
```

Pod 的 ServiceAccount 需要的最小权限：

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: orders-leader
rules:
  - apiGroups: ["coordination.k8s.io"]
    resources: ["leases"]
    resourceNames: [orders-scheduler]
    verbs: ["get", "update", "patch"]
  - apiGroups: ["coordination.k8s.io"]
    resources: ["leases"]
    verbs: ["create"]
```

`xihan pack` 把应用清单 `xihan.app.json` 里的 `leader.lease` 写进环境变量 `XiHan__Scheduling__Kubernetes__Lease`，并生成 ServiceAccount、只含该 Lease 的 Role 与 RoleBinding，见[部署与打包](../guide/deployment)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Scheduling:Kubernetes:Lease` | DNS 标签 | `xihan-scheduler` | 调度器租约的 Lease 名 |
| `XiHan:Scheduling:Kubernetes:Namespace` | 字符串 | 按 Pod 约定解析 | 命名空间 |
| `XiHan:Scheduling:Kubernetes:Holder` | 字符串 | 按 Pod 约定解析 | 持有者标识 |
| `XiHan:Scheduling:Kubernetes:ApiServer` | 绝对 URI | 按 Pod 约定解析 | API Server 地址 |
| `XiHan:Scheduling:Kubernetes:TokenPath` | 路径 | `/var/run/secrets/kubernetes.io/serviceaccount/token` | ServiceAccount 令牌文件 |
| `XiHan:Scheduling:Kubernetes:CertificateAuthorityPath` | 路径 | `/var/run/secrets/kubernetes.io/serviceaccount/ca.crt` | CA 证书文件 |

```json
{ "XiHan": { "Scheduling": { "Kubernetes": { "Lease": "orders-scheduler" } } } }
```

租约的存活时间与资源名是引擎选项 `XiHan:Scheduling:LeaseTimeToLiveSeconds`（缺省 30）与 `XiHan:Scheduling:LeaseResource`（缺省 `scheduling.jobs`），见 [Scheduling.Abstractions](./scheduling-abstractions#配置)。`Lease` 不是 DNS 标签、`ApiServer` 不是绝对 URI 时启动抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `KubernetesSchedulingFeature` | 特性：注册选项、Lease 客户端与选举；被选中提供 `scheduling.leader` 时把 `ILeaderElection` 指向本包 |
| `KubernetesLeaderElection` | `ILeaderElection` 实现；`Holder` 是本实例的持有者标识 |
| `KubernetesSchedulingOptions` | 配置项，以及 `ResolveNamespace()`、`ResolveHolder()`、`ResolveApiServer()`、`LeaseNameFor(resource)` |
| `Leases.KubernetesLeaseClient` | Lease 的 GET / POST / PUT 客户端；`CreateInCluster(options)` 按 Pod 约定创建，应用或测试可预先注册自己的实例替代它 |

## 使用示例

确认当前 Pod 是否是领导（取自示例应用的 `/leader` 端点）：

```csharp
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Kubernetes;

app.MapGet("/leader", (JobSchedulerEngine scheduler, ILeaderElection election) =>
    new LeaderView(election is KubernetesLeaderElection kubernetes ? kubernetes.Holder : Environment.MachineName, scheduler.IsLeader));

public sealed record LeaderView(string Holder, bool IsLeader);
```

对两个 Pod 各请求一次，只有 Lease 持有者返回 `IsLeader` 为真；`kubectl get lease orders-scheduler -o yaml` 能看到 `holderIdentity` 与 `leaseTransitions`。持有者 Pod 正常停止时引擎释放租约，另一个 Pod 下一个轮询周期接管；Pod 被强制杀掉时，要等租约过期（缺省 30 秒）。

## 行为与限制

- 只提供领导选举。运行时登记的作业、暂停状态与运行历史仍在各 Pod 的内存里，不在副本间共享，也没有失联恢复。
- Lease 存活时间以整秒写入，小于 1 秒的存活时间在 API Server 上按 1 秒处理。
- 与 `Scheduling.Database` 同时装配时，两个显式 Provider 都提供 `scheduling.leader`，需要用 `XiHan:Providers:scheduling.leader` 指定一个，规则见[能力包与清单](../guide/modularity)。
- 通过 `Scheduling.Conformance` 的 `LeaderElectionConformance`（以内存 API Server 桩验证）。

## 相关

- [调度与后台作业](../guide/scheduling)
- [Scheduling.Abstractions](./scheduling-abstractions)
- [Scheduling.InMemory](./scheduling-inmemory)
- [Scheduling.Database](./scheduling-database)
- [部署与打包](../guide/deployment)
- 规范：RFC-0013《调度与领导选举》
