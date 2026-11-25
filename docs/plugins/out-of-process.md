# 进程外插件

进程外插件是宿主启动的子进程，经本机 gRPC 按协议 `xihan.plugin.v1` 与宿主通信。这一页讲协议的组成、握手与生命周期、插件声明的操作如何成为宿主的代理操作并经完整管道执行、事件投递、回调、失败降级，然后用 Go SDK 写一个能跑的插件，最后讲远程 Provider 与配置节。读完你能写一个任意语言的插件并接进 .NET 宿主。前置知识是[隔离级别与清单](/plugins/overview)。

## 为什么需要

进程内扩展与宿主同生共死：一个未处理异常让整个应用退出，依赖冲突在加载时才暴露，而且只能用 .NET 写。把插件放进独立进程后，崩溃只影响插件本身，宿主按退避重启它；插件带自己的依赖；只要有 protobuf、gRPC 与 JSON 的语言都能实现。框架把这套约定写成规范 RFC-0017，.NET 宿主 `Plugins.OutOfProcess` 与 Go SDK 是它的两个实现，一致性套件保证不同语言的插件行为一致。

## 协议 xihan.plugin.v1 概述

协议定义在规范仓库的 `plugin/plugin.proto`，分两个服务：

| 服务 | 方法 | 用途 |
| --- | --- | --- |
| `Plugin`（宿主调插件） | `Handshake` | 协商协议版本、核对原语层大版本与清单，交换操作声明（契约与 JSON Schema）、订阅与消息目录 |
| | `Configure` / `Start` / `Stop` | 生命周期；`Configure` 下发清单声明的配置节与绑定（名字、类型、主值与属性） |
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
| `XIHAN_PLUGIN_HOST_ADDRESS` | 宿主回调端点，一个或多个、按宿主的偏好排序、以 `;` 分隔：`<network> <address>[;<network> <address>]…`。.NET 宿主在 Windows 上给命名管道加回环 TCP，其他系统给 Unix 域套接字加回环 TCP（临时目录的路径含空白或 `;` 时只有回环 TCP）；插件取第一个自己的 gRPC 库能连的 |
| `XIHAN_PLUGIN_INSTANCE` | 实例 ID |
| `XIHAN_PLUGIN_LOCALE` | 宿主默认区域 |

插件启动 gRPC 服务端后向 stdout 写一行握手行 `xihan-plugin/1 <network> <address>`（`network` 为 `unix`、`tcp` 或 `npipe`；地址是一个或多个非空白字符，空白与 .NET 正则的 `\s` 相同，路径里的中文用户名可以出现；`tcp` 只能是写法固定的回环地址：`127.0.0.0/8` 的点分十进制且不带前导零，或 `[::1]`，端口 1–65535、不带前导零，`127.1`、`0177.0.0.1` 都不合法），宿主连上后调用 `Handshake`：取双方都支持的最高协议版本（没有交集时插件以 gRPC `FailedPrecondition` 拒绝），核对原语层大版本，比对清单，交换声明。stdin 在插件停止前保持打开，插件必须在 stdin 关闭时自行退出；stderr 是插件日志，宿主逐行转成来源 `plugins.<清单 ID>` 的日志，以 `{` 开头的 JSON 行按结构化日志处理。

`HandshakeRequest.host_endpoint` 是端点列表的第一项，只供诊断，插件选回调端点以环境变量为准。环境变量缺失、不合法或没有一项能连时插件照常服务，启动时在 stderr 记一条警告，每次回调都以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `no supported callback endpoint`）失败、不发请求。

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

## 插件一侧的规则

不管用哪个 SDK 还是直接手写 gRPC 服务端（`samples/EchoPlugin`），插件对宿主调用的处理都按规范的同一套规则（`plugin/lifecycle.md` §7、`plugin/invocation.md` §1、§4，ADR-0237），黑盒套件逐条核对：

- **调用次序**：`Handshake` 只在 `Declared`、`Configure` 只在 `Resolved`、`Start` 只在 `Configured` 时接受，`Stop` 在 `Declared`、`Draining`、`Stopped` 之外都接受；其余组合（重复握手、没握手就配置、配置两次、没配置就启动、启动两次、没握手就停止、停止中再停止）以 gRPC `FailedPrecondition` 拒绝、什么都不做。会话令牌先于次序核对，元数据里必须恰好一个 `xihan-plugin-session` 值。
- **配置**：`configuration` 为空等同 `{}`；不是 JSON 对象（或通不过共同的 JSON 读取检查）时插件进入 `Failed`，`ConfigureResponse.status` 是 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为 `configure`）。`bindings` 里每个绑定带 `name`、`type`、主值 `value` 与属性表 `properties`：属性名区分大小写、原样交给作者，没有属性时是空表，只给了属性的绑定 `value` 为空，`type` 总是清单声明的类型；清单标为必需（`required` 缺省 `true`）的绑定一定在请求里，没有它宿主不会发 `Configure`。SDK 不把主值与属性值写进日志、错误与诊断输出，交给作者的绑定类型的调试输出只带绑定名、类型与属性名。gRPC 服务端的接收上限不得小于 4194304 字节（各 gRPC 库的缺省值），宿主保证 `ConfigureRequest` 不超过 2097152 字节。
- **就绪与健康**：启动回调成功后立即求一次健康，按结果进入 `Ready`、`Degraded` 或 `Failed` 再应答 `Start`（健康回调报其他状态的按 `Ready`，抛出的按 `Degraded`）；只有 `Ready` 与 `Degraded` 可服务。`Start` 之前的 `Health` 报本地状态（`Declared`、`Resolved`、`Configured`），没有健康项。可服务期间每次 `Health` 都求一次健康回调，报 `Ready`、`Degraded`、`Failed` 的照办、报其他状态的不改变状态；回调抛出时这一次是 `Degraded`（`error` 为 `plugins.process.failed`，`phase` 为 `health`，没有健康项），状态也迁到 `Degraded`。健康项名不在清单 `contributes.health` 里的丢掉；静态声明的健康项名在启动前就核对。
- **调用与投递**：`Invoke` 先看操作是否声明（否则 gRPC `Unimplemented`），再看是否可服务（否则 `PLUGINS_UNAVAILABLE_001` + `plugins.process.unavailable`）。`HandleEvent` 没有信封、`mode` 不是空、`competing`、`broadcast`，或信封的 `time` 不是空串却读不出（SDK 按 RFC-0003 规则 8 读成该语言的时间类型）时为 gRPC `InvalidArgument`；找不到（主题，方式，类型）的订阅为 `KERNEL_NOTFOUND_001` + `plugins.event.unsubscribed`；匹配多个订阅时按声明顺序依次交给处理器，第一个失败即为结果。`InvokeStream` 在流式契约之前一律不实现：第一帧不是 `start` 为 `InvalidArgument`，是 `start` 为 `Unimplemented`。
- **错误的写出**：带消息键的错误 `text` 留空，由宿主按目录翻译成调用方的语言；运行时自己的消息键（`plugins.process.*`、`plugins.event.unsubscribed`、`plugins.host.unavailable`、`plugins.callback.*`）由宿主的目录提供，SDK 不随握手发送。
- **回调客户端**：发出之前依次核对：有没有可用的回调端点（没有为 `plugins.host.unavailable`）、权限（清单声明且已授予，否则 `plugins.callback.denied`）、上下文的租户（否则 `plugins.callback.tenant_denied`），然后回调上下文的截止时间已过或已被取消时也不发请求，结果分别按 `DeadlineExceeded` 与「调用方已取消的 `Cancelled`」映射；`Host.Invoke` 的载荷以字符串给出时最后还要严格编码成 UTF-8，有孤立的代理项即不发请求，结果是 `KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`（`id` 为操作 ID，`reason` 为 `$`），与宿主读到不是 JSON 的载荷相同，不换成 U+FFFD 或 `?` 照发（`plugin/invocation.md` §5，ADR-0312）。以字节给出的载荷原样发出，由宿主核对；六个 SDK 里收字符串载荷的是 TypeScript 的 `Host.invoke` 与 Java 的 `OperationRequest.of(String, String)`。gRPC 库按截止时间在本端中止调用时报的状态（如 tonic 的 `Cancelled: Timeout expired`）按 `DeadlineExceeded`，映射之外不再按时钟或令牌改写结果。
- **停止与收尾**：启动回调没成功过时 `Stop` 不调停止回调、应答后以退出码 0 退出；启动过的进入 `Draining`、等在途调用最长到宽限期（`0` 按 5 秒）再调停止回调。stdin 读到结尾与 SIGTERM 都是收尾请求：立即取消在途调用，最多 3 秒后以退出码 0 退出。

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

第四步，在宿主的 `XiHan:Plugins:OutOfProcess:Directories` 里指向 `bin`（宿主装配见[隔离级别与清单](/plugins/overview)）。启动后 `POST /echoes` 带 `{"text":"hi"}` 得到带租户的回声，健康详情端点（`MapXiHanHealthDetails()` 映射的 `/health/details`）里出现 `plugins.xihan.contoso.echo`。

更完整的写法见 SDK 的 `examples/echo`（仓库里的 `samples/EchoGoPlugin` 就是它在 .NET 宿主里的端到端运行）：订阅、生命周期回调与健康项经 `plugin.Options` 的 `Subscriptions`、`Hooks`、`Catalogs` 接入；`Hooks.Configure` 里用 `configuration.Decode(&settings)` 读清单声明的配置节；操作里用 `plugin.HostOf(ctx)` 取回调客户端；业务失败返回 `primitives.Fail`，文案只带消息键。SDK 不提供流式操作，不覆盖 Wasm 载体。用 .NET 写进程外插件不引用任何 XiHan 包，只需 protobuf 与 gRPC 从 `plugin.proto` 生成桩，见 `samples/EchoPlugin`。

## 代理操作经完整管道

握手声明的每个操作在宿主的 `OperationRegistry` 里登记为代理操作，归属是插件清单 ID，契约（`stability`、`expose`、权限码或策略）与 Schema 取自声明。代理操作和本地[操作契约](/web/operations)没有区别：协议适配器按 `expose` 发布，调用经宿主完整[管道](/startup/pipeline)，Authorize 阶段按契约判定，Handle 阶段把 JSON 载荷与上下文发给插件。

上下文平铺为元数据：`traceparent` / `tracestate`、截止时间（同时设为 gRPC 调用的截止时间）、只含已登记 `xihan.*` 键的 Baggage；`Items` 与服务不传播。宿主为每次调用插件的操作与每次事件投递记一条 Span `xihan.plugin.invoke`（种类 Client，是这次调用的 `xihan.invoke` 的子 Span，标签 `xihan.plugin.id`，操作调用另带 `xihan.operation`），写给插件的 `traceparent` 以它为父级，插件一侧的 Span 与回调都挂在它下面，见[日志与可观测性](/common/observability#内核发出什么)。插件返回的业务失败原样成为调用结果，宿主用握手时拿到的消息目录翻译消息键（[全球化和本地化](/common/localization)）。读入插件写来的 `Error` 时（宿主读插件、插件读宿主同一规则，ADR-0237）：错误码不合文法的整个换成 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 一律为 `invoke`，原来的 `inner` 丢掉）；带消息键的错误不取 `text`；`details` 为空、只有空白、是空对象、文本里有孤立的代理项（严格编码成 UTF-8 不成，ADR-0301）、通不过共同的 JSON 读取检查、不是对象，或任何一层有读成双精度会溢出的数时当作没有细节；`ok` 为假却没有 `error`（或整个 `status` 缺失）是 `plugins.process.failed`。传输层失败按下表映射：

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
| 发布消息 | `Host.Publish` | `capability:messaging.queue` | 以元数据建调用上下文（主体同样是插件），在这个上下文里按主题分组批量经宿主消息发布器发布：信封缺的租户、主体、关联 ID、区域与追踪由发布器从上下文补齐，与进程内的发布一致；逐项返回结果，逐项依次核对发布器（没有时 `PLUGINS_UNAVAILABLE_001`）、主题、信封的 `xihantenant` 与转换；信封的 `time` 是空串（没有时间）或带秒与偏移的 RFC 3339 时刻（`2026-11-25T03:25:00Z`、`2026-11-25T11:25:00.123+08:00`，与消息信封、截止时间同一条文法，读成 UTC），不合文法的那一项以 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid_time`（参数 `time` 是原文）不发布；`data_schema`（字段 11）是信封的 `dataschema`，发布与投递两个方向都原样传递，空串表示没有，只有空白的那一项以 `messaging.envelope.invalid` 不发布。同一协议版本里新增的字段，较早规范的宿主不认它，插件发布到那样的宿主上时丢掉 `dataschema`、其余照常 |

回调必须恰好带一个 `xihan-plugin-session` 元数据值，令牌缺失、不匹配或带了两个值为 gRPC `Unauthenticated`（`PLUGINS_DENIED_002`）；令牌只对本次进程有效，宿主的各个回调端点认同一套令牌。回调的截止时间取元数据 `deadline`（按规范的文法严格读，读不出当作没有）与 gRPC 调用自身截止时间中较早的一个；`deadline` 不为空而读不出时回调照常执行，宿主记一条带插件清单 ID 与原文长度的 Warning（不带原文，插件回调这一类 1 分钟最多一条，与请求头的告警分开计）。反方向同样：宿主调用插件的每一次 RPC（含 `Handshake`）都带这个令牌，插件必须按常数时间比较校验，缺失或不匹配以 `Unauthenticated` 拒绝，本机别的进程连上插件端点也冒充不了宿主（ADR-0202；.NET 写法见 `samples/EchoPlugin/SessionGuard.cs`，六个语言 SDK 的插件运行时都已内置这项校验）。两端的 Unix 域套接字都放在新建的私有目录里（`0700`，名字随机，已存在即不用），套接字文件 `0600`；宿主的命名管道只允许当前用户并拒绝远程客户端；插件的命名管道应同样如此，运行时给不了访问控制表或拒绝远程客户端的标志时（Node 的 libuv 两样都给不了，不用 unsafe 的 Rust 只能拒绝远程客户端）用系统缺省的安全描述符，其他用户只能以只读打开、发不出请求，需要只接受本机连接的改用回环 TCP；TCP 只绑定回环地址，那里的隔离只靠令牌。权限未声明或未授予为 gRPC `PermissionDenied`，尾部元数据 `xihan-error-code: PLUGINS_DENIED_002`。回调以插件身份执行（主体 ID 是清单 ID、认证方案 `Plugin`），元数据里的 `xihan.principal` 被忽略，其余已登记的 Baggage 照常采用。回调与发布只能带宿主交给插件的租户：宿主调用插件时元数据里的 `xihan.tenant` 是这次调用的租户（Tenant 阶段裁定过时取裁定结果），回调元数据的 `xihan.tenant` 与发布信封的 `xihantenant` 必须是宿主此刻交给该插件处理的某个调用的租户，否则为 `PLUGINS_DENIED_002`（`Host.Publish` 的元数据越界时整批不发布，每一项都是这个错误）；不带租户的回调是无租户调用（ADR-0195）。插件并发处理多个租户的调用时，回调可以带其中任何一个；原样转发收到的元数据即可。Authorize 阶段按目标操作的契约判定，所以插件主体还要在[授权与权限](/security/authorization)里被授予相应权限码。

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

配置节 `XiHan:Plugins:OutOfProcess`（`OutOfProcessPluginOptions.ConfigurationSection`）里最常用的是两项：`Directories` 列出启动时装载的插件目录（一个目录一个插件，相对路径相对当前目录），`Grants:<清单 ID>` 授予该插件的权限，清单声明的权限必须全部授予才能加载，清单要读的宿主配置节与绑定也在这里授予（`config:<配置节>`、`binding:<绑定名>`）。握手与启停时限、健康轮询、重启上限与工作目录等其余键见[包参考](/packages/plugins-outofprocess#配置)。

取值不合类型与范围时，`OutOfProcessPluginOptions.FromConfiguration` 以带配置键的 `ArgumentException` 拒绝启动。插件自己的配置节由插件清单 `contributes.config.section` 决定，宿主在 `Configure` 时把它序列化为 JSON 对象下发。清单声明并授予的资源绑定随同一个请求下发：来源与宿主自己的特性相同（Service Binding 目录与 `XiHan:Bindings`），每次配置插件时重新解析，带名字、清单声明的类型、主值与全部属性（只挂属性文件、没有主值的绑定照常下发、主值为空）。下发之前宿主按清单核对：必需（`required` 缺省 `true`）的绑定没给或只给了类型、来源声明的类型与清单不同、属性名或大小不合下发的上限、绑定目录多于 128 个文件（`reason` 为 `binding '<绑定名>' has more than 128 files`）、整个请求按 protobuf 编码超过 2 MiB，这个插件都配置失败、进入 `Failed` 并按重启策略重试；可以没有的绑定在清单写 `"required": false`。规则与 `reason` 见[资源绑定](/configuration/bindings#下发给插件的绑定)。

## 常见问题

### 插件进程起来了，宿主却报握手超时

宿主要在 `HandshakeTimeoutSeconds` 内读到 stdout 的握手行并完成 `Handshake`。stdout 与 stderr 的一行最多 16384 个字符，超过的部分被丢弃，握手行超过即握手失败（ADR-0185）。常见原因：日志写到了 stdout（只能走 stderr）、`tcp` 地址不是回环地址、握手发回的清单与目录里的文件不一致。`xihan-conformance plugin --dir <目录>` 的 `handshake` 用例能直接定位。

### 插件的操作能不能像本地操作一样出现在 OpenAPI 与 MCP 里

能。HTTP 路由、OpenAPI 文档与 MCP tool 跟随操作登记表（ADR-0251），配置阶段发现的与运行期 `LoadAsync` 的插件一样：登记之后就有路由、进文档、是 tool，卸载之后随之消失（[规范化接口文档](/web/openapi)、[MCP](/protocols/mcp)）。要给插件单独一份分组文档，应用登记一个 `IOpenApiGroupSource`，按插件的清单 ID 给出分组。gRPC 方法与 AI 工具目录只在启动时建成，运行期加载的插件不在其中。

## 相关包与下一步

- 包参考：[Plugins.OutOfProcess](/packages/plugins-outofprocess)、[Plugins.Protocol](/packages/plugins-protocol)、[Caching.Remote](/packages/caching-remote)、[Conformance](/packages/conformance)
- 相关章节：[隔离级别与清单](/plugins/overview)、[操作契约](/web/operations)、[发布与订阅](/messaging/pubsub)、[授权与权限](/security/authorization)、[一致性套件与门禁](/testing/conformance)
- 规范：RFC-0017《进程外插件协议》、RFC-0019《远程 Provider》；Go SDK [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go)
- 下一步：[Wasm 插件](/plugins/wasm)讲沙箱、ABI 与插件贡献中间件
