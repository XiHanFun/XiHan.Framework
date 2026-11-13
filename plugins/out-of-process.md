来源：https://framework.docs.xihanfun.com/plugins/out-of-process

# 进程外插件

进程外插件是宿主启动的子进程，经本机 gRPC 按协议 `xihan.plugin.v1` 与宿主通信。这一页讲协议的组成、握手与生命周期、插件声明的操作如何成为宿主的代理操作并经完整管道执行、事件投递、回调、失败降级，然后用 Go SDK 写一个能跑的插件，最后讲远程 Provider 与配置节。读完你能写一个任意语言的插件并接进 .NET 宿主。前置知识是[隔离级别与清单](/plugins/overview)。

## 为什么需要

进程内扩展与宿主同生共死：一个未处理异常让整个应用退出，依赖冲突在加载时才暴露，而且只能用 .NET 写。把插件放进独立进程后，崩溃只影响插件本身，宿主按退避重启它；插件带自己的依赖；只要有 protobuf、gRPC 与 JSON 的语言都能实现。框架把这套约定写成规范 RFC-0017，.NET 宿主 `Plugins.OutOfProcess` 与 Go SDK 是它的两个实现，一致性套件保证不同语言的插件行为一致。

## 协议 xihan.plugin.v1 概述

协议定义在规范仓库的 `plugin/plugin.proto`，分两个服务：

| 服务 | 方法 | 用途 |
| --- | --- | --- |
| `Plugin`（宿主调插件） | `Handshake` | 协商协议版本、核对原语层大版本与清单，交换操作声明（契约与 JSON Schema）、订阅与消息目录 |
| | `Configure` / `Start` / `Stop` | 生命周期；`Configure` 下发清单声明的配置节与绑定 |
| | `Health` / `WatchHealth` | 健康轮询与推送 |
| | `Invoke` / `InvokeStream` | 调用插件声明的操作；.NET 宿主暂不发布流式操作 |
| | `HandleEvent` | 投递插件订阅的消息 |
| `Host`（插件调宿主） | `Invoke` / `Publish` | 回调：以插件身份调用宿主操作、发布消息 |

业务载荷是 JSON，上下文按 RFC-0003 平铺为元数据（`traceparent`、截止时间、只含已登记 `xihan.*` 键的 Baggage）。.NET 侧的协议常量（环境变量名、元数据键、`PLUGINS_*` 错误码）在 [Plugins.Protocol](/packages/plugins-protocol) 包里，应用通常不直接引用。

## 握手与生命周期

宿主只继承必要的环境变量（`PATH`、用户与临时目录、区域与时区、`DOTNET_*`，以及 Go 运行时的 `GOMAXPROCS`、`GOGC`、`GOMEMLIMIT`、`GODEBUG`、`GOTRACEBACK` 等；不按 `GO` 前缀继承，`GOOGLE_*` 凭据不交给插件，ADR-0208），再追加下表的变量启动进程，工作目录是插件目录：

| 环境变量 | 含义 |
| --- | --- |
| `XIHAN_PLUGIN_PROTOCOL_VERSIONS` | 宿主支持的协议版本，逗号分隔、升序，当前为 `1` |
| `XIHAN_PLUGIN_PRIMITIVES` | 宿主的原语层大版本 |
| `XIHAN_PLUGIN_SESSION` | 本次启动的会话令牌：宿主调用插件时、插件回调宿主时都放在元数据 `xihan-plugin-session` |
| `XIHAN_PLUGIN_HOST_ADDRESS` | 宿主回调端点 `<network> <address>`（Windows 命名管道，其他系统 Unix 域套接字） |
| `XIHAN_PLUGIN_INSTANCE` | 实例 ID |
| `XIHAN_PLUGIN_LOCALE` | 宿主默认区域 |

插件启动 gRPC 服务端后向 stdout 写一行握手行 `xihan-plugin/1 <network> <address>`（`network` 为 `unix`、`tcp` 或 `npipe`，`tcp` 只能是回环地址），宿主连上后调用 `Handshake`：取双方都支持的最高协议版本（没有交集时插件以 gRPC `FailedPrecondition` 拒绝），核对原语层大版本，比对清单，交换声明。stdin 在插件停止前保持打开，插件必须在 stdin 关闭时自行退出；stderr 是插件日志，宿主逐行转成来源 `plugins.<清单 ID>` 的日志，以 `{` 开头的 JSON 行按结构化日志处理。

生命周期状态与原语层的 `LifecycleState` 一致：

| 状态 | 进入时机 |
| --- | --- |
| `Declared` | 宿主读到清单 |
| `Resolved` | 握手成功，声明与清单一致 |
| `Configured` | `Configure` 成功 |
| `Ready` | `Start` 成功；之后按健康推送或轮询更新 |
| `Degraded` | 插件自报 `Degraded`，或任一健康项为 Degraded / Unhealthy |
| `Draining` | 宿主已发 `Stop`，等待插件完成在途调用 |
| `Stopped` | 正常停止 |
| `Failed` | 握手、配置、启动失败，进程异常退出，健康自报 `Failed`，停止超时被终止 |

时限与重启的缺省值：

| 环节 | 缺省 | 配置键（`XiHan:Plugins:OutOfProcess`） |
| --- | --- | --- |
| 握手（读到握手行并完成 `Handshake`） | 10 秒，超时 `PLUGINS_TIMEOUT_001` | `HandshakeTimeoutSeconds` |
| `Configure` 与 `Start` 各自 | 30 秒 | `StartTimeoutSeconds` |
| 健康 | 先订阅 `WatchHealth` 推送，断开后每 10 秒一次 `Health`；连续 3 次失败视为失效 | `HealthIntervalSeconds`、`HealthFailures` |
| 崩溃后重启 | 1 秒起翻倍、上限 60 秒的退避 | — |
| 重启上限 | 10 分钟窗口内 5 次，超过后保持 `Failed`，直到 `UnloadAsync` + `LoadAsync` | `RestartLimit`、`RestartWindowSeconds` |
| 停止宽限 | 取 `XiHan:Kernel:StopTimeoutSeconds`（缺省 30 秒） | `StopGraceSeconds` |

停止时宿主发 `Stop`（带宽限秒数与原因 `shutdown` / `unload` / `restart`），在宽限内等进程退出；超时先发终止信号（关闭 stdin，Unix 上同时发 SIGTERM），5 秒内仍未退出才终止整个进程树，两种结局都记为 `Failed`（`PLUGINS_TIMEOUT_001`）。宿主停止时按与登记相反的顺序停止插件。重启后是新进程、新会话令牌、重新握手，声明必须与首次一致，否则 `PLUGINS_VALIDATION_004`。插件失败只让应用进入 `Degraded`，宿主不退出。

## 最小示例

用 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 写一个回声插件。SDK 的 `plugin.Serve` 包掉协议的全部机械部分：读环境变量、监听、写握手行、实现服务端、生命周期与健康推送、事件投递、宿主回调客户端和 stderr 结构化日志。

第一步，写清单 `xihan.manifest.json`。不写 `contributes.pipeline`：只有 Wasm 插件能贡献中间件，SDK 读到非空即报错不启动，宿主也以 `PLUGINS_VALIDATION_001` 拒绝。

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.contoso.echo",
  "package": "contoso/echo",
  "version": "1.0.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "requires": [ { "id": "kernel.plugins", "version": "1" } ],
  "contributes": { "operations": [ "echoes.create" ] },
  "activation": [ "onStartup" ],
  "entry": "echo"
}
```

第二步，写入口。用 `operation.Add` 登记操作，用 `plugin.LoadCatalog` 加载消息目录（目录 `Localization/` 下一种区域一个 JSON 文件，至少要有 `en-US.json`），交给 `plugin.Serve`：

```go
package main

import (
	"context"
	"embed"
	"log"

	"github.com/xihanfun/framework-sdk-go/invocation"
	"github.com/xihanfun/framework-sdk-go/operation"
	"github.com/xihanfun/framework-sdk-go/plugin"
	"github.com/xihanfun/framework-sdk-go/primitives"
)

// 握手时原样发送，所以构建产物目录里的 xihan.manifest.json 必须是同一份
//
//go:embed xihan.manifest.json
var manifest []byte

//go:embed Localization
var localization embed.FS

type CreateRequest struct {
	Text string `json:"text"`
}

type CreateResponse struct {
	Text   string `json:"text"`
	Tenant string `json:"tenant,omitempty"`
}

// 回声文本并附上下文里的租户；租户、主体、关联 ID 都来自宿主传来的 Baggage
func create(ctx *invocation.Context, request CreateRequest) primitives.Result[CreateResponse] {
	return primitives.Ok(CreateResponse{Text: request.Text, Tenant: ctx.Tenant()})
}

func main() {
	catalog, err := plugin.LoadCatalog("echo", localization, "Localization")
	if err != nil {
		log.Fatal(err)
	}

	registry := operation.NewRegistry()
	contract := operation.Contract{ID: "echoes.create", Stability: primitives.Experimental, Expose: []string{operation.ExposeHTTP}}
	if err := operation.Add(registry, contract, operation.HandlerFunc[CreateRequest, CreateResponse](create), nil, ""); err != nil {
		log.Fatal(err)
	}

	// 日志只能走 stderr：stdout 留给握手行
	if err := plugin.Serve(context.Background(), plugin.Options{Manifest: manifest, Operations: registry, Catalogs: []plugin.Catalog{catalog}}); err != nil {
		log.Fatal(err)
	}
}
```

第三步，构建并把清单放进产物目录，再用规范仓库的黑盒运行器验证协议一致性（[一致性套件与门禁](/testing/conformance)）：

```bash
go build -o bin/echo .
cp xihan.manifest.json bin/
go install github.com/xihanfun/framework-spec/conformance/cmd/xihan-conformance@latest
xihan-conformance plugin --dir bin --sample 'echoes.create={"text":"hi"}'
```

第四步，在宿主的 `XiHan:Plugins:OutOfProcess:Directories` 里指向 `bin`（宿主装配见[隔离级别与清单](/plugins/overview)）。启动后 `POST /echoes` 带 `{"text":"hi"}` 得到带租户的回声，`/health/ready` 里出现 `plugins.xihan.contoso.echo`。

更完整的写法见 SDK 的 `examples/echo`（仓库里的 `samples/EchoGoPlugin` 就是它在 .NET 宿主里的端到端运行）：订阅、生命周期回调与健康项经 `plugin.Options` 的 `Subscriptions`、`Hooks`、`Catalogs` 接入；`Hooks.Configure` 里用 `configuration.Decode(&settings)` 读清单声明的配置节；操作里用 `plugin.HostOf(ctx)` 取回调客户端；业务失败返回 `primitives.Fail`，文案只带消息键。SDK 不提供流式操作，不覆盖 Wasm 载体。用 .NET 写进程外插件不引用任何 XiHan 包，只需 protobuf 与 gRPC 从 `plugin.proto` 生成桩，见 `samples/EchoPlugin`。

## 代理操作经完整管道

握手声明的每个操作在宿主的 `OperationRegistry` 里登记为代理操作，归属是插件清单 ID，契约（`stability`、`expose`、权限码或策略）与 Schema 取自声明。代理操作和本地[操作契约](/web/operations)没有区别：协议适配器按 `expose` 发布，调用经宿主完整[管道](/startup/pipeline)，Authorize 阶段按契约判定，Handle 阶段把 JSON 载荷与上下文发给插件。

上下文平铺为元数据：`traceparent` / `tracestate`、截止时间（同时设为 gRPC 调用的截止时间）、只含已登记 `xihan.*` 键的 Baggage；`Items` 与服务不传播。插件返回的业务失败原样成为调用结果，宿主用握手时拿到的消息目录翻译消息键（[全球化和本地化](/common/localization)）；传输层失败按下表映射：

| 插件侧 | 宿主错误码 |
| --- | --- |
| 插件未就绪、正在停止、进程已退出或不可达；进行中的调用被插件停机切断 | `PLUGINS_UNAVAILABLE_001` |
| 截止时间到（宿主的 `DeadlineMiddleware` 按截止令牌一律改报超时，见[弹性与限流](/common/resilience)） | `KERNEL_TIMEOUT_001` |
| 截止时间到达之前调用方取消 | `KERNEL_CANCELLED_001` |
| 插件未实现该操作（gRPC `Unimplemented`） | `KERNEL_NOTFOUND_001` |
| 载荷或帧序不合法（gRPC `InvalidArgument`） | `KERNEL_VALIDATION_001` |
| 其他 gRPC 失败 | `PLUGINS_UNEXPECTED_001` |

## 事件投递

握手里的每个订阅让宿主按它的 `mode` 向消息代理订阅该主题：空或 `competing` 是竞争消费（订阅了主题的宿主实例里只有一个把消息交给插件），`broadcast` 是广播（每个在线的宿主实例都交给自己的插件，见[发布与订阅](/messaging/pubsub)）；`mode` 不合法或同一（主题，类型）以两种方式各声明一次是 `PLUGINS_VALIDATION_004`。消息到达时派发器开新作用域、经完整管道，在 Handle 阶段把信封（CloudEvents 属性平铺，`attempt` 从 1 起）发给插件；订阅写了消息类型时只投递类型相同的消息。投递带这次的订阅方式 `mode`（`competing` 或 `broadcast`），插件按（主题，方式）找处理器再看类型：同一主题的不同类型可以各用一种方式，一条消息以两种方式各到一次时，各交给自己方式的处理器。投递至少一次，插件处理器必须幂等，可用 Baggage 里的 `xihan.idempotency-key` 或信封 `id` 去重；插件返回失败或不可用时由代理按自己的重试与死信策略处理，宿主不缓冲消息。宿主要装配一个消息代理，Standard 组合已带内存默认实现。

## 回调：Host.Invoke 与 Host.Publish

插件可以反过来调用宿主：

| 回调 | RPC | 权限（须在清单声明且已授予） | 语义 |
| --- | --- | --- | --- |
| 调用操作 | `Host.Invoke` | `capability:kernel.pipeline` | 以新的调用经宿主完整管道执行操作，未登记的操作为 `KERNEL_NOTFOUND_001` |
| 发布消息 | `Host.Publish` | `capability:messaging.queue` | 按主题分组批量经宿主消息发布器发布，逐项返回结果；宿主没有发布器时每项为 `PLUGINS_UNAVAILABLE_001` |

回调必须带 `xihan-plugin-session` 元数据，令牌缺失或不匹配为 gRPC `Unauthenticated`（`PLUGINS_DENIED_002`）；令牌只对本次进程有效。反方向同样：宿主调用插件的每一次 RPC（含 `Handshake`）都带这个令牌，插件必须按常数时间比较校验，缺失或不匹配以 `Unauthenticated` 拒绝，本机别的进程连上插件端点也冒充不了宿主（ADR-0202；.NET 写法见 `samples/EchoPlugin/SessionGuard.cs`，Go SDK 的校验随它的下一个版本提供）。两端的 Unix 域套接字都放在新建的私有目录里（`0700`，名字随机，已存在即不用），套接字文件 `0600`；命名管道只允许当前用户；TCP 只绑定回环地址，那里的隔离只靠令牌。权限未声明或未授予为 gRPC `PermissionDenied`，尾部元数据 `xihan-error-code: PLUGINS_DENIED_002`。回调以插件身份执行（主体 ID 是清单 ID、认证方案 `Plugin`），元数据里的 `xihan.principal` 被忽略，其余已登记的 Baggage 照常采用。回调与发布只能带宿主交给插件的租户：宿主调用插件时元数据里的 `xihan.tenant` 是这次调用的租户（Tenant 阶段裁定过时取裁定结果），回调元数据的 `xihan.tenant` 与发布信封的 `xihantenant` 必须是宿主此刻交给该插件处理的某个调用的租户，否则为 `PLUGINS_DENIED_002`；不带租户的回调是无租户调用（ADR-0195）。插件并发处理多个租户的调用时，回调可以带其中任何一个；原样转发收到的元数据即可。Authorize 阶段按目标操作的契约判定，所以插件主体还要在[授权与权限](/security/authorization)里被授予相应权限码。

## 失败降级

插件故障的结果是降级而不是退出：每个插件是一个健康条目 `plugins.<清单 ID>`，`Failed` 或 `Degraded` 只把应用压成 `Degraded`，`/health/ready` 仍按宿主自己的就绪规则应答（[生命周期与健康](/startup/lifecycle)）。

| 故障 | 插件状态 | 调用方看到 |
| --- | --- | --- |
| 进程异常退出 | `Failed`（`PLUGINS_UNEXPECTED_002`），按退避重启 | 在途调用 `PLUGINS_UNAVAILABLE_001` |
| 调用超过截止时间 | 不变 | `KERNEL_TIMEOUT_001` |
| 健康探测连续失败 | 进程被终止，按崩溃处理 | 重启前 `PLUGINS_UNAVAILABLE_001` |
| 重启次数超过上限 | 保持 `Failed` | `PLUGINS_UNAVAILABLE_001` |

`OutOfProcessPluginHost.FindStatus(清单 ID)` 返回诊断快照 `Maybe<OutOfProcessPluginStatus>`（未加载时无值）：状态、进程 ID、协议版本、重启次数、是否懒激活。

## 远程 Provider

子系统能力也可以由进程外插件提供（规范 RFC-0019）：宿主侧的 [Caching.Remote](/packages/caching-remote) 适配器声明 `caching.distributed@1`，插件只实现规范规定的操作集 `cache-entries.*`（单条与批量的读、写、不存在才写、删），适配器把它接成 `ICacheStore`、共享缓存、分布式锁与幂等中间件，和 Redis Provider 一样参与装配。Go 写的 `samples/CacheGoPlugin` 是现成实现：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutOfProcessPluginsFeature>()
    .UseFeature<RemoteCachingFeature>());
```

```json
{
  "XiHan": {
    "Plugins": { "OutOfProcess": { "Directories": [ "plugins/cache-go" ] } },
    "Caching": { "Remote": { "TimeoutMilliseconds": 5000 } }
  }
}
```

适配器每次调用带超时，插件不可达映射为统一的不可用错误；「适配器 + 插件」整体通过与 Redis 相同的缓存一致性套件。用别的语言写 Provider 都走这条路，见[写一个 Provider](/extending/provider)。

## 配置节

配置节 `XiHan:Plugins:OutOfProcess`（`OutOfProcessPluginOptions.ConfigurationSection`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Directories` | 字符串数组 | 空 | 启动时装载的插件目录，一个目录一个插件；相对路径相对当前目录 |
| `Grants:<清单 ID>` | 字符串数组 | 空授予 | 授予该插件的权限；清单声明的权限必须全部授予才能加载。清单要读的宿主配置节与绑定也在这里授予：`config:<配置节>`、`binding:<绑定名>`，没有授予即拒绝加载 |
| `HandshakeTimeoutSeconds` | 正数 | 10 | 读到握手行并完成 `Handshake` 的时限 |
| `StartTimeoutSeconds` | 正数 | 30 | `Configure` 与 `Start` 各自的时限 |
| `StopGraceSeconds` | 正数 | 取 `XiHan:Kernel:StopTimeoutSeconds` | 停止宽限 |
| `HealthIntervalSeconds` | 正数 | 10 | 健康推送断开后的轮询间隔 |
| `HealthFailures` | 正整数 | 3 | 连续多少次健康轮询失败视为进程失效 |
| `RestartLimit` | 非负整数 | 5 | `RestartWindowSeconds` 内最多自动重启多少次 |
| `RestartWindowSeconds` | 正数 | 600 | 统计重启次数的时间窗 |
| `WorkDirectory` | 字符串 | 临时目录下的 `xihan-plugins` | 插件工作目录的根，每个插件一个子目录 |

取值不合类型与范围时，`OutOfProcessPluginOptions.FromConfiguration` 以带配置键的 `ArgumentException` 拒绝启动。插件自己的配置节由插件清单 `contributes.config.section` 决定，宿主在 `Configure` 时把它序列化为 JSON 对象下发。

## 常见问题

### 插件进程起来了，宿主却报握手超时

宿主要在 `HandshakeTimeoutSeconds` 内读到 stdout 的握手行并完成 `Handshake`。stdout 与 stderr 的一行最多 16384 个字符，超过的部分被丢弃，握手行超过即握手失败（ADR-0185）。常见原因：日志写到了 stdout（只能走 stderr）、`tcp` 地址不是回环地址、握手发回的清单与目录里的文件不一致。`xihan-conformance plugin --dir <目录>` 的 `handshake` 用例能直接定位。

### 插件崩溃后多久恢复，一直崩会怎样

从 1 秒起翻倍退避重启，上限 60 秒；`RestartWindowSeconds` 内超过 `RestartLimit` 次就保持 `Failed`，调用方一直得到 `PLUGINS_UNAVAILABLE_001`，直到 `UnloadAsync` 再 `LoadAsync`。

### 插件的操作能不能像本地操作一样出现在 OpenAPI 与 MCP 里

能。配置阶段发现的插件，其操作在协议适配器发布路由与文档时已经在登记表里（[规范化接口文档](/web/openapi)、[MCP](/protocols/mcp)）。只有运行期 `LoadAsync` 的插件例外：路由已经发布完，它的操作只对 `IOperationInvoker` 可见。

### 能用 gRPC 流式操作吗

协议定义了 `InvokeStream`，但 .NET 宿主暂不发布流式操作，Go SDK 也不提供。需要流式结果时拆成多次一元调用或经消息投递。

## 相关包与下一步

- 包参考：[Plugins.OutOfProcess](/packages/plugins-outofprocess)、[Plugins.Protocol](/packages/plugins-protocol)、[Caching.Remote](/packages/caching-remote)、[Conformance](/packages/conformance)
- 相关章节：[隔离级别与清单](/plugins/overview)、[操作契约](/web/operations)、[发布与订阅](/messaging/pubsub)、[授权与权限](/security/authorization)、[一致性套件与门禁](/testing/conformance)
- 规范：RFC-0017《进程外插件协议》、RFC-0019《远程 Provider》；Go SDK [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go)
- 下一步：[Wasm 插件](/plugins/wasm)讲沙箱、ABI 与插件贡献中间件
