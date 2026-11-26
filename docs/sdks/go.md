# Go SDK

[framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 是 [XiHan Spec](/appendix/spec) 的 Go 实现：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时，以及 `caching.distributed@1` 远程 Provider 的插件侧。用它可以写在 .NET 宿主（[XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)）里运行的 Go 插件，也可以把同一套原语用在 Go 应用里。规范以 submodule 放在 SDK 仓库的 `spec/`，一致性由规范仓库的向量与黑盒运行器验证；.NET 是参考实现，SDK 的行为与它一致。宿主一侧怎样加载、配置插件与处理失败见[进程外插件](/plugins/out-of-process)。

## 安装

```bash
go get github.com/xihanfun/framework-sdk-go
```

需要的 Go 版本见 SDK 仓库的 [`go.mod`](https://github.com/XiHanFun/framework-sdk-go/blob/main/go.mod)。验证插件用的黑盒运行器在规范仓库：

```bash
go install github.com/xihanfun/framework-spec/conformance/cmd/xihan-conformance@latest
```

## 模块

| 包 | 内容 | 规范 |
| --- | --- | --- |
| `primitives` | `Error` / 错误码、`Result[T]`、`Maybe[T]`、`ID[Tag, V]`、`LifecycleState`、`PipelineStage`、`Stability`、Baggage 键、消息键与模板、`Catalog`、`FrameworkError` | RFC-0001、RFC-0010、`conventions/` |
| `manifest` | `xihan.manifest.json` 的模型、读取、规范化输出与校验，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `invocation` | `Context`（嵌入 `context.Context`）：取消与截止时间、W3C 追踪、已登记的 Baggage、对象袋 | RFC-0003 |
| `pipeline` | 十一阶段的 `Builder`：`Use` / `UseAt` / `UseNamedInstance`、`Before` / `After`、`Describe` / `Dump`、`Build` | RFC-0004 |
| `operation` | `Contract`、`Schema`、`Handler[Req, Res]`、`Registry`、经完整管道执行的 `Invoker`（XGI：JSON 请求与响应）与类型化入口 `operation.Invoke` | RFC-0011 |
| `plugin` | 进程外插件运行时 `Serve`（协议 `xihan.plugin.v1`）、宿主回调客户端 `Host`、宿主可解析的 JSON 日志、消息目录 | RFC-0017、`plugin/` |
| `plugin/v1` | 由 `spec/plugin/plugin.proto` 生成的协议代码（`buf generate`） | `plugin/plugin.proto` |
| `caching` | `caching.distributed@1` 远程 Provider 的插件侧：`Store` 是六个原子存储操作加三个批量操作，`MemoryStore` 是有界的进程内实现，`RegisterOperations` 把任何 `Store` 暴露成操作集，让 .NET 宿主的 Caching.Remote 适配器把这个插件当作分布式缓存 Provider | RFC-0019、RFC-0006、`provider/caching-distributed.md` |
| `examples/echo`、`examples/cache` | 回声插件（操作、订阅、健康、配置、宿主回调）与远程缓存 Provider 示例；可执行入口在各自的 `cmd/` | — |

## 快速开始

一个最小的插件：一份清单 `xihan.manifest.json`、一个操作，交给 `plugin.Serve`。

```go
package main

import (
	"context"
	_ "embed"
	"os"

	"github.com/xihanfun/framework-sdk-go/invocation"
	"github.com/xihanfun/framework-sdk-go/operation"
	"github.com/xihanfun/framework-sdk-go/plugin"
	"github.com/xihanfun/framework-sdk-go/primitives"
)

//go:embed xihan.manifest.json
var manifest []byte

type CreateRequest struct{ Text string `json:"text"` }
type CreateResponse struct{ Text, Tenant string }

func main() {
	registry := operation.NewRegistry()
	_ = operation.Add(registry, operation.Contract{ID: "echoes.create", Stability: primitives.Experimental, Expose: []string{operation.ExposeHTTP}},
		operation.HandlerFunc[CreateRequest, CreateResponse](func(ctx *invocation.Context, request CreateRequest) primitives.Result[CreateResponse] {
			return primitives.Ok(CreateResponse{Text: request.Text, Tenant: ctx.Tenant()})
		}), nil, "")

	if err := plugin.Serve(context.Background(), plugin.Options{Manifest: manifest, Operations: registry}); err != nil {
		os.Exit(1)
	}
}
```

清单、订阅、回调、日志与退出的写法见[写一个插件](#写一个插件)。

### 在 Go 应用里用原语

```go
observe := pipeline.Func("Observe", func(ctx *invocation.Context, next pipeline.Step) primitives.Result[primitives.Unit] {
	return next(ctx)
})
builder := pipeline.NewBuilder().UseAt(observe, primitives.Observe, pipeline.End, pipeline.AppOwner)
invoker, _ := operation.NewInvoker(registry, builder)

ctx := invocation.Background(invocation.WithTimeout(2*time.Second), invocation.WithBaggage(map[string]string{primitives.BaggageTenant: "t1"}))
response := invoker.Invoke(ctx, operation.Request{ID: "orders.create", Payload: []byte(`{"quantity":2}`)})
```

1. 可恢复失败返回 `primitives.Fail`，不 panic；错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键 + 参数（`primitives.Validation(code, key, details)` 等工厂）。`Error.Message` 先经 `primitives.SetDefaultMessages` 设置的解析器，再找 SDK 自带的内核错误英文模板，都解析不了时是 `Describe` 的「键 (参数)」。
2. 编程错误（不合文法的错误码、消息键或 Baggage 键，管道的重复登记、未知阶段、约束主体未登记）当场 panic `*primitives.FrameworkError`；约束目标未登记、跨阶段与成环在排序时由 `Describe` / `Dump` / `Build` 返回。自己的配置错误用 `primitives.NewFrameworkError(code, format, args...)` 创建。
3. 处理器与中间件在调用方的 goroutine 里同步执行。取消与截止时间只经 `ctx.Done()` 与 `ctx.Err()` 通知，调用器不中止处理器，处理器自己检查后尽早返回；`ctx.Cancellation()` 按截止令牌给出 `KERNEL_TIMEOUT_001` 或 `KERNEL_CANCELLED_001`。
4. 上下文用 `invocation.New(parent, opts...)` 或 `Background(opts...)` 创建；带截止时间的上下文占着一个定时器，调用一结束就该归还的用 `NewWithCancel`（另返回释放函数，`defer` 它）。`ctx.WithContext(c)` 换底层的 `context.Context`（用法同 `http.Request.WithContext`），追踪、Baggage 与对象袋照旧共享，截止时间取较早的。
5. 调用器把 panic 翻译成 `KERNEL_UNEXPECTED_001`，值与堆栈只写进日志：缺省是 `slog.Default()`，用 `operation.NewInvoker(registry, builder, operation.WithLogger(logger))` 换成自己的。

## 写一个插件

这一节是插件一侧用 Go 的写法；宿主怎样发现、启动与降级插件，见[进程外插件](/plugins/out-of-process)。[快速开始](#快速开始)的插件配这样一份清单，构建产物目录里放同一份文件（宿主读它，握手时运行时原样发送）：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.echo-go",
  "version": "0.1.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "contributes": { "operations": [ "echoes.create" ] },
  "activation": [ "onStartup" ],
  "entry": "echo-go"
}
```

1. 清单与启动前的核对：`kind` 为 `plugin`、`isolation` 为 `out-of-process`，`entry` 是可执行文件名，`primitives` 等于 `primitives.SpecVersion`，不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；登记的每个操作都要列在 `contributes.operations`。`Serve` 在监听之前核对这些，以及订阅与消息目录，不合即不监听、不写握手行，在 stderr 记一条 `error` 并返回错误。
2. 操作用 `operation.Add` 登记，请求与响应是普通的结构体（成员怎样对应 JSON 见[语言映射](#语言映射)）。文案用消息键，目录用 `plugin.LoadCatalog(name, fsys, "Localization")` 从 `Localization/<区域>.json` 加载后放进 `Options.Catalogs`，每份都要有 `en-US`。
3. 订阅经 `Options.Subscriptions` 接入，至少一次投递，处理器必须幂等。`Subscription.Mode` 选订阅方式（RFC-0009）：空或 `plugin.SubscriptionModeCompeting` 是竞争消费，`plugin.SubscriptionModeBroadcast` 是广播（每个在线的宿主实例都交给自己的插件实例，尽力而为、不进死信）。同一（主题，类型）只能声明一次；处理器用 `plugin.EventDeliveryOf(ctx)` 取这次投递的方式与次数。
4. 生命周期回调与健康项经 `Options.Hooks`（`Configure`、`Start`、`Stop`、`Health`）接入，健康项的名字要列在 `contributes.health`。回调返回的错误是（或用 `%w` 包着）`*primitives.Error` 时原样交给宿主；别的 `error` 与 panic 报成 `plugins.process.failed`，`reason` 只是错误的类型名（`*fs.PathError` 写 `PathError`，panic 写 `panic`），文本只进 stderr。
5. 配置：清单写 `contributes.config.section`，`Configure` 回调里用 `configuration.Decode(&settings)` 读。宿主把叶子都写成字符串，`Decode` 按字段类型转换：成员名按 ASCII 不区分大小写，数字按 JSON 数字的文法，布尔只认 `true` / `false`。绑定：`Configure` 回调的 `configuration.Bindings` 是清单 `bindings` 声明并获授予的资源绑定，按宿主下发的先后。每个 `plugin.Binding` 带 `Name`、`Type`、主值 `Value` 与属性 `Properties`（`map[string]string`，属性名区分大小写、原样；没有属性时是空表；只挂属性、没有主值的绑定 `Value` 为空）。主值与属性值可能是凭据，不要写进日志：`Binding` 经 `fmt` 与 `slog` 输出时只带绑定名、类型与属性名；经 `encoding/json` 写出时只有 `{"name":…,"type":…,"propertyNames":[…]}`（属性名按升序，`Binding.MarshalJSON`），`[]Binding`、`Configuration` 与 `NewLogHandler` 的 JSON 日志同样不带主值与属性值。遮过的 JSON 读不回原值，要持久化主值或属性的逐字段自己取。清单里的绑定缺省必需（`required` 缺省 `true`），部署里没有它时宿主不配置插件；可以没有的写 `"required": false`，这时 `Bindings` 里可能没有它。
6. 回调宿主：操作里用 `plugin.HostOf(ctx)` 取 `Host`，`Invoke` 经宿主的完整管道，`Publish` 经宿主的消息代理（批量，逐项结果）。信封的 `Time` 是 `*time.Time`，nil 是没有（可以写 `Time: new(time.Now())`）：发布时换算成 UTC 写成 RFC 3339，以 `Z` 结尾，秒的小数去掉末尾的 0、整秒不写小数点，最多九位；SDK 不在本地核对，年份越界的时刻照写，由宿主以 `messaging.envelope.invalid_time` 拒绝那一项。投递时宿主写来的文本读成 UTC 的 `time.Time`（纳秒，不截断），读不出的投递以 gRPC `InvalidArgument` 拒绝。`DataSchema` 是 CloudEvents 的 `dataschema`，空串是没有，投递与发布都原样传递，SDK 不裁剪、不核对。清单 `permissions` 要声明 `capability:kernel.pipeline`（`Invoke`）或 `capability:messaging.queue`（`Publish`）并由宿主授予；权限、租户与截止时间在发出之前先在本地核对，不合不发请求。回调端点取 `XIHAN_PLUGIN_HOST_ADDRESS` 里第一个能连的项（Windows 上 `npipe`、`unix`、`tcp`，其他系统 `unix`、`tcp`），一个都没有时插件照常服务，回调以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable` 失败。
7. 监听端点：缺省是 Unix 域套接字（Windows 也是），放在新建的 `0700` 私有目录里，临时目录的路径含空白时改用回环 TCP。`Options.Network` 可以指定：`plugin.NetworkTCP` 只绑定 `127.0.0.1` 的随机端口，隔离只靠会话令牌；`plugin.NetworkPipe` 是只允许当前用户、拒绝远程客户端的命名管道。
8. 日志：stdout 只有握手行，日志写 stderr 的 JSON 行。运行时自己建一个日志器；插件的日志想进同一处，就建 `slog.New(plugin.NewLogHandler(os.Stderr, slog.LevelInfo))` 并交给 `Options.Logger`，操作 panic 的值与堆栈也写进它。不要用 `slog.Default()`，它的文本行宿主读不出级别与字段。
9. 退出：宿主调用 `Stop`、关闭 stdin、发 SIGTERM，或 `Serve` 的上下文结束后，`Serve` 按规范的时限收尾并返回 nil，进程以退出码 0 退出。

验证：`xihan-conformance plugin --dir <产物目录>`。运行器自带一份协议生成代码，与本 SDK 的 `plugin/v1` 不能链接进同一个二进制，测试里以独立进程运行它。

更完整的写法见 SDK 的 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-go/blob/main/examples/echo)（操作、订阅、健康、配置与宿主回调）与 [`examples/cache`](https://github.com/XiHanFun/framework-sdk-go/blob/main/examples/cache)（远程缓存 Provider）。XiHan.Framework 的 `framework/samples/EchoGoPlugin` 与 `CacheGoPlugin` 是这两个示例在 .NET 宿主里的端到端运行。

## 与宿主共用的行为

线上编码、文法、次序与时限的唯一出处是规范，各语言与 .NET 宿主相同，这一页不复述。规范怎样组织见[规范与多语言](/appendix/spec)，插件一侧规则的摘要见[进程外插件](/plugins/out-of-process#插件一侧的规则)。

| 行为 | 规范 |
| --- | --- |
| `Result`、`Error`、`Maybe` 的线上编码，JSON 文本的共同读取检查，空白字符 | [RFC-0001](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0001-primitives.md) |
| 清单的读取顺序、错误与写出的排版 | [RFC-0002](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0002-manifest.md) |
| 追踪、Baggage、截止时间与取消的归因 | [RFC-0003](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0003-invocation-context.md) |
| 管道的阶段、排序规则与配置错误 | [RFC-0004](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0004-pipeline-stages.md) |
| 消息键、模板、格式说明符与区域 | [RFC-0010](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0010-localization.md) |
| 载荷解码、调用器的内置步骤、内核错误 | [RFC-0011](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0011-operations.md) |
| 握手行、环境变量、回调端点列表 | [`plugin/handshake.md`](https://github.com/XiHanFun/framework-spec/blob/main/plugin/handshake.md) |
| 调用次序、配置、就绪、健康、停止与收尾 | [`plugin/lifecycle.md`](https://github.com/XiHanFun/framework-spec/blob/main/plugin/lifecycle.md) |
| 调用、事件投递、错误的写出与读入、回调与映射表、日志、运行时的消息键 | [`plugin/invocation.md`](https://github.com/XiHanFun/framework-spec/blob/main/plugin/invocation.md) |
| 远程缓存 Provider 的操作集 | [`provider/caching-distributed.md`](https://github.com/XiHanFun/framework-spec/blob/main/provider/caching-distributed.md) |

Go 的写法与差异：

- **JSON 读取**：线上编码、载荷与清单都不交给 `encoding/json` 读（它不查重复成员、成员名不区分大小写、把孤立的代理项换成 U+FFFD），SDK 先做规范的共同检查，再按自己的规则解码。写出时字符串只转义必须转义的字符，`<`、`>`、`&` 与非 ASCII 原样。
- **`Result`**：`Success()` / `Result[Unit]` 写 `{"ok":true}`。可能没有的值用 `Result[Maybe[T]]`；值编码成 `null`（`nil` 指针、`nil` 切片）而类型不是 `Maybe` 时写出报错，空集合用非 `nil` 的空切片或空映射。
- **数字**：`Error.Details` 按值模型读成 `int64` / `float64`；`Result[any]` 与值里的 `any` 保留原文的 `json.Number`，它不是合法的 JSON 数字时写出报错。
- **`Error`**：`Literal` 与 `WithText` 的文案为空或只有空白时 panic。`(*Error).Unwrap()` 返回 `Inner`，`errors.Is` / `errors.As` 沿错误链查找。
- **清单**：`manifest.Read` / `ReadFile` 失败是 `*primitives.Error`（`KERNEL_CAPABILITY_006`），`Read` 不跳过 BOM、`ReadFile` 跳过；`Manifest.Validate` 的码是 `PRIMITIVES_VALIDATION_001`；`manifest.Write` 不做校验。可选的字符串 `Package`、`Entry` 与 `ConfigContribution.Schema` 是 `*string`（`nil` 是没写，指向空串是写了空串），用 `manifest.Text("…")` 赋值。`contributes.pipeline` 的规则由 `plugin.Serve` 核对，`manifest.Read` 不查。
- **模板**：带区域的入口是 `FormatMessageIn`、`TryFormatMessageIn`、`FormatArgumentIn`，不带区域的 `FormatMessage` 等按不变文化。`Catalog.Resolve` 是严格解析（缺键或缺值即返回假），`FormatMessage` 是宽松的（缺值的占位符原样保留）。
- **调用器**：`operation.NewInvoker` 在管道的副本里加上三个内置步骤 `ObserveMiddleware`、`DeadlineMiddleware`、`ValidationMiddleware`，管道里不能再登记同名的中间件，`Invoker.Pipeline()` 返回实际执行的管道。响应在管道结束之后才用 `encoding/json` 编码，中间件用 `operation.ResponseOf(ctx)` 看编码之前的对象；`operation.Current`、`RequestOf` 只在调用期间的上下文上可见。
- **应答与发布的数据**：回调载荷与 `Envelope.Data` 是 `[]byte`，原样发出，由宿主核对（`plugin/invocation.md` §5，ADR-0312、ADR-0315）。操作的响应由 `encoding/json` 写出，写不成合法的 JSON 文本即写不出（RFC-0011 规则 10 的「编码」，ADR-0318）：要写出的字符串（成员名与值，含 map 的键、`MarshalText` 的结果与 `primitives.ID` / `Maybe` 里的值）不是合法的 UTF-8（含编成 UTF-8 的孤立代理项），`json.Marshaler` 交出的原文有非法字节、孤立代理项的转义或重复成员，或嵌套超过 64 层，都以 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception` 结束，没有参数、不带 `inner`、没有载荷，原因与出错的路径写进调用器的日志。`encoding/json` 把非法的 UTF-8 换成 U+FFFD 照常写出，SDK 在它写成功之后照它的规则（导出字段、`json:"-"`、嵌入、`omitzero`、指针与值接收者的 `MarshalJSON` / `MarshalText`）把要写出的字符串再查一遍；`MarshalJSON` 自己换成 U+FFFD 的看不出来。ADR-0319 把写出之后过 JSON 文本的共同检查（64 层、重复成员）写进了规范，Go 写完之后本来就对整段文本做这道检查，行为不变。
- **远程缓存 Provider**：`caching.RegisterOperations` 的九个操作都先在适配层校验键、选项与 base64，再调 `Store`；`Store` 返回的错误是（或用 `%w` 包着）`*primitives.Error` 时原样交给宿主，别的按后端不可用。

## 语言映射

规范把一部分写法交给各语言自己声明，Go 的写法如下。

### 请求类型的成员

RFC-0011 规则 2。字段名、嵌入结构体的提升与 `json` 标签照 `encoding/json`，成员名区分大小写，未知成员忽略。

| 规范 | Go |
| --- | --- |
| 必填 | 字段带 `xihan:"required"` 标签；只要求成员出现，值为 `null` 也算出现 |
| 可选 | 没有这个标签，缺少时取零值 |
| 接受 `null` | 字符串、切片、数组、映射、结构体、指针与 `any`，`null` 等于没有值（字段保持零值） |
| 不接受 `null` | 整数、浮点与布尔（含它们的切片项与映射值）；要接受时写成指针（`*int64`、`*bool`） |
| 整数与浮点 | 整数按字段类型的范围核对（`int32` 收到 `2147483648` 即不符），不接受 `1.0` 与 `1e2`；浮点接受整数写法，`float32` 按 32 位的范围读 |
| 任意 JSON | `any`：数字读成原文的 `json.Number`，原样返回时响应的 JSON 值与原文相等 |
| 自己解码的类型 | 实现 `json.Unmarshaler` 的类型（`time.Time`、`json.RawMessage`）拿到原文自己解码；`encoding.TextUnmarshaler` 只接字符串；`[]byte` 读 base64 字符串 |

字段是非空接口（如 `io.Reader`）时解码报该处的路径，SDK 不猜具体类型，多态的请求由类型自己实现 `UnmarshalJSON`。

```go
type CreateOrder struct {
	Customer string      `json:"customer" xihan:"required"` // 必填，null 也算出现
	Note     string      `json:"note"`                       // 可选，null 等于没有
	Quantity int32       `json:"quantity"`                   // 可选，不接受 null 与 1.0
	Discount *float64    `json:"discount"`                   // 可选，接受 null
	Lines    []OrderLine `json:"lines"`
	Extra    any         `json:"extra"`                      // 任意 JSON，数字保留原文
}
```

### 值模型

模板参数与错误细节归到 RFC-0010「模板」的值模型时，Go 的类型这样归：

| Go 的值 | 值模型 | 说明 |
| --- | --- | --- |
| `nil`、`nil` 指针、`nil` 切片与映射 | 空 | 顶层写空串，嵌套写 `null` |
| `bool` 与底层是 `bool` 的类型 | 布尔 | `true` / `false` |
| `string` 与底层是 `string` 的类型 | 字符串 | 原样 |
| `int`、`int8`…`int64`，`uint`…`uint64` 在 int64 范围内 | 整数 | `rune` 是 `int32`，按整数写；要写一个字符用 `string(r)` |
| 超出 int64 的 `uint64`、`*big.Int` | 浮点 | `D` 与 `X` 不再适用 |
| `float64`、`float32`、`*big.Float`、`*big.Rat` | 浮点 | `float32` 换成最接近的 `float64`：`float32(0.1)` 写 `0.10000000149011612` |
| `json.Number`、`json.RawMessage` | 按 JSON 读 | 整数写法且在 64 位范围内是整数，其余数字是浮点 |
| 切片、数组（含 `[]byte`） | 数组 | |
| 映射 | 对象 | 键写成它的文本 |
| 实现 `error` / `fmt.Stringer` 的值 | 字符串 | 取 `Error()` / `String()`：`time.Time`、`time.Duration`、带 `String` 的枚举（`primitives.Stability`）、`primitives.Maybe`（`Some(值)` / `None`） |
| 其余（结构体、复数等） | 字符串 | `fmt.Sprint` 的文本 |

Go 自己的文本（日期、`Duration`、结构体）不跨语言：插件的目录要在宿主上格式化时，先把这些值转成字符串再放进参数。

### 文法与空白

操作 ID、主题、清单的激活事件、缓存键、协议版本协商、端点与回调失败的映射直接调规范一致性模块的 `rules`，SDK 里不另存一份。操作 ID 不超过 128 个字符（`operation.MaxIDLength`），缓存键按 UTF-8 编码后不超过 512 字节（`caching.MaxKeyLength`）。空白指 RFC-0001「空白字符」写死的 25 个码位（`rules.IsWhitespace`），握手行、`Error` 的 `message` 与自由文本共用。缓存键还要是合法的 UTF-8：孤立的代理项在 Go 里只能是非法的字节，`caching.IsKey` / `ValidateKey` 拒收（ADR-0316）。

## 设计取舍

- 依赖：`google.golang.org/grpc` 与 `google.golang.org/protobuf`（插件协议），`github.com/Microsoft/go-winio`（Windows 命名管道），规范的一致性模块 `github.com/xihanfun/framework-spec/conformance`（文法与映射表）。协议代码 `plugin/v1` 生成后提交，使用者不需要 buf。
- 同步的处理器：处理器、中间件与生命周期回调都是普通函数，在调用方的 goroutine 里执行，SDK 不另起 goroutine 放弃它们；取消只是通知。
- 编程错误 panic、配置与运行期的失败返回值：登记冲突这类错误与 `http.ServeMux` 一样当场 panic，可恢复失败走 `Result`，读清单、建调用器这类可能失败的步骤返回 `error`。
- 不用 `encoding/json` 读线上文本：它的宽松读法与规范不符（见[与宿主共用的行为](#与宿主共用的行为)）。

## 一致性

- 现状：原语、清单、调用上下文、管道、操作、进程外插件运行时与远程缓存 Provider 的插件侧可用，示例 `examples/echo` 与 `examples/cache` 通过规范黑盒运行器的用例（`examples/cache` 没有订阅，`event` 用例按规则跳过）。
- 认领的档（RFC-0024）：`core`、`plugin` 与 `provider:caching.distributed@1`；档的定义见规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。
- 缺口：无。
- 钉的规范版本：SDK 仓库的 `spec/` 子模块与 `go.mod` 里的一致性模块，两者是同一个规范提交。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 范围外：流式操作（操作契约还没有流式形态，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束）与 Wasm 载体。
- 怎样验证：各包的测试经一致性模块读取内嵌的向量并回放，仓库根的 [`vectors_test.go`](https://github.com/XiHanFun/framework-sdk-go/blob/main/vectors_test.go) 按档表核对认领的档要求的每个向量要么有测试回放、要么列为缺口；两个示例的测试构建插件，用 `go.mod` 里同一版本的 `xihan-conformance` 跑全部用例。CI 在 Linux 与 Windows 上都跑。用 Go 实现的分布式缓存另经远程 Provider 适配器通过 .NET 的缓存一致性套件（`framework/samples/CacheGoPlugin`）。
- 信封的 `time`：回放 `plugin/sdk-envelope-times.json` 的纳秒一列（ADR-0310）。作者给的时刻经 `ToProtoEnvelope` 写出的文本与 `text.nanoseconds` 逐字相同，宿主写来的文本经 `FromProtoEnvelope` 读出的时刻与 `utc.nanoseconds` 相等，`unreadable` 的每一条投递都以 `InvalidArgument` 拒绝。`time.Time` 是纳秒精度，不截断，没有跳过的用例。`plugin/envelope-times.json` 现在同在 `plugin` 档。
- 缓存键：`caching/cache-keys.json` 里以 `utf16` 码元给出的键（孤立的代理项，ADR-0316）按 WTF-8 组成字节（一致性模块的 `Input()`），`IsKey` 与 `ValidateKey` 都拒收；它们写不进宿主的 JSON，按码元写成转义的请求在解码时以 `KERNEL_VALIDATION_001`（`reason` 为 `$`）被拒。

## 破坏性改动与迁移

首个对外版本是 `v0.2.0-rc.1`。之后的变化：

- 线上编码遇到不是合法 JSON 数字的 `json.Number`（含带空白的 `" 1"`）报错，不再原样写出或写成 `0` → 放进 `Result` 的值与错误细节之前保证它是数字文本。
- `errors.Is` / `errors.As` 沿 `Error.Inner` 往里匹配 → 只想比较最外层错误码的地方改为直接比较 `Code`。
- `Maybe` 作模板参数或进日志时的文本由 `{值 true}` 变为 `Some(值)` / `None` → 匹配旧文本的地方改掉。
- 插件进程里操作 panic 与响应写不出的日志从 `slog.Default()` 改写进运行时的日志器 → 要收这些日志的给 `Options.Logger`，不再靠替换 `slog.Default()`。
- 生命周期回调与 `caching.Store` 返回的、用 `%w` 包着的 `*primitives.Error` 原样交给宿主，不再报成 `plugins.process.failed` 或后端不可用 → 不想把内部错误带给宿主的不要用 `%w` 包它。
- `caching` 的九个操作都在适配层先校验键，`try-remove` 在键与 `expected` 都不合法时改报键（`CACHING_VALIDATION_001`）→ 自己实现的 `Store` 不必再校验键；按错误内容断言的测试更新期望。
- 超过 128 个字符的操作 ID 不再合法：`operation.Add` 返回 `PRIMITIVES_VALIDATION_001`，读清单为 `kernel.manifest.invalid`，原先不设上限；新增常量 `operation.MaxIDLength` → 把 ID 缩到 128 个字符以内。
- 清单 `activation` 里 `onEvent:` 的模式可以带下划线（如 `onEvent:order_events.*`），原先读不出 → 不用改。
- 示例 `examples/cache` 的 `caching.remote.key_invalid` 文案由「不超过 512 个字符」改为「按 UTF-8 编码后不超过 512 字节」（`en-US` 是 `at most 512 bytes long in UTF-8`）→ 照抄这份消息目录的插件，目录要与宿主 `Caching.Remote` 的同名目录逐字一致，两边同时升级。
- `plugin.Binding` 新增 `Properties map[string]string`（绑定的属性，ADR-0285），类型因此不再可比较 → `==` 与拿它作 map 键的地方改为逐字段比较，不带字段名的复合字面量补上字段名；`reflect.DeepEqual` 区分 nil 与空表，运行时交出的没有属性的绑定是空表。
- `plugin.Binding` 经 `fmt` 与 `slog` 输出时不再带 `Value`，只带绑定名、类型与属性名 → 确实要看主值的地方直接读 `binding.Value`。
- `json.Marshal` 绑定（含 `[]Binding`、`Configuration` 与 `NewLogHandler` 的 JSON 日志）只写 `name`、`type` 与按升序排列的 `propertyNames`，不再写 `Name`、`Type`、`Value` 字段；slog 里的绑定组同样是这三个成员 → 要持久化主值或属性的逐字段自己取；按旧字段名解析 JSON 或日志的改用新成员名。
- `plugin.Envelope.Time` 由 `string` 改为 `*time.Time`（nil 是没有，ADR-0306），SDK 写出以 `Z` 结尾、去掉小数末尾 0 的 RFC 3339，投递时把宿主的文本读成 UTC 时刻；宿主写来的 `time` 读不出的投递以 gRPC `InvalidArgument` 拒绝；`plugin.FromProtoEnvelope` 改为返回 `(Envelope, bool)` → 发布处把 `Time: t.UTC().Format(time.RFC3339Nano)` 改成 `Time: new(t)`（或 `&t`）；读 `envelope.Time` 的先判 nil；调用 `FromProtoEnvelope` 的接住第二个返回值。
- `plugin.Envelope` 与 `plugin/v1.Envelope` 新增 `DataSchema`（CloudEvents `dataschema`，ADR-0305）→ 不用改；不带字段名的 `Envelope` 复合字面量补上字段名。
- 操作的响应里有不是合法 UTF-8 的字符串（含 map 的键、`MarshalText` 的结果、`primitives.ID` / `Maybe` 里的值），`json.Marshaler` 的原文有非法字节、孤立代理项的转义或重复成员，或嵌套超过 64 层时，调用以 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception` 失败、没有载荷，原先写成 U+FFFD 照常成功（ADR-0318）→ 外部来的字节放进响应之前先用 `utf8.ValidString` 核对或用 `strings.ToValidUTF8` 换掉，`json.RawMessage` 只放合法的 JSON 文本。ADR-0319 把其中嵌套与重复成员的核对写进了规范，Go 的行为不变。
- `caching.IsKey` / `ValidateKey` 拒收不是合法 UTF-8 的键（`CACHING_VALIDATION_001`），原先按 U+FFFD 读、照常接受（ADR-0316）→ 自己拼键的地方保证键是合法的 UTF-8。
