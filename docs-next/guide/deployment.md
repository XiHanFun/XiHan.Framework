# 部署与打包

本页讲怎样用应用清单 `xihan.app.json` 和 `xihan pack` 生成容器镜像与 Kubernetes、Compose、Aspire 的部署产物，以及探针、停机宽限、配置热更新、领导选举与 Native AOT 在部署时怎样对齐，适用于把应用交付到容器平台的开发与运维人员。命令的完整用法见 [Cli 命令行工具](../packages/cli)。

## 应用清单

应用在项目旁放一份 `xihan.app.json` 描述自己（RFC-0016，规范 `spec/packaging/app-manifest.md`，Schema `spec/packaging/app.schema.json`）：名字、项目、组合包、镜像、端口、资源绑定、配置挂载、探针、停机宽限、资源与伸缩、领导租约。`xihan pack` 只读这份清单，产物每次整份重新生成，不手改。它与能力包的清单 `xihan.manifest.json`（[能力包与清单](./modularity)）是两回事：后者描述一个包提供与依赖什么能力，前者描述一个可部署的应用。

示例 `samples/KubernetesApp/xihan.app.json`：

```json
{
  "$schema": "https://spec.xihanfun.com/packaging/app.v1.json",
  "name": "kubernetes-app",
  "project": "KubernetesApp.csproj",
  "profile": "full",
  "build": { "context": "../.." },
  "image": { "repository": "ghcr.io/xihanfun/kubernetes-app", "tag": "5.0.0-preview.1" },
  "ports": { "http": 8080, "grpc": 8081 },
  "config": { "files": ["appsettings.Production.json"] },
  "probes": { "startupSeconds": 60, "periodSeconds": 10, "timeoutSeconds": 2 },
  "shutdown": { "graceSeconds": 25 },
  "resources": { "requests": { "cpu": "100m", "memory": "128Mi" }, "limits": { "cpu": "500m", "memory": "256Mi" } },
  "replicas": 2,
  "scaling": { "min": 2, "max": 4, "cpuPercent": 70 },
  "leader": { "lease": "kubernetes-app-scheduler" },
  "environment": { "ASPNETCORE_ENVIRONMENT": "Production" }
}
```

| 字段 | 必填 | 缺省 | 约束与作用 |
| --- | --- | --- | --- |
| `name` | 是 | — | DNS 标签 `^[a-z][a-z0-9-]{0,62}$`，不以 `-` 结尾；同时是镜像仓库、k8s 资源与 Compose 项目的缺省名 |
| `project` | 是 | — | 相对清单所在目录的项目文件，必须存在 |
| `profile` | 否 | `standard` | `minimal` / `standard` / `full`，只写进标签 `xihan.profile` |
| `build.context` | 否 | `.` | Docker 构建上下文，相对清单所在目录，必须存在；Dockerfile 里的项目路径与 Compose 的 `context` / `dockerfile` 都相对它 |
| `image.repository` | 否 | `name` | 镜像仓库，可带注册表前缀 |
| `image.tag` | 否 | `latest` | 镜像标签 |
| `image.aot` | 否 | `false` | 为真时以 Native AOT 发布，运行镜像为 `runtime-deps` |
| `ports.http` | 否 | `8080` | 1–65535 |
| `ports.grpc` | 否 | — | 1–65535，不等于 `http`；有值时多暴露一个 h2c 端点 |
| `bindings.<name>.type` | 有绑定时必填 | — | `connection:relational` / `cache:distributed` / `queue` / `blob`；绑定名 `^[a-z][a-z0-9-]*$` |
| `bindings.<name>.secret` | 否 | `<name 字段>-<绑定名>` | k8s Secret 名 |
| `config.mountPath` | 否 | `/etc/xihan` | 绝对路径 |
| `config.files` | 否 | `[]` | 文件名（不含目录分隔符），文件存在时内容进 ConfigMap，否则为 `{}` |
| `probes.startupSeconds` | 否 | `60` | 不小于 `periodSeconds` |
| `probes.periodSeconds` | 否 | `10` | 至少 1 |
| `probes.timeoutSeconds` | 否 | `2` | 至少 1 |
| `shutdown.graceSeconds` | 否 | `30` | 至少 1 |
| `resources.requests` / `limits` | 否 | — | `cpu`、`memory`，k8s 数量写法（`500m`、`512Mi`） |
| `replicas` | 否 | `1` | 至少 1 |
| `scaling.min` / `max` / `cpuPercent` | 否 | — | `min` 至少 1，`max` 不小于 `min`，`cpuPercent` 1–100（缺省 70）；有值时生成 HPA |
| `leader.lease` | 否 | — | DNS 标签；有值时生成 Lease 的 RBAC |
| `environment` | 否 | `{}` | 环境变量，键 `^[A-Za-z_][A-Za-z0-9_]*$` |

清单不合法时 `xihan pack` 以 `CLI_VALIDATION_001` 失败（退出码 2），`detail` 一次列出全部问题。

## 生成部署产物

5.0 尚未发布到 nuget.org，工具可以从源码运行（在仓库根执行）：

```bash
dotnet run --project next/src/XiHan.Framework.Cli -- pack --manifest next/samples/KubernetesApp/xihan.app.json
```

取得预览包后作为 .NET 工具安装（来源见[快速上手](../quickstart#获取预览包)），在清单所在目录执行：

```bash
xihan pack                                        # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --output deploy/orders --only dockerfile,kustomize
```

成功时逐行输出写出的文件（相对输出目录），最后一行是写出的文件数与输出目录，退出码 0。产物：

| 产物 | 路径 | 内容 |
| --- | --- | --- |
| Dockerfile | `Dockerfile`、`Dockerfile.dockerignore` | 多阶段构建；BuildKit 读与 Dockerfile 同目录的同名忽略文件，忽略 `bin/`、`obj/`、`deploy/`、`.git/` |
| Kustomize | `kustomize/*.yaml` | Deployment、Service、ConfigMap、HPA（有 `scaling` 时）、PDB（副本大于 1 时）、ServiceAccount + Role + RoleBinding（有 `leader` 时）、`kustomization.yaml`；有绑定时另有不在 kustomization 里的 `bindings.example.yaml`（Secret 示例） |
| Compose | `compose.yaml` | 应用服务，加每个绑定一个依赖服务 |
| Aspire | `aspire/XiHanAppHost.g.cs` | `Add<Name>(this IDistributedApplicationBuilder)` 扩展方法，放进 AppHost 项目调用 |

### Dockerfile

生成的 Dockerfile 以 `mcr.microsoft.com/dotnet/sdk:10.0` 发布，运行阶段用 chiseled 镜像 `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`（没有 shell 与包管理器），以 `USER $APP_UID` 非 root 运行；Kestrel 端点与内核停止超时以环境变量写入。`samples/KubernetesApp/deploy/Dockerfile`：

```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet publish "samples/KubernetesApp/KubernetesApp.csproj" --configuration Release --arch $TARGETARCH --output /app/publish --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:8080
ENV Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:8081 Kestrel__Endpoints__Grpc__Protocols=Http2
ENV XiHan__Kernel__StopTimeoutSeconds=25
EXPOSE 8080 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "KubernetesApp.dll"]
```

`image.aot` 为真时，构建阶段先装 `clang` 与 `zlib1g-dev`，以 `--self-contained true -p:PublishAot=true` 发布，运行阶段换成 `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled`，入口是原生可执行文件。

### Kustomize

Deployment 带三个探针、停机宽限、Pod 与容器的安全上下文（`runAsNonRoot`、`seccompProfile: RuntimeDefault`、`allowPrivilegeEscalation: false`、丢弃全部能力、只读根文件系统，`/tmp` 是 emptyDir）、资源、配置挂载与绑定挂载；Service 暴露同名的 `http` / `grpc` 端口；`kustomization.yaml` 的 `images` 写入 `image.tag`。

### Compose 与 Aspire

Compose 用于本地联调：应用服务的 `stop_grace_period` 取 `shutdown.graceSeconds`，每个绑定一个依赖服务，以 `XiHan__Bindings__<绑定名>__type` / `__value` 注入，依赖服务健康后应用才启动。Aspire 片段把应用登记为项目资源，按绑定添加资源并把连接串注入 `XiHan__Bindings__<绑定名>__value`。

| 绑定类型 | Compose 镜像 | 注入的 `value` | Aspire |
| --- | --- | --- | --- |
| `connection:relational` | `postgres:17-alpine` | `Host=<绑定名>;Port=5432;Database=<应用名>;Username=xihan;Password=xihan` | `AddPostgres(<绑定名>).AddDatabase(<应用名>)` |
| `cache:distributed` | `redis:7-alpine` | `<绑定名>:6379` | `AddRedis(<绑定名>)` |
| `queue` | `rabbitmq:4-alpine` | `amqp://guest:guest@<绑定名>:5672` | `AddRabbitMQ(<绑定名>)` |
| `blob` | `minio/minio` | `http://<绑定名>:9000` | `AddContainer(<绑定名>, "minio/minio")` |

Compose 里的账号口令是本地联调用的固定开发值。生成的应用服务 healthcheck 是 `dotnet --version`，只说明运行时在，不探测 `/health/ready`；`image.aot` 为真时运行镜像里没有 `dotnet`，这条 healthcheck 不会通过。

## 资源绑定

`bindings` 在 Kubernetes 里按 Service Binding 规范落地：每个绑定一个 Secret（键 `type` 与 `uri`）挂到 `/bindings/<绑定名>`，并设 `SERVICE_BINDING_ROOT=/bindings`。内核的 `BindingResolver` 先读 `SERVICE_BINDING_ROOT` 下的目录，再读配置节 `XiHan:Bindings:<绑定名>`（子键 `value` / `type`，或节本身的标量值），同名时配置整体覆盖目录来源。所以同一个应用在 k8s 里读 Secret、在 Compose 里读环境变量，代码不变。`bindings.example.yaml` 只是 Secret 的样板，真实连接串由部署方另行创建，不进仓库。

## 探针与停机宽限

宿主的 `app.MapXiHanHealth()` 映射 `/health/startup`、`/health/ready`、`/health/live`（[生命周期与健康](./lifecycle)），生成的 Deployment 以它们做三个探针：

| 探针 | 路径 | `failureThreshold` |
| --- | --- | --- |
| startupProbe | `/health/startup` | ⌈`startupSeconds` ÷ `periodSeconds`⌉，示例为 6 |
| readinessProbe | `/health/ready` | 3 |
| livenessProbe | `/health/live` | 3 |

三个探针的 `periodSeconds` 与 `timeoutSeconds` 取清单的 `probes`。内核没有进入服务状态时 `/health/ready` 返回 503，插件或 Provider 健康条目降级只把应用压成 `Degraded`，不让探针失败。

停机宽限要两边对齐：`shutdown.graceSeconds` 写进 `XiHan:Kernel:StopTimeoutSeconds`（Dockerfile 与 Deployment 都以环境变量 `XiHan__Kernel__StopTimeoutSeconds` 设置），Deployment 的 `terminationGracePeriodSeconds` 取 `graceSeconds + 5`。kubelet 发 SIGTERM 后，内核在 `StopTimeoutSeconds` 内按与启动相反的顺序停止特性并完成 Draining，多出的 5 秒保证 kubelet 不会在内核收尾前 SIGKILL。进程外插件的停止宽限缺省也取 `XiHan:Kernel:StopTimeoutSeconds`（[插件](./plugins#握手与生命周期)）。示例里 `graceSeconds: 25` 生成 `XiHan__Kernel__StopTimeoutSeconds=25` 与 `terminationGracePeriodSeconds: 30`。

## 配置热更新

`config.files` 把列出的文件做成 ConfigMap（`<name>-config`）只读挂到 `mountPath`，并设两个环境变量：

| 环境变量 | 值 | 作用 |
| --- | --- | --- |
| `XIHAN_CONFIG_PATH` | `mountPath`（缺省 `/etc/xihan`） | 宿主的 `AddXiHan` 把这个目录里的 JSON 文件追加为 `reloadOnChange` 配置源 |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `1` | ConfigMap 以切换符号链接的方式更新，文件监视必须轮询 |

`Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 在装配内核之前处理这个目录：只取顶层 `*.json`，跳过以 `.` 开头的条目（ConfigMap 的 `..data` 之类），`appsettings.json` 最先装载，其余按文件名序数排序随后覆盖。这些源追加在构建器已有配置源之后，优先级高于 `appsettings.json`、环境变量与命令行参数。文件变化后 `IOptionsMonitor<T>` 随之更新，应用不需要重启：

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

kubelet 同步 ConfigMap 有延迟，示例里改完约一分钟内生效。要在 Native AOT 下照常绑定与重载，项目设 `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`（KubernetesApp 就这样做）。

## Lease 领导选举

多副本部署时，集群唯一的作业只应在一个 Pod 上运行。`leader.lease` 生成 ServiceAccount、只含该 Lease 的 Role（`coordination.k8s.io` 的 `leases`：对该名字 `get` / `update` / `patch`，另加 `create`）与 RoleBinding，并在 Deployment 里设：

| 环境变量 | 来源 |
| --- | --- |
| `XiHan__Scheduling__Kubernetes__Lease` | `leader.lease` |
| `XIHAN_POD_NAME` | Pod 字段 `metadata.name`，作为 Lease 持有者 |
| `POD_NAMESPACE` | Pod 字段 `metadata.namespace` |

应用在集群里装配 `KubernetesSchedulingFeature`（[Scheduling.Kubernetes](../packages/scheduling-kubernetes)），它以 Lease 顶替进程内选举，直接调用 API Server、不依赖客户端库。KubernetesApp 按 `KUBERNETES_SERVICE_HOST` 是否存在决定是否装配，本地运行退回 Full 组合自带的进程内选举：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Scheduling.Kubernetes;

var builder = WebApplication.CreateBuilder(args);

// API Server 的环境变量只在 Pod 里存在
var inCluster = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;

builder.AddXiHan(xihan =>
{
    xihan.UseFullProfile();
    if (inCluster)
    {
        xihan.UseFeature<KubernetesSchedulingFeature>();
    }
});

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.Run();
```

调度引擎与作业的写法见[调度与后台作业](./scheduling)。需要跨副本持久化作业与分发运行时换 `Scheduling.Database`。

## Native AOT 发布

内核与原语层零反射，序列化走源生成，标为 AOT 兼容的包可以随应用以 Native AOT 发布；不兼容的包（如 `Plugins.*`、EF Core、Redis、RabbitMQ、JWT 等 Provider）在各自包页的信息表里标明，选型见[序列化与 Native AOT](./aot)。`samples/SimpleApp` 是 AOT 冒烟示例，CI 每次都以 Native AOT 发布它：

```bash
# Linux 上先装原生工具链
sudo apt-get install -y clang zlib1g-dev
dotnet publish next/samples/SimpleApp/SimpleApp.csproj --configuration Release -o ./artifacts/aot
```

SimpleApp 做到可 AOT 发布的写法：项目文件声明 `<PublishAot>true</PublishAot>`，不在命令行传，以免这个全局属性传给 netstandard2.0 的分析器与生成器项目（NETSDK1207）；项目设 `<EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>`，把最小 API 的端点委托改为编译期生成；源生成的 `JsonSerializerContext` 插在 `ConfigureHttpJsonOptions` 解析链最前面；操作契约经 `Kernel.Generators` 在编译期生成登记代码与 JSON Schema（[Kernel.Generators](../packages/kernel-generators)）。容器里以 AOT 运行时，在应用清单里设 `"image": { "aot": true }`，生成的 Dockerfile 自动换成原生构建与 `runtime-deps` 镜像。

## 在 kind 上验证

`samples/KubernetesApp` 用 Full 组合演示探针、有序停机、ConfigMap 热更新与 Lease 领导选举；它的 `deploy/` 是 `xihan pack` 生成并提交的产物，测试 `SampleDeployTests.KubernetesSample_CommittedDeployFolder_MatchesItsManifest` 保证它与清单不漂移。在仓库根执行：

1. 本地运行，确认端点：

   ```bash
   dotnet run --project next/samples/KubernetesApp
   curl -s localhost:5100/config      # banner 来自 appsettings.json，configPath 为空
   curl -s localhost:5100/leader      # election 为 InMemoryLeaderElection，isLeader 为 true
   ```

2. 改了 `xihan.app.json` 后重新生成产物：

   ```bash
   dotnet run --project next/src/XiHan.Framework.Cli -- pack --manifest next/samples/KubernetesApp/xihan.app.json
   ```

3. 构建镜像（`build.context` 是 `next/`）并载入 kind：

   ```bash
   cd next
   docker build -f samples/KubernetesApp/deploy/Dockerfile -t ghcr.io/xihanfun/kubernetes-app:5.0.0-preview.1 .
   kind load docker-image ghcr.io/xihanfun/kubernetes-app:5.0.0-preview.1
   ```

4. 部署并转发端口：

   ```bash
   kubectl apply -k samples/KubernetesApp/deploy/kustomize
   kubectl rollout status deployment/kubernetes-app
   kubectl port-forward service/kubernetes-app 8080:8080
   ```

5. 逐项验证：

   | 项 | 做法 | 预期 |
   | --- | --- | --- |
   | 探针 | `kubectl describe pod <pod>` | startup `/health/startup`（`failureThreshold: 6`），readiness `/health/ready`，liveness `/health/live` |
   | 有序停机 | `kubectl delete pod <pod>` 后看日志 | 内核在 25 秒内按逆序停止特性；`terminationGracePeriodSeconds: 30`，kubelet 不提前 SIGKILL |
   | 热更新 | `kubectl edit configmap kubernetes-app-config`，改 `Sample.Banner` | 约一分钟内 `curl localhost:8080/config` 返回新横幅，Pod 不重启 |
   | 领导选举 | `kubectl get lease kubernetes-app-scheduler -o yaml`，对两个 Pod 各请求 `/leader` | 只有持有者 `isLeader: true` 且 `ticks` 增长；删除持有者 Pod 后约 15 秒内另一个 Pod 接管，`leaseTransitions` 加一 |

## 相关

- [Cli 命令行工具](../packages/cli)
- [生命周期与健康](./lifecycle)、[配置与选项](./configuration)、[调度与后台作业](./scheduling)、[序列化与 Native AOT](./aot)
- [Profiles.Full](../packages/profiles-full)、[Scheduling.Kubernetes](../packages/scheduling-kubernetes)、[Hosting.AspNetCore](../packages/hosting-aspnetcore)
- 规范：RFC-0016《应用清单与打包》
