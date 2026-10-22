# RFC-0016 应用清单与打包

> 状态：已采纳 | 日期：2026-10-23 | 对应 ADR：— | 参考实现：`XiHan.Framework.Cli`（`xihan pack`） | 规范性文本：`spec/packaging/app-manifest.md`、`spec/packaging/app.schema.json`

## 摘要

应用在仓库里放一份 `xihan.app.json` 描述自己：名字、项目、Profile、镜像、协议端口、资源绑定、配置挂载、探针、停机宽限、资源与伸缩、领导租约。`xihan pack` 只读这份清单，生成可直接使用的部署产物：Dockerfile、Kustomize 清单、Compose 文件与 .NET Aspire AppHost 登记代码。清单是唯一输入；产物每次整份重新生成，不手改。

## 清单

字段与约束见 `spec/packaging/app-manifest.md`；JSON Schema 见 `spec/packaging/app.schema.json`。要点：

- `name` 是 DNS 标签（`^[a-z][a-z0-9-]{0,62}$`），同时作为镜像仓库名、k8s 资源名与 Compose 项目名的默认值。
- `bindings` 的类型限定为 `connection:relational`、`cache:distributed`、`queue`、`blob`，与清单 `bindings` 的资源类型一致（RFC-0002）。每个绑定对应一个 k8s Secret（按 Service Binding 规范挂到 `SERVICE_BINDING_ROOT`）、一个 Compose 依赖服务与一个 Aspire 资源。
- `shutdown.graceSeconds` 同时写进 `XiHan:Kernel:StopTimeoutSeconds` 与 `terminationGracePeriodSeconds`（后者多 5 秒，让内核先完成 Draining 再被 kubelet 终止）。
- `probes` 映射为 startup / readiness / liveness 探针，路径固定为宿主的 `/health/startup`、`/health/ready`、`/health/live`。
- `config` 把列出的文件做成 ConfigMap 挂到 `mountPath`，并设环境变量 `XIHAN_CONFIG_PATH`；宿主按该目录装载并监听文件（P3-12 实现）。
- `leader.lease` 为调度器的 Lease 名：生成 ServiceAccount 与只含 `coordination.k8s.io/leases` 的 Role，并设 `XiHan:Scheduling:Kubernetes:Lease`（`Scheduling.Kubernetes` 随 P3-12 提供）。

## 产物

| 产物 | 路径 | 说明 |
| --- | --- | --- |
| Dockerfile | `Dockerfile`、`.dockerignore` | 多阶段：`sdk:10.0` 发布 → `aspnet:10.0-noble-chiseled`（AOT 时 `runtime-deps:10.0-noble-chiseled` 并在构建阶段装 clang）；非 root（`USER $APP_UID`）；Kestrel 端点与停机超时以环境变量写入 |
| Kustomize | `kustomize/*.yaml` | Deployment（探针、宽限、安全上下文、只读根文件系统、绑定与配置挂载）、Service、ConfigMap、HPA（有 `scaling` 时）、PDB（副本大于 1 时）、ServiceAccount + Role + RoleBinding（有 `leader` 时）、`bindings.example.yaml`（Secret 示例，不在 kustomization 里） |
| Compose | `compose.yaml` | 应用服务 + 每个绑定一个依赖服务（PostgreSQL、Redis、RabbitMQ、MinIO），以 `XiHan__Bindings__<name>__type/value` 注入 |
| Aspire | `aspire/XiHanAppHost.g.cs` | `Add<Name>(this IDistributedApplicationBuilder)`：按绑定添加资源并把连接串注入 `XiHan__Bindings__<name>__value` |

## 命令

`xihan pack [--manifest <path>] [--output <dir>] [--only dockerfile,kustomize,compose,aspire]`。默认清单 `./xihan.app.json`，默认输出清单所在目录下的 `deploy/`。成功时逐行输出写出的文件（相对输出目录），退出码 0；清单不存在 → `CLI_NOTFOUND_001`（3），不合法 → `CLI_VALIDATION_001`（2，`detail` 列出全部问题）。

`xihan graph` / `pipeline` / `operations` / `invoke` 转发给应用自己的命令行宿主（`dotnet run --project <project> -- <command> …`），项目取 `--project`，否则取当前目录 `xihan.app.json` 的 `project`，否则交给 `dotnet run` 自己找。

## 错误码

见 `conventions/error-codes.md` 中 `CLI_*`。
