# KubernetesApp

Full 组合在 Kubernetes 上的落地示例（P3-12）：探针、优雅停机、ConfigMap 热更新、Lease 领导选举。`deploy/` 由 `xihan pack` 从 `xihan.app.json` 生成并提交，`Cli.Tests` 保证它与清单不漂移。

## 本地运行

```bash
dotnet run --project next/samples/KubernetesApp          # http://localhost:5100
curl -s localhost:5100/config                            # banner 来自 appsettings.json，configPath 为空
curl -s localhost:5100/leader                            # election = InMemoryLeaderElection，isLeader = true，ticks 每 10 秒加一
```

本地没有 API Server 环境变量，程序不装配 `KubernetesSchedulingFeature`，退回进程内选举。

## 部署到集群（kind）

```bash
cd next
docker build -f samples/KubernetesApp/deploy/Dockerfile -t ghcr.io/xihanfun/kubernetes-app:5.0.0-preview.1 .   # build.context 指向 next/，镜像里只有发布产物
kind load docker-image ghcr.io/xihanfun/kubernetes-app:5.0.0-preview.1
kubectl apply -k samples/KubernetesApp/deploy/kustomize
kubectl rollout status deployment/kubernetes-app
kubectl port-forward service/kubernetes-app 8080:8080
```

## 四项验证

| 项 | 做法 | 预期 |
| --- | --- | --- |
| 探针 | `kubectl describe pod` | startup 探针 `/health/startup`（60 秒内 6 次），readiness `/health/ready`，liveness `/health/live`；内核未进入服务状态时 `/health/ready` 返回 503 |
| 优雅停机 | `kubectl delete pod <leader>` 后看日志 | 内核在 `XiHan__Kernel__StopTimeoutSeconds=25` 内按逆序停止特性；`terminationGracePeriodSeconds: 30` 比它多 5 秒，kubelet 不会提前 SIGKILL |
| 热更新 | `kubectl edit configmap kubernetes-app-config`，改 `Sample.Banner` | 约 1 分钟内（kubelet 同步周期）`curl localhost:8080/config` 返回新横幅；靠 `XIHAN_CONFIG_PATH=/etc/xihan` + `DOTNET_USE_POLLING_FILE_WATCHER=1` |
| 领导选举 | `kubectl get lease kubernetes-app-scheduler -o yaml`；对两个 Pod 各 `curl /leader` | 只有持有者 `isLeader: true` 且 `ticks` 增长；删除持有者 Pod 后 15 秒内另一个 Pod 接管，`leaseTransitions` 加一 |

## 清单到产物的对应

`xihan.app.json` 里 `shutdown.graceSeconds: 25` 同时写进 Dockerfile 的 `ENV XiHan__Kernel__StopTimeoutSeconds` 与 Deployment 的 `terminationGracePeriodSeconds: 30`；`leader.lease` 生成 ServiceAccount、只含该 Lease 的 Role 与 `XiHan__Scheduling__Kubernetes__Lease`；`config.files` 生成 ConfigMap 并挂到 `/etc/xihan`；`scaling` 生成 HPA、`replicas: 2` 生成 PDB。完整映射见 `next/spec/packaging/app-manifest.md`。

重新生成：`dotnet run --project next/src/XiHan.Framework.Cli -- pack --manifest next/samples/KubernetesApp/xihan.app.json`。
