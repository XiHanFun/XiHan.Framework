# 应用清单 `xihan.app.json`

> 规范性。参考实现：`XiHan.Framework.Cli`（`xihan pack`）。Schema：`app.schema.json`。

## 字段

| 字段 | 必填 | 默认 | 约束 |
| --- | --- | --- | --- |
| `name` | 是 | — | `^[a-z][a-z0-9-]{0,62}$`，不以 `-` 结尾 |
| `project` | 是 | — | 相对清单所在目录的项目文件，必须存在 |
| `profile` | 否 | `standard` | `minimal` / `standard` / `full`，信息性（写进标签） |
| `image.repository` | 否 | `name` | 镜像仓库（可含注册表前缀） |
| `image.tag` | 否 | `latest` | 镜像标签 |
| `image.aot` | 否 | `false` | 为真时 Native AOT 发布，运行镜像为 `runtime-deps` |
| `ports.http` | 否 | `8080` | 1–65535 |
| `ports.grpc` | 否 | — | 1–65535，且不等于 `http`；有值时暴露 h2c 端点 |
| `bindings.<name>.type` | 是（有绑定时） | — | `connection:relational` / `cache:distributed` / `queue` / `blob` |
| `bindings.<name>.secret` | 否 | `<app>-<name>` | k8s Secret 名 |
| `config.mountPath` | 否 | `/etc/xihan` | 绝对路径 |
| `config.files` | 否 | `[]` | 文件名（无目录分隔符），存在时内容进 ConfigMap，否则为 `{}` |
| `probes.startupSeconds` | 否 | `60` | ≥ `periodSeconds` |
| `probes.periodSeconds` | 否 | `10` | ≥ 1 |
| `probes.timeoutSeconds` | 否 | `2` | ≥ 1 |
| `shutdown.graceSeconds` | 否 | `30` | ≥ 1 |
| `resources.requests` / `limits` | 否 | — | `cpu`、`memory` 为 k8s 数量（`500m`、`512Mi`） |
| `replicas` | 否 | `1` | ≥ 1 |
| `scaling.min` / `max` / `cpuPercent` | 否 | — | `min` ≥ 1，`max` ≥ `min`，`cpuPercent` 1–100；有值时生成 HPA |
| `leader.lease` | 否 | — | DNS 标签；有值时生成 Lease 的 RBAC |
| `environment` | 否 | `{}` | 环境变量，键 `^[A-Za-z_][A-Za-z0-9_]*$` |

绑定名与 `environment` 的键都区分大小写；绑定名 `^[a-z][a-z0-9-]*$`。

## 映射

| 清单 | Dockerfile | Kustomize | Compose | Aspire |
| --- | --- | --- | --- | --- |
| `ports` | `ENV Kestrel__Endpoints__Http__Url`（gRPC 另加 `Protocols=Http2`）、`EXPOSE` | 容器端口 `http` / `grpc`，Service 同名端口 | `ports` | `WithHttpEndpoint` |
| `shutdown.graceSeconds` | `ENV XiHan__Kernel__StopTimeoutSeconds` | 同左 + `terminationGracePeriodSeconds = graceSeconds + 5` | `stop_grace_period` | — |
| `probes` | — | startup（`failureThreshold = ⌈startup / period⌉`）、readiness、liveness | `healthcheck`（`/health/ready`） | — |
| `bindings` | — | Secret 挂载 `/bindings/<name>`（`type`、`uri` 两个键），`SERVICE_BINDING_ROOT=/bindings` | 依赖服务 + `XiHan__Bindings__<name>__type/value` | 资源 + `WithEnvironment("XiHan__Bindings__<name>__value", 资源)` |
| `config` | — | ConfigMap 挂载 `mountPath`，`XIHAN_CONFIG_PATH`，`DOTNET_USE_POLLING_FILE_WATCHER=1`（ConfigMap 以符号链接切换更新，须轮询监视） | — | — |
| `resources` | — | `resources` | — | — |
| `replicas` / `scaling` | — | `replicas`（有 `scaling` 时取 `min`）、HPA、PDB（副本 > 1） | — | `WithReplicas` |
| `leader.lease` | — | ServiceAccount、Role（leases）、RoleBinding，`XiHan__Scheduling__Kubernetes__Lease`，`XIHAN_POD_NAME`、`POD_NAMESPACE`（Pod 字段） | — | — |
| `environment` | — | `env` | `environment` | `WithEnvironment` |

绑定类型在 Compose 与 Aspire 里的对应：

| 类型 | Compose 镜像 | `value` | Aspire |
| --- | --- | --- | --- |
| `connection:relational` | `postgres:17-alpine` | `Host=<name>;Port=5432;Database=<app>;Username=xihan;Password=xihan` | `AddPostgres(<name>).AddDatabase(<app>)` |
| `cache:distributed` | `redis:7-alpine` | `<name>:6379` | `AddRedis(<name>)` |
| `queue` | `rabbitmq:4-alpine` | `amqp://guest:guest@<name>:5672` | `AddRabbitMQ(<name>)` |
| `blob` | `minio/minio` | `http://<name>:9000` | `AddContainer(<name>, "minio/minio")` |

## 安全基线

容器以非 root 运行（`USER $APP_UID`，`runAsNonRoot`），`allowPrivilegeEscalation: false`，丢弃全部能力，`seccompProfile: RuntimeDefault`，根文件系统只读（`/tmp` 为 emptyDir）。
