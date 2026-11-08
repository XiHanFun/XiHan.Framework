来源：https://framework.docs.xihanfun.com/deployment/pack

# 应用清单与 xihan pack

这一页讲怎样用应用清单 `xihan.app.json` 描述一个可部署的应用，再用命令行工具 `xihan pack` 生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，以及资源绑定、消息代理与 Native AOT 在打包时要定下的事。读完你能从一份清单得到能构建、能本地联调、能部署到 Kubernetes 的全套文件。前置知识是[配置目录与热更新](/configuration/config-directory)与[资源绑定](/configuration/bindings)。

## 为什么需要

部署一个应用要写 Dockerfile、Deployment、Service、ConfigMap、探针、停机宽限、HPA、RBAC，本地联调还要 Compose 或 Aspire 配置。这些文件里的端口、超时、挂载路径互相引用，手写容易漂移：Deployment 的 `terminationGracePeriodSeconds` 比内核的停机宽限短，kubelet 就会在收尾前杀掉进程。框架把这些信息收进一份应用清单（规范 RFC-0016），`xihan pack` 只读这份清单、每次整份重新生成全部产物，不手改。它与能力包的清单 `xihan.manifest.json` 是两回事：后者描述一个包提供与依赖什么能力，前者描述一个可部署的应用。

## 应用清单 xihan.app.json

应用在项目旁放一份 `xihan.app.json`：名字、项目、组合包、镜像、端口、资源绑定、配置挂载、探针、停机宽限、资源与伸缩、领导租约。仓库示例 `samples/KubernetesApp/xihan.app.json`：

```json
{
  "$schema": "https://spec.xihanfun.com/packaging/app.v1.json",
  "name": "kubernetes-app",
  "project": "KubernetesApp.csproj",
  "profile": "full",
  "build": { "context": "../.." },
  "image": { "repository": "ghcr.io/xihanfun/kubernetes-app", "tag": "<版本>" },
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
| `build.context` | 否 | `.` | Docker 构建上下文，相对清单所在目录；Dockerfile 里的项目路径与 Compose 的 `context` / `dockerfile` 都相对它 |
| `image.repository` | 否 | `name` | 镜像仓库，可带注册表前缀 |
| `image.tag` | 否 | `latest` | 镜像标签 |
| `image.aot` | 否 | `false` | 为真时以 Native AOT 发布，运行镜像为 `runtime-deps` |
| `ports.http` | 否 | `8080` | 1–65535 |
| `ports.grpc` | 否 | — | 1–65535，不等于 `http`；有值时多暴露一个 h2c 端点 |
| `bindings.<name>.type` | 有绑定时必填 | — | `connection:relational` / `cache:distributed` / `queue` / `blob` / `stream` / `configuration:remote`；绑定名 `^[a-z][a-z0-9-]*$` |
| `bindings.<name>.secret` | 否 | `<name 字段>-<绑定名>` | k8s Secret 名 |
| `config.mountPath` | 否 | `/etc/xihan` | 绝对路径 |
| `config.files` | 否 | `[]` | 文件名（不含目录分隔符），文件存在时内容进 ConfigMap，否则为 `{}` |
| `probes.startupSeconds` / `periodSeconds` / `timeoutSeconds` | 否 | `60` / `10` / `2` | `startupSeconds` 不小于 `periodSeconds`，后两者至少 1 |
| `shutdown.graceSeconds` | 否 | `30` | 至少 1 |
| `resources.requests` / `limits` | 否 | — | `cpu`、`memory`，k8s 数量写法（`500m`、`512Mi`） |
| `replicas` | 否 | `1` | 至少 1 |
| `scaling.min` / `max` / `cpuPercent` | 否 | — | `min` 至少 1，`max` 不小于 `min`，`cpuPercent` 1–100；有值时生成 HPA |
| `leader.lease` | 否 | — | DNS 标签；有值时生成 Lease 的 RBAC |
| `environment` | 否 | `{}` | 环境变量，键 `^[A-Za-z_][A-Za-z0-9_]*$` |

清单不合法时 `xihan pack` 以 `CLI_VALIDATION_001` 失败（退出码 2），`detail` 一次列出全部问题；清单不存在为 `CLI_NOTFOUND_001`（退出码 3）。

## 安装 xihan 工具

`xihan` 是 .NET 工具包 `XiHan.Framework.Cli`，全局安装：

```bash
dotnet tool install --global XiHan.Framework.Cli --prerelease
xihan version
```

不安装也可以从仓库源码运行（在仓库根执行）：

```bash
dotnet run --project framework/src/XiHan.Framework.Cli -- pack --manifest framework/samples/KubernetesApp/xihan.app.json
```

`pack` 之外的命令（`graph`、`pipeline`、`invoke`、`mcp`、`setup`）见 [xihan 工具](/agents/cli)。

## 最小示例

在项目旁写一份最小清单，只给必填项与一个绑定：

```json
{
  "$schema": "https://spec.xihanfun.com/packaging/app.v1.json",
  "name": "orders",
  "project": "Orders.csproj",
  "bindings": { "db": { "type": "connection:relational" } },
  "config": { "files": ["appsettings.Production.json"] }
}
```

在清单所在目录执行：

```bash
xihan pack                                        # 读 ./xihan.app.json，写到 ./deploy/
xihan pack --output deploy/orders --only dockerfile,kustomize
```

成功时逐行输出写出的文件（相对输出目录），最后一行是文件数与输出目录，退出码 0。`deploy/` 下会出现 `Dockerfile`、`Dockerfile.dockerignore`、`kustomize/`（Deployment、Service、ConfigMap、`bindings.example.yaml`、`kustomization.yaml`）、`compose.yaml` 与 `aspire/XiHanAppHost.g.cs`。接下来 `docker build -f deploy/Dockerfile .` 构建镜像，`docker compose -f deploy/compose.yaml up` 在本地连同 PostgreSQL 一起启动，`kubectl apply -k deploy/kustomize` 部署到集群（Secret 按样板另行创建）。

| 选项 | 缺省 | 说明 |
| --- | --- | --- |
| `--manifest` | `./xihan.app.json` | 应用清单路径 |
| `--output` | 清单所在目录下的 `deploy/` | 输出目录，产物整份覆盖写入（UTF-8 无 BOM、LF 换行） |
| `--only` | 全部 | 逗号分隔的产物种类 `dockerfile`、`kustomize`、`compose`、`aspire`，不区分大小写；不认识的种类为 `CLI_VALIDATION_001` |

`pack` 只读清单与它引用的文件（项目、构建上下文、`config.files`），不构建项目、不调用 Docker 或 kubectl。

## 生成的产物

| 产物 | 路径 | 内容 |
| --- | --- | --- |
| Dockerfile | `Dockerfile`、`Dockerfile.dockerignore` | 多阶段构建；BuildKit 读与 Dockerfile 同目录的同名忽略文件，忽略 `bin/`、`obj/`、`deploy/`、`.git/` |
| Kustomize | `kustomize/*.yaml` | Deployment、Service、ConfigMap、HPA（有 `scaling` 时）、PDB（副本大于 1 时）、ServiceAccount + Role + RoleBinding（有 `leader` 时）、`kustomization.yaml`；有绑定时另有不在 kustomization 里的 `bindings.example.yaml` |
| Compose | `compose.yaml` | 应用服务，加每个绑定一个依赖服务 |
| Aspire | `aspire/XiHanAppHost.g.cs` | `Add<Name>(this IDistributedApplicationBuilder)` 扩展方法，放进 AppHost 项目调用 |

### Dockerfile

生成的 Dockerfile 以官方 .NET SDK 镜像发布，运行阶段用 chiseled 的 `aspnet` 镜像（没有 shell 与包管理器），以 `USER $APP_UID` 非 root 运行；Kestrel 端点与内核停止超时以环境变量写入。chiseled 镜像里没有 `curl` 与 `wget`，构建阶段因此从内联源码编译一个就绪探针放到 `/xihan/healthprobe`，供 Compose 的 healthcheck 使用。示例的 Dockerfile 主干（探针部分略去，镜像标签以 `<版本>` 代替）：

```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:<版本> AS build
ARG TARGETARCH
# （就绪探针的内联源码与 dotnet publish 略）
WORKDIR /src
COPY . .
RUN dotnet publish "samples/KubernetesApp/KubernetesApp.csproj" --configuration Release --arch $TARGETARCH --output /app/publish --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:<版本>-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /xihan/healthprobe-src/publish /xihan/healthprobe
ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:8080
ENV Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:8081 Kestrel__Endpoints__Grpc__Protocols=Http2
ENV XiHan__Kernel__StopTimeoutSeconds=25
EXPOSE 8080 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "KubernetesApp.dll"]
```

### Kustomize

Deployment 带三个探针、停机宽限、Pod 与容器的安全上下文（`runAsNonRoot`、`seccompProfile: RuntimeDefault`、`allowPrivilegeEscalation: false`、丢弃全部能力、只读根文件系统，`/tmp` 是 emptyDir）、资源、配置挂载与绑定挂载；Service 暴露同名的 `http` / `grpc` 端口；`kustomization.yaml` 的 `images` 写入 `image.tag`。探针、热更新与领导选举在集群里的行为见 [Kubernetes](/deployment/kubernetes)。

Kustomize 产物只包含应用自己，不在集群里起依赖服务：六种绑定类型一律只生成 Secret 示例 `bindings.example.yaml`（键 `type` 与 `uri`）与挂载，数据库、缓存、消息与配置中心由部署方用 Helm Chart、Operator 或托管服务提供，再按示例创建绑定 Secret。

### Compose 与 Aspire

Compose 用于本地联调：`stop_grace_period` 取 `shutdown.graceSeconds`，每个绑定一个依赖服务，以 `XiHan__Bindings__<绑定名>__type` / `__value` 注入，依赖服务健康后应用才启动。Aspire 片段把应用登记为项目资源，按绑定添加资源并注入 `XiHan__Bindings__<绑定名>__value`。

| 绑定类型 | Compose 依赖服务 | 注入的 `value` | Aspire |
| --- | --- | --- | --- |
| `connection:relational` | PostgreSQL | `Host=<绑定名>;Port=5432;Database=<应用名>;Username=xihan;Password=xihan` | `AddPostgres(<绑定名>).AddDatabase(<应用名>)` |
| `cache:distributed` | Redis | `<绑定名>:6379` | `AddRedis(<绑定名>)` |
| `queue` | RabbitMQ | `amqp://guest:guest@<绑定名>:5672` | `AddRabbitMQ(<绑定名>)` |
| `blob` | RustFS（S3 兼容） | `http://<绑定名>:9000` | `AddContainer(<绑定名>, "rustfs/rustfs", …)`，注入它的 `api` 端点 |
| `stream` | Kafka（单节点 KRaft） | `<绑定名>:9092` | `AddKafka(<绑定名>)` |
| `configuration:remote` | Nacos（standalone） | `http://<绑定名>:8848` | `AddContainer(<绑定名>, "nacos/nacos-server", …)`，注入它的 `http` 端点 |

依赖服务的镜像标签由生成器固定，`image.tag` 之外的版本不在清单里。`blob` 的凭据不在主值里：两者都以固定的开发账号启动 RustFS，并把同一对值作为绑定属性 `XiHan__Bindings__<绑定名>__accessKey` / `__secretKey` 注入，覆盖 S3 配置节里的同名键；其余类型的凭据已在主值里，或依赖服务没开认证。这些账号口令都是本地联调用的固定开发值。依赖服务都不映射宿主机端口，要从宿主机访问时另加一份 override 文件声明 `ports`。

应用服务的 healthcheck 用镜像里的就绪探针（`dotnet /xihan/healthprobe/XiHanHealthProbe.dll <端口> /health/ready`）请求本机的 `/health/ready`，`interval`、`timeout`、`start_period` 分别取 `probes.periodSeconds`、`timeoutSeconds`、`startupSeconds`；探针发一次 HTTP/1.1 `GET`，2xx 退出 0，其余状态、连接失败或 10 秒无应答退出 1。Kubernetes 用 `httpGet` 探针，不使用它。RustFS 不会自动建桶，应用开 `XiHan:ObjectStorage:S3:EnsureBuckets` 或先自行建桶，并配好 `XiHan:ObjectStorage:S3:HealthBucket`（[ObjectStorage.S3](/packages/objectstorage-s3)）。

## 资源绑定

`bindings` 在 Kubernetes 里按 Service Binding 规范落地：每个绑定一个 Secret（键 `type` 与 `uri`）挂到 `/bindings/<绑定名>`，并设 `SERVICE_BINDING_ROOT=/bindings`。内核的绑定解析先读 `SERVICE_BINDING_ROOT` 下的目录，再读配置节 `XiHan:Bindings:<绑定名>`（子键 `value` / `type`，或节本身的标量值），同名时配置整体覆盖目录来源。所以同一个应用在 k8s 里读 Secret、在 Compose 里读环境变量，代码不变（[资源绑定](/configuration/bindings)）。

六种类型与框架 Provider 声明的绑定一一对应，清单里的绑定名要写成 Provider 读取的这些名字，Provider 才取得到注入的值：

| 绑定名 | 类型 | Provider |
| --- | --- | --- |
| `db` | `connection:relational` | 持久化 Provider（EF Core、SqlSugar） |
| `cache` | `cache:distributed` | `Caching.Redis` |
| `queue` | `queue` | `Messaging.RabbitMQ` |
| `objectstorage` | `blob` | `ObjectStorage.S3` |
| `stream` | `stream` | `Messaging.Kafka` |
| `nacos` | `configuration:remote` | `Configuration.Nacos` |

`bindings.example.yaml` 只是 Secret 的样板，真实连接串由部署方另行创建，不进仓库；样板以注释列出各类型可单独给出的凭据键（`cache`、`queue`、`nacos` 的 `username` / `password`，`blob` 的 `accessKey` / `secretKey`，`stream` 的 SASL 四个键），写进 Secret 就覆盖应用配置节里的同类键，凭据可以不放进连接串。

## 消息代理

多副本部署接 RabbitMQ 或 Kafka 时，下面两件事要在部署时定下来（[发布与订阅](/messaging/pubsub)、[队列与流](/messaging/streams)）。

实例标识：广播订阅给每个实例一个独占的队列（RabbitMQ）或消费组（Kafka），名字里带实例标识。缺省每次启动生成（机器名、进程 ID 与随机后缀），不用配置；要在代理的管理界面里认出实例，可以配 `XiHan:Messaging:InstanceId`，但必须每个同时在线的副本都不同，重复时 Kafka 的广播静默退化成竞争，RabbitMQ 后启动的副本启动失败。Kubernetes 里用下行 API 把 Pod 名设进去，不要写在副本共用的配置文件里：

```yaml
        env:
          - name: XiHan__Messaging__InstanceId
            valueFrom:
              fieldRef:
                fieldPath: metadata.name
```

Kafka 主题：框架不建主题。集群开着 `auto.create.topics.enable`（Compose 里的单节点依赖服务就是）时主题按需自动建出；生产集群关掉它时，主题不存在会让发布约 30 秒后失败（`MESSAGING_UNAVAILABLE_001`）。这类集群在部署时预先建好应用的主题、死信主题，以及框架自己发布的调度与工作流事件主题，清单见 [Messaging.Kafka](/packages/messaging-kafka)。

## Native AOT 发布

内核与原语层零反射，序列化走源生成，标为 AOT 兼容的包可以随应用以 Native AOT 发布；不兼容的包（`Plugins.*`、EF Core、Redis、RabbitMQ、JWT 等 Provider）在各自包页的信息表里标明，选型见[序列化与 Native AOT](/common/serialization)。`samples/SimpleApp` 是 AOT 冒烟示例：

```bash
# Linux 上先装原生工具链
sudo apt-get install -y clang zlib1g-dev
dotnet publish framework/samples/SimpleApp/SimpleApp.csproj --configuration Release -o ./artifacts/aot
```

做到可 AOT 发布的写法：项目文件声明 `<PublishAot>true</PublishAot>`，不在命令行传，以免这个全局属性传给分析器与生成器项目（NETSDK1207）；设 `<EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>` 把最小 API 的端点委托改为编译期生成；源生成的 `JsonSerializerContext` 插在 `ConfigureHttpJsonOptions` 解析链最前面；操作契约经 `Kernel.Generators` 在编译期生成登记代码与 JSON Schema。容器里以 AOT 运行时，在应用清单里设 `"image": { "aot": true }`：构建阶段先装 `clang` 与 `zlib1g-dev`，以 `--self-contained true -p:PublishAot=true` 发布，运行阶段换成 chiseled 的 `runtime-deps` 镜像，入口是原生可执行文件；就绪探针也以 Native AOT 发布，Compose 的 `test` 直接执行原生探针 `/xihan/healthprobe/XiHanHealthProbe`。

## 常见问题

### 改了清单之后要手改 deploy/ 里的文件吗

不要。重新执行 `xihan pack`，产物整份覆盖。需要环境差异（副本数、镜像标签、额外的 env）时用 Kustomize 的 overlay 叠在生成的 base 上，或用 Compose 的 override 文件，生成的文件保持原样。仓库示例的 `deploy/` 由测试保证与清单不漂移。

### 为什么 Kustomize 里没有数据库和 Redis

生成器给不出通用的正确答案：副本数、存储类、备份与升级策略都因环境而异。Kustomize 只出 Secret 样板 `bindings.example.yaml`，依赖服务由部署方用 Helm Chart、Operator 或托管服务提供。单节点的本地联调形态用 Compose 或 Aspire。

### 应用在 Compose 里读得到连接串，在集群里读不到

检查三件事：绑定名是否与 Provider 读取的名字一致（`db`、`cache`、`queue`、`objectstorage`、`stream`、`nacos`）；Secret 是否按 `bindings.example.yaml` 创建且名字与清单 `bindings.<name>.secret` 一致；Pod 里是否有 `SERVICE_BINDING_ROOT=/bindings` 与对应的挂载。

### `image.aot` 为真却构建失败

应用引用了不兼容 AOT 的包，或项目里还有反射序列化。先在本机 `dotnet publish` 看 AOT 分析警告，按[序列化与 Native AOT](/common/serialization)排除；`Plugins.*` 宿主无法进 AOT 应用。

## 相关包与下一步

- 包参考：[Cli](/packages/cli)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Profiles.Full](/packages/profiles-full)、[Messaging.Kafka](/packages/messaging-kafka)
- 相关章节：[配置目录与热更新](/configuration/config-directory)、[资源绑定](/configuration/bindings)、[生命周期与健康](/startup/lifecycle)、[序列化与 Native AOT](/common/serialization)、[xihan 工具](/agents/cli)
- 规范：RFC-0016《应用清单与打包》
- 下一步：[Kubernetes](/deployment/kubernetes)
