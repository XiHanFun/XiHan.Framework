来源：https://framework.docs.xihanfun.com/deployment/kubernetes

# Kubernetes

这一页讲 `xihan pack` 生成的 Deployment 在集群里怎样与框架对齐：三个探针与有序停机、ConfigMap 配置热更新、用 Lease 做领导选举，以及怎样在本机的 kind 集群上逐项验证。读完你能把一个多副本应用部署到 Kubernetes，并确认探针、停机、热更新与选举都按预期工作。前置知识是[应用清单与 xihan pack](/deployment/pack)与[生命周期与健康](/startup/lifecycle)。

## 为什么需要

容器平台只认三件事：探针告诉它应用是否活着、是否能接流量；SIGTERM 之后的宽限期决定它何时强杀；ConfigMap 与 Lease 是它提供配置与协调的方式。应用若没按这些约定实现，表现就是滚动更新时请求失败、停机时事务被打断、改配置要重启、多副本把同一个定时作业跑了两遍。框架的内核已经提供了与之对应的能力：三个健康端点、按逆序停止的内核宽限、挂载目录的热重载配置源、可替换的领导选举。本页讲的就是这些能力与 Kubernetes 对象之间的对应关系。

## 探针与停机宽限

宿主的 `app.MapXiHanHealth()` 映射 `/health/startup`、`/health/ready`、`/health/live`，生成的 Deployment 以它们做三个探针：

| 探针 | 路径 | `failureThreshold` | 含义 |
| --- | --- | --- | --- |
| startupProbe | `/health/startup` | ⌈`startupSeconds` ÷ `periodSeconds`⌉，示例为 6 | 内核完成装配与启动之前返回 503，期间不触发 liveness |
| readinessProbe | `/health/ready` | 3 | 内核进入服务状态才返回 200，决定 Pod 是否进 Service 端点 |
| livenessProbe | `/health/live` | 3 | 进程存活即 200，失败才重启容器 |

三个探针的 `periodSeconds` 与 `timeoutSeconds` 取清单的 `probes`。内核没有进入服务状态时 `/health/ready` 返回 503；插件或 Provider 健康条目降级只把应用压成 `Degraded`，不让探针失败，所以一个外部依赖抖动不会把全部 Pod 摘出 Service。

停机宽限要两边对齐：`shutdown.graceSeconds` 写进 `XiHan:Kernel:StopTimeoutSeconds`（Dockerfile 与 Deployment 都以环境变量 `XiHan__Kernel__StopTimeoutSeconds` 设置），Deployment 的 `terminationGracePeriodSeconds` 取 `graceSeconds + 5`。kubelet 发 SIGTERM 后，内核在 `StopTimeoutSeconds` 内按与启动相反的顺序停止特性并完成 Draining（在途调用走完、不再接新调用），多出的 5 秒保证 kubelet 不会在内核收尾前 SIGKILL。进程外插件的停止宽限缺省也取这个值（[进程外插件](/plugins/out-of-process)）。示例里 `graceSeconds: 25` 生成 `XiHan__Kernel__StopTimeoutSeconds=25` 与 `terminationGracePeriodSeconds: 30`。

## 配置热更新

`config.files` 把列出的文件做成 ConfigMap（`<name>-config`）只读挂到 `mountPath`，并设两个环境变量：

| 环境变量 | 值 | 作用 |
| --- | --- | --- |
| `XIHAN_CONFIG_PATH` | `mountPath`（缺省 `/etc/xihan`） | 宿主的 `AddXiHan` 把这个目录里的 JSON 文件作为 `reloadOnChange` 配置源，插在 `appsettings.{环境}.json` 之后 |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `1` | ConfigMap 以切换符号链接的方式更新，文件监视必须轮询 |

`Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 在装配内核之前处理这个目录：只取顶层 `*.json`，跳过以 `.` 开头的条目（ConfigMap 的 `..data` 之类），`appsettings.json` 最先装载，其余按文件名序数排序随后覆盖。这些源紧接 `appsettings.{环境}.json` 插入：覆盖两份 appsettings，环境变量与命令行参数又覆盖它们，所以 Deployment 以环境变量写的 `XiHan__Kernel__StopTimeoutSeconds` 不会被 ConfigMap 里的同名键改掉（完整顺序见[配置目录与热更新](/configuration/config-directory)）。文件变化后 `IOptionsMonitor<T>` 随之更新，应用不需要重启：

```csharp
using Microsoft.Extensions.Options;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BannerOptions>(builder.Configuration.GetSection(BannerOptions.Section));

// AddXiHan 先装载 XIHAN_CONFIG_PATH 目录里的 JSON 文件并监听变化
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();

// 读 CurrentValue 而不是在启动时缓存，ConfigMap 更新后返回新值
app.MapGet("/banner", (IOptionsMonitor<BannerOptions> banner) => banner.CurrentValue.Banner);

app.Run();

public sealed class BannerOptions
{
    public const string Section = "Sample";

    public string Banner { get; set; } = "Hello from XiHan";
}
```

kubelet 同步 ConfigMap 有延迟，示例里改完约一分钟内生效。要在 Native AOT 下照常绑定与重载，项目设 `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`。

## Lease 领导选举

多副本部署时，集群唯一的作业只应在一个 Pod 上运行。`leader.lease` 生成 ServiceAccount、只含该 Lease 的 Role（`coordination.k8s.io` 的 `leases`：对该名字 `get` / `update` / `patch`，另加 `create`）与 RoleBinding，并在 Deployment 里设：

| 环境变量 | 来源 |
| --- | --- |
| `XiHan__Scheduling__Kubernetes__Lease` | `leader.lease` |
| `XIHAN_POD_NAME` | Pod 字段 `metadata.name`，作为 Lease 持有者 |
| `POD_NAMESPACE` | Pod 字段 `metadata.namespace` |

应用在集群里装配 `KubernetesSchedulingFeature`（[Scheduling.Kubernetes](/packages/scheduling-kubernetes)），它以 Lease 顶替进程内选举，直接调用 API Server、不依赖客户端库。它只替换领导选举，作业存储仍在各 Pod 内存里；需要跨副本持久化作业、节点失联恢复时换 `Scheduling.Database`（[集群与管理](/scheduling/cluster)）。

## 最小示例

`samples/KubernetesApp` 用 Full 组合演示上述四项，它的 `deploy/` 是 `xihan pack` 生成并提交的产物。它按 `KUBERNETES_SERVICE_HOST` 是否存在决定是否装配 Lease 选举，本地运行退回 Full 组合自带的进程内选举：

```csharp
using KubernetesApp.Heartbeat;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Kubernetes;

var builder = WebApplication.CreateBuilder(args);

// API Server 的环境变量只在 Pod 里存在
var inCluster = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;

builder.AddXiHan(xihan =>
{
    xihan.UseFullProfile().UseFeature<HeartbeatFeature>();
    if (inCluster)
    {
        xihan.UseFeature<KubernetesSchedulingFeature>();
    }
});

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();        // /health/startup、/health/ready、/health/live：Deployment 的三个探针
app.MapXiHanOperations();

// 只有持有 Lease 的 Pod 按计划运行心跳作业；响应类型是记录而不是匿名类型，AOT 下才能经源生成序列化
app.MapGet("/leader", (JobSchedulerEngine scheduler, ILeaderElection election) =>
    new LeaderView(election.GetType().Name, scheduler.IsLeader));

app.Run();

public sealed record LeaderView(string Election, bool IsLeader);
```

`HeartbeatFeature` 是一个依赖 `scheduling.jobs@1` 的能力包，在 `Configure` 里登记一个每 10 秒一次的作业：

```csharp
context.Services.AddJob<HeartbeatJob>(new JobName("heartbeat"), JobSchedule.Every(TimeSpan.FromSeconds(10)));
```

作业缺省只在领导上运行（[作业与计划](/scheduling/jobs)），所以两个 Pod 里只有持有 Lease 的那个计数增长。本地运行并确认端点（在仓库根执行）：

```bash
dotnet run --project framework/samples/KubernetesApp
curl -s localhost:5100/config      # banner 来自 appsettings.json，configPath 为空
curl -s localhost:5100/leader      # election 为 InMemoryLeaderElection，isLeader 为 true
```

## 在 kind 上验证

1. 改了 `xihan.app.json` 后重新生成产物：

   ```bash
   dotnet run --project framework/src/XiHan.Framework.Cli -- pack --manifest framework/samples/KubernetesApp/xihan.app.json
   ```

2. 构建镜像（`build.context` 是 `framework/`）并载入 kind，`<tag>` 取 `xihan.app.json` 的 `image.tag`：

   ```bash
   cd framework
   docker build -f samples/KubernetesApp/deploy/Dockerfile -t ghcr.io/xihanfun/kubernetes-app:<tag> .
   kind load docker-image ghcr.io/xihanfun/kubernetes-app:<tag>
   ```

3. 部署并转发端口：

   ```bash
   kubectl apply -k samples/KubernetesApp/deploy/kustomize
   kubectl rollout status deployment/kubernetes-app
   kubectl port-forward service/kubernetes-app 8080:8080
   ```

4. 逐项验证：

   | 项 | 做法 | 预期 |
   | --- | --- | --- |
   | 探针 | `kubectl describe pod <pod>` | startup `/health/startup`（`failureThreshold: 6`），readiness `/health/ready`，liveness `/health/live`；内核未进入服务状态时 `/health/ready` 返回 503 |
   | 有序停机 | `kubectl delete pod <pod>` 后看日志 | 内核在 25 秒内按逆序停止特性；`terminationGracePeriodSeconds: 30`，kubelet 不提前 SIGKILL |
   | 热更新 | `kubectl edit configmap kubernetes-app-config`，改 `Sample.Banner` | 约一分钟内 `curl localhost:8080/config` 返回新横幅，Pod 不重启 |
   | 领导选举 | `kubectl get lease kubernetes-app-scheduler -o yaml`，对两个 Pod 各请求 `/leader` | 只有持有者 `isLeader: true` 且 `ticks` 增长；删除持有者 Pod 时它在有序停机里释放 Lease，另一个 Pod 在下一次轮询（示例配置 `XiHan:Scheduling:PollIntervalSeconds` 为 1 秒）接管；持有者被强杀时等租约（`XiHan:Scheduling:LeaseTimeToLiveSeconds`，15 秒）过期后接管，`leaseTransitions` 加一 |

## 多副本的其他注意事项

- 消息代理：广播订阅的队列或消费组名带实例标识，缺省每次启动生成；要固定就用下行 API 把 Pod 名设进 `XiHan__Messaging__InstanceId`，不要写进共用配置（[应用清单与 xihan pack](/deployment/pack)）。
- 绑定 Secret：按 `bindings.example.yaml` 创建，挂载到 `/bindings/<绑定名>`，连接串与凭据不进镜像与 ConfigMap（[资源绑定](/configuration/bindings)）。
- HPA 与 PDB：清单有 `scaling` 时生成 HPA，副本大于 1 时生成 PDB，滚动更新与节点维护时至少保留一个可用副本。
- 只读根文件系统：生成的容器只有 `/tmp` 可写，默认实现里写文件的 Provider（如 `ObjectStorage.FileSystem`）要把根目录指到 `/tmp` 之下或改用持久卷；示例的 ConfigMap 就把 `XiHan:ObjectStorage:FileSystem:RootPath` 设为 `/tmp/xihan-blobs`。

## 常见问题

### 滚动更新时还是有请求失败

先看 readiness 是否真的在内核进入服务状态后才返回 200（`MapXiHanHealth` 已保证），再看 `terminationGracePeriodSeconds` 是否大于内核的 `StopTimeoutSeconds`。用 `xihan pack` 生成的 Deployment 两者自动对齐，手写的常常只设了其中一个。

### ConfigMap 改了，应用没变

三个常见原因：Pod 里没有 `DOTNET_USE_POLLING_FILE_WATCHER=1`（符号链接切换不触发 inotify）；代码在启动时读了一次 `IOptions<T>` 并缓存，而不是读 `IOptionsMonitor<T>.CurrentValue`；改的键在环境变量里也有同名值，环境变量优先。另外 kubelet 同步周期约一分钟，不是立刻生效。

### 两个 Pod 都在跑同一个定时作业

集群里没有装配 `KubernetesSchedulingFeature`，两个 Pod 各用进程内选举、各自都是领导。确认 Deployment 里有 `XiHan__Scheduling__Kubernetes__Lease`，ServiceAccount 有 Lease 的 RBAC，`kubectl get lease` 能看到持有者。

### 持有者 Pod 被强杀后多久接管

等租约过期：`XiHan:Scheduling:LeaseTimeToLiveSeconds`（示例 15 秒）加一次轮询间隔。正常删除 Pod 时内核在有序停机里释放 Lease，另一个 Pod 下一次轮询就接管。

### 能不能不用 kind

可以，任何集群都一样；kind 只是本机验证的最低成本方式。换集群时镜像要推到集群能拉取的注册表，`kind load docker-image` 换成 `docker push`。

## 相关包与下一步

- 包参考：[Scheduling.Kubernetes](/packages/scheduling-kubernetes)、[Profiles.Full](/packages/profiles-full)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Cli](/packages/cli)
- 相关章节：[应用清单与 xihan pack](/deployment/pack)、[生命周期与健康](/startup/lifecycle)、[配置目录与热更新](/configuration/config-directory)、[作业与计划](/scheduling/jobs)、[集群与管理](/scheduling/cluster)
- 规范：RFC-0016《应用清单与打包》
- 下一步：[单元与集成测试](/testing/basics)讲怎样在内存里装配应用、测中间件与操作
