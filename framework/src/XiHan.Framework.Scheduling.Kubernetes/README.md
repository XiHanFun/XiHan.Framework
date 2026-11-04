# XiHan.Framework.Scheduling.Kubernetes

## 关于

`XiHan.Framework.Scheduling.Kubernetes` 用 Kubernetes 的 Lease 在多个 Pod 之间选出领导，替换 `Scheduling.InMemory` 的进程内选举。直接调用 API Server，不依赖 Kubernetes 客户端库。

## 主要特性

* 集群唯一的作业（`RunOnEveryInstance = false`）只在持有 Lease 的 Pod 上按计划运行
* Pod 正常停止时释放 Lease，另一个 Pod 随即接手
* 只替换领导选举，作业存储仍在各 Pod 内存里；需要共享存储与失联恢复请用 `Scheduling.Database`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Scheduling.Kubernetes --prerelease
```

### 使用

在 Pod 里启用特性，与 `Scheduling.InMemory`（`UseFullProfile()` 已含）一起使用：

```csharp
builder.AddXiHan(x => x.UseFullProfile().UseFeature<KubernetesSchedulingFeature>());
```

Lease 名缺省为 `xihan-scheduler`，可改：

```json
{ "XiHan": { "Scheduling": { "Kubernetes": { "Lease": "orders-scheduler" } } } }
```

Pod 的 ServiceAccount 需要 `coordination.k8s.io` 下 `leases` 的 `get`、`update`、`patch`、`create` 权限。

## 主要类型

此库提供的主要类型：

* `KubernetesSchedulingFeature`：启用 Kubernetes 领导选举的特性
* `KubernetesLeaderElection`：基于 Lease 的领导选举
* `KubernetesSchedulingOptions`：配置项，对应 `XiHan:Scheduling:Kubernetes`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/scheduling-kubernetes)

## 相关包

* 调度抽象与引擎：[XiHan.Framework.Scheduling.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Abstractions)
* 内存实现：[XiHan.Framework.Scheduling.InMemory](https://www.nuget.org/packages/XiHan.Framework.Scheduling.InMemory)
* 数据库实现：[XiHan.Framework.Scheduling.Database](https://www.nuget.org/packages/XiHan.Framework.Scheduling.Database)

## 反馈与贡献

`XiHan.Framework.Scheduling.Kubernetes` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
