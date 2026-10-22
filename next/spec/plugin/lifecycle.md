# 插件生命周期、健康与重启

> 规范性。协议 `xihan.plugin.v1`（`plugin.proto`，RFC-0017）。状态词汇与原语层 `LifecycleState`（RFC-0001）一致。

## 1. 状态机

| 状态 | 进入时机 |
| --- | --- |
| `Declared` | 宿主读到清单 |
| `Resolved` | 握手成功，清单与声明校验通过 |
| `Configured` | `Configure` 返回 `ok` |
| `Started` | `Start` 返回 `ok` |
| `Ready` | 首次 `Health` 或 `WatchHealth` 报 `Ready`（插件可直接在 `Start` 后报 `Ready`） |
| `Degraded` | 插件自报 `Degraded`，或任一健康项 `Degraded` / `Unhealthy` |
| `Draining` | 宿主已发 `Stop`，等待在途调用完成 |
| `Stopped` | 进程正常退出 |
| `Failed` | 握手 / 配置 / 启动失败、进程异常退出、健康报 `Failed`、`Stop` 超时被终止 |

合法迁移同 `conformance/vectors/primitives/lifecycle-transitions.json`；插件自报的状态只能是 `Started`、`Ready`、`Degraded`、`Draining`、`Failed`，其余由宿主裁决。

## 2. 配置与启动

1. `Configure`：`configuration` 是清单 `contributes.config.section` 节的 JSON 对象（宿主配置源经 `IConfiguration` 展开后再序列化；没有声明配置节为 `{}`）；`granted_permissions` 等于清单 `permissions`；`bindings` 只含清单声明的绑定；`work_directory` 是宿主为该插件创建的可写目录（宿主停止时不删除）。配置变更时宿主不重新调用 `Configure`，而是以 `reason: restart` 停止后重新启动进程。
2. `Start`：返回 `ok` 后宿主才登记代理操作与事件订阅；在此之前到达的调用按 `PLUGINS_UNAVAILABLE_001` 失败。
3. 每一步都带 `StartTimeout`（默认 30 秒）的截止时间；超时为 `PLUGINS_TIMEOUT_001`，宿主终止进程。

## 3. 激活

清单 `activation` 决定进程何时启动：

| 事件 | 行为 |
| --- | --- |
| `onStartup` | 宿主启动时启动，与特性启动并行；插件失败不阻止宿主进入 `Ready`，但宿主进入 `Degraded` |
| `onOperation:<操作 ID 或 * 通配>` | 首次调用匹配的操作时启动；调用在截止时间内等待插件 `Ready`，超时为 `KERNEL_TIMEOUT_001` |
| `onEvent:<主题或 * 通配>` | 首次有匹配主题的消息要投递时启动；投递在截止时间内等待 |

懒激活的插件在 `Ready` 之前，其代理操作与订阅已经登记（握手信息来自清单与上次成功握手的缓存；没有缓存时宿主在启动阶段做一次"探测启动"取得声明后立即 `Stop`）。5.0 不定义空闲停止。

## 4. 健康

- 宿主优先用 `WatchHealth` 订阅推送；连接断开时回退为每 `HealthInterval`（默认 10 秒）一次 `Health`，连续 `HealthFailures`（默认 3）次失败视为 `Failed`。
- 健康项名字必须在清单 `contributes.health` 里声明；宿主把每个健康项映射为宿主健康项 `plugins.<清单 ID>.<项名>`，插件整体映射为 `plugins.<清单 ID>`。
- 任一插件 `Failed` 或 `Degraded` → 宿主 `Degraded`；宿主永远不因插件而退出。`/health/ready` 的应答按宿主的就绪规则，不受懒激活插件影响。

## 5. 停止

1. 宿主调用 `Stop`（`grace_seconds` 默认等于宿主的 `XiHan:Kernel:StopTimeoutSeconds`，`reason` 为 `shutdown` / `unload` / `restart`），插件进入 `Draining`：拒绝新调用（`PLUGINS_UNAVAILABLE_001`）、完成在途调用、释放资源、返回、退出进程。
2. 宿主在宽限期内等待进程退出；超时先发 SIGTERM（Windows 上关闭进程），再 5 秒后强制终止，记为 `Failed`（`PLUGINS_TIMEOUT_001`）。
3. 宿主停止时按与启动相反的顺序停止插件，并等待全部结束再停止特性。

## 6. 崩溃与重启

- 进程异常退出（非 `Stop` 引起）记为 `Failed`（`PLUGINS_UNEXPECTED_002`，细节含退出码与 stderr 末尾若干行），宿主进入 `Degraded`；在途调用以 `PLUGINS_UNAVAILABLE_001` 失败。
- 重启退避：首次 1 秒，之后翻倍，上限 60 秒；插件连续 `Ready` 满 10 分钟后退避归零。`RestartLimit`（默认 5，按 10 分钟窗口计）内重启失败后停止重试，保持 `Failed`，直到宿主显式 `UnloadAsync` + `LoadAsync`。
- 重启后是新的进程、新的会话令牌、重新握手；宿主校验新握手的清单与上次一致，不一致按 `PLUGINS_VALIDATION_004` 失败。
- 事件订阅在插件不可用期间由宿主的消息代理按其重试与死信策略处理，宿主不缓冲消息。
