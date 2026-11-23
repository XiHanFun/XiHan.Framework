# Rust SDK

[framework-sdk-rust](https://github.com/XiHanFun/framework-sdk-rust) 是 [XiHan Spec](/appendix/spec) 的 Rust 实现：原语层、清单、调用上下文、管道与操作契约，以及进程外插件运行时（cargo feature `plugin`）。规范以 submodule 放在 SDK 仓库的 `spec/`，一致性由规范仓库的向量与黑盒运行器验证；参考实现是 .NET（[XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)），规范写到的行为与 .NET 宿主一致。宿主一侧怎样加载插件见[进程外插件](/plugins/out-of-process)。

## 安装

```bash
cargo add xihan-framework@<版本>
```

写插件时打开 feature `plugin`：

```bash
cargo add xihan-framework@<版本> --features plugin
```

`plugin` 模块是可选的：它带来 tonic、prost 与 tokio，不打开这个 feature 时这些依赖都不进来，其余模块也不依赖任何异步运行时。最低 Rust 版本见仓库 `Cargo.toml` 的 `rust-version`。

目前只有预发布版，要写明版本号：`<版本>` 换成 [Releases](https://github.com/XiHanFun/framework-sdk-rust/releases) 里的版本（不带 `v`）；不写版本会装到 crates.io 上占名用的旧版本。

## 模块

整个 SDK 是一个 crate `xihan-framework`；领域词与规范术语表一致，只换本语言的大小写风格。

| 模块 | 内容 | 规范 |
| --- | --- | --- |
| `primitives` | `Error` / 错误码、`Result<T>`（标准库 `Result<T, Error>`）与线上编码 `WireResult`、`Maybe<T>`（`Option<T>`）、`TypedId<Tag, V>`、`LifecycleState`、`PipelineStage`、`Stability`、Baggage 键与值约束、消息键与模板、`Catalog`、`FrameworkError`；`primitives::json` 是 JSON 文本的共同读取检查与按值模型的读写 | RFC-0001、RFC-0010、`conventions/` |
| `manifest` | `xihan.manifest.json` 的模型、读取、按规范排版的写出与校验，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `invocation` | `Context`：可等待、父子传播的 `CancellationToken`，后台定时器触发的截止时间，W3C 追踪、已登记的 Baggage、按类型存取的对象袋 | RFC-0003 |
| `pipeline` | 十一阶段的 `Builder`：`use_middleware` / `use_at` / `use_named_instance`、`before` / `after`、`describe` / `dump` / `build`；中间件与终点都是异步的 | RFC-0004 |
| `operation` | `Contract`、`Schema`、异步 `Handler<Req, Res>`、容量 10000 的 `Registry`、经完整管道执行的 `Invoker`（XGI：JSON 请求与响应；另有不经 JSON 的类型化入口） | RFC-0011 |
| `plugin`（feature `plugin`） | 进程外插件运行时 `Plugin`（协议 `xihan.plugin.v1`）、宿主回调客户端 `Host`、往 stderr 写 JSON 行的 `Logger`、消息目录 `MessageCatalog`；`plugin::v1` 是 `plugin.proto` 的生成代码 | RFC-0017、`plugin/` |

## 快速开始

```rust
use std::sync::Arc;
use std::time::Duration;

use serde::{Deserialize, Serialize};
use xihan_framework::invocation::Context;
use xihan_framework::operation::{self, Contract, EXPOSE_HTTP, Invoker, Registry, Request};
use xihan_framework::pipeline::{self, APP_OWNER, Builder, Placement};
use xihan_framework::primitives::{BAGGAGE_TENANT, Error, PipelineStage, Stability};

#[derive(Clone, Deserialize)]
struct CreateOrder {
    quantity: u32,
}

#[derive(Serialize)]
struct OrderCreated {
    quantity: u32,
    tenant: Option<String>,
}

async fn run() -> Result<(), Box<dyn std::error::Error>> {
    let registry = Arc::new(Registry::new());
    registry.add(
        Contract::new("orders.create").with_stability(Stability::Experimental).with_expose([EXPOSE_HTTP]),
        operation::handler_fn(|ctx: &Context, request: CreateOrder| {
            Box::pin(async move {
                if request.quantity == 0 {
                    return Err(Error::validation("ORDERS_VALIDATION_001", "orders.quantity.invalid").with_detail("quantity", 0));
                }
                Ok(OrderCreated { quantity: request.quantity, tenant: ctx.tenant().map(str::to_owned) })
            })
        }),
    )?;

    let observe = pipeline::middleware_fn("Observe", |ctx, next| {
        Box::pin(async move {
            let outcome = next.run(ctx).await;
            if let Err(error) = &outcome {
                eprintln!("{} failed: {error}", ctx.trace_id());
            }
            outcome
        })
    });
    let mut builder = Builder::new();
    builder.use_at(observe, PipelineStage::Observe, Placement::End, APP_OWNER)?;
    print!("{}", builder.dump()?); // observe      Observe

    let invoker = Invoker::new(registry, &builder)?;
    let ctx = Context::builder().timeout(Duration::from_secs(2)).baggage(BAGGAGE_TENANT, "t1").build();
    let payload = invoker.invoke(&ctx, Request::new("orders.create", r#"{"quantity":2}"#)).await?;
    assert_eq!(payload, br#"{"quantity":2,"tenant":"t1"}"#);
    Ok(())
}
```

1. 中间件、处理器与管道终点返回 `BoxFuture`（`Pin<Box<dyn Future + Send>>`），写 `Box::pin(async move { … })`；本 crate 不依赖任何异步运行时，future 交给 tokio、`futures::executor::block_on` 或任何执行器。`Invoker::invoke` 与 `invoke_typed` 返回 `Send` 的 future，`invoke_owned` 返回 `'static` 的 future，可以直接 `tokio::spawn`。`pipeline::middleware_fn`、`step_fn` 与 `operation::handler_fn` 把闭包包成中间件、管道步骤与处理器（`pipeline::from_fn` 是 `middleware_fn` 早先的名字，照常可用）。
2. 可恢复失败返回 `Err(Error)`，错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键 + 参数（`Error::validation(code, key).with_detail(…)`）。`Error::message` 先按 `primitives::set_default_messages` 设置的解析器解析，再按 SDK 自带的 en-US 模板（`primitives::builtin_catalog()`），都没有时是「键 (参数)」。`with_*` 消耗原值、返回改过的值，都标了 `#[must_use]`。
3. 契约的 `expose` 为空表示不对外发布：不调 `with_expose` 的操作在任何协议上都不发布，全部协议写 `with_expose(operation::EXPOSE_ALL)`；五个协议名之外的值与写了两次的名字在登记时被拒绝。
4. 编程错误（不合文法的错误码、消息键或 Baggage 键）panic，各函数的文档写明 panic 条件；panic 的内容是 `FrameworkError` 本身（`std::panic::panic_any`），接住后用 `downcast_ref::<FrameworkError>()` 取出。来自不可信输入的值先用 `is_error_code`、`is_baggage_key_well_formed` 判断，或用 `Error::try_new`。清单校验、管道排序、操作登记等配置期失败返回 `FrameworkError`，它可以用 `?` 转成 `Error`。
5. 取消与截止时间只经令牌通知：`Context::done()` 在调用方取消或到达截止时间时完成，`Context::cancellation_error()` 给出 `KERNEL_TIMEOUT_001` 或 `KERNEL_CANCELLED_001`；调用器不中止处理器、不丢弃它的 future，处理器把取消写成结果（`Err(ctx.cancellation_error()…)`）时才按令牌归因。截止时间由进程里一个后台线程（`xihan-deadline-timer`）按时触发。`with_cancellation`、`with_deadline`、`with_timeout` 派生还跟随另一个令牌、或截止时间更紧的上下文，追踪、Baggage 与对象袋共享。
6. 调用器把排在 `ObserveMiddleware` 之后的中间件与处理器的 panic 翻译成 `KERNEL_UNEXPECTED_001`（要 `panic = "unwind"`），排在它之前的 panic 原样交给调用方。中间件经 `operation::with_response` 看编码之前的响应；响应写不出（浮点是 NaN 或无穷、序列化不支持的值）时调用以未处理失败结束，不抛给调用方。

## 写一个插件

这一节是插件一侧用 Rust 的写法；宿主怎样发现、启动与降级插件，见[进程外插件](/plugins/out-of-process)。

```bash
cargo add xihan-framework@<版本> --features plugin
cargo add tokio --features macros,rt
cargo add serde --features derive
```

```rust
use std::sync::Arc;

use serde::{Deserialize, Serialize};
use xihan_framework::operation::{self, Contract, Registry};
use xihan_framework::plugin::{MANIFEST_FILE_NAME, Plugin};

#[derive(Deserialize)]
struct Ping {
    text: String,
}

#[derive(Serialize)]
struct Pong {
    text: String,
}

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let registry = Arc::new(Registry::new());
    registry.add(
        Contract::new("pings.create"),
        operation::handler_fn(|_ctx, ping: Ping| Box::pin(async move { Ok(Pong { text: ping.text }) })),
    )?;
    // 握手交给宿主的清单必须与插件目录里的文件一致：工作目录就是插件目录，直接读它
    Plugin::from_manifest_file(MANIFEST_FILE_NAME)?.with_operations(registry).serve().await?;
    Ok(())
}
```

插件目录里放可执行文件与清单 `xihan.manifest.json`：

```json
{
  "id": "xihan.samples.ping",
  "version": "1.0.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "contributes": { "operations": [ "pings.create" ] },
  "entry": "ping"
}
```

1. 清单与启动前的核对：`kind` 为 `plugin`、`isolation` 为 `out-of-process`，`entry` 是相对插件目录的可执行文件（Windows 上没有扩展名时宿主补 `.exe`），`primitives` 等于本 SDK 实现的原语层大版本 `1`，不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；登记的每个操作都要在 `contributes.operations` 里，健康项在 `contributes.health` 里。`serve` 在监听与写握手行之前核对这些，以及订阅与消息目录（要有 `en-US`），不合即在 stderr 记一条 `error`、不监听，返回 `FrameworkError`。
2. 订阅用 `with_subscription(Subscription::new(主题), event_handler_fn(…))` 登记，至少一次投递，处理器必须幂等；`with_mode` 选订阅方式，同一（主题，类型）只能声明一次。信封的数据用 `Envelope::data_as::<T>()` 按 JSON 读、`with_json_data` 写。
3. 生命周期与配置：`with_hooks` 接入 `Hooks`（`configure`、`start`、`stop`、`health`）。`Configuration` 的 `section` 是配置节原文，`decode::<T>()` 是类型化读取：成员名按 ASCII 不区分大小写，宿主写成字符串的叶子可以读成数字（按 JSON 数字的文法）与布尔（`true` / `false`）。钩子返回的错误原样交给宿主；panic 报成 `plugins.process.failed`，`reason` 是 `panic`，内容只写进 stderr。
4. 回调宿主：处理器经 `plugin::host(ctx)`（调用之外经 `Configuration::host()`）取 `Host`。`invoke` 收发 JSON 字节，`invoke_json` 是类型化的入口，`publish` 是批量、逐项结果。清单 `permissions` 要声明 `capability:kernel.pipeline`（`Host.Invoke`）或 `capability:messaging.queue`（`Host.Publish`）并由宿主授予；权限、租户与截止时间在发出之前先在本地核对，不合不发请求。回调端点取 `XIHAN_PLUGIN_HOST_ADDRESS` 里第一个本平台能连的项（`Network::supported()`：Unix 上是 `unix` 与 `tcp`，Windows 上是 `npipe` 与 `tcp`），一个都没有时插件照常服务，回调以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable` 失败。
5. 监听端点：Unix 上是 Unix 域套接字，放在临时目录下新建的私有目录里（目录 `0700`、套接字 `0600`，临时目录的路径含空白或不是 UTF-8 时改用 `/tmp`），停止后连同目录删掉；Windows 上是命名管道 `\\.\pipe\xihan-plugin-<随机>`，拒绝远程客户端，用系统缺省的安全描述符（本 crate 禁用 `unsafe`，设不了只含当前用户的访问控制表），隔离与回环 TCP 一样靠会话令牌；`with_network(Network::Tcp)` 只绑定 `127.0.0.1`。
6. 日志与退出：stdout 只有握手行，日志用 `Logger` 写 stderr 的 JSON 行（`with_logger` 换成自己的）。`Stop`、stdin 关闭与 SIGTERM（Unix）按规范的时限收尾，`serve` 返回 `Ok`，进程以退出码 0 退出。测试里用 `Plugin::start(launch)` 取 `Running`，它的 `shutdown` 按同样的步骤收尾但不结束进程。
7. 消息目录：插件自己的目录用 `MessageCatalog`（`from_json` 从各区域的 JSON 文本建，至少要有 `en-US`），`with_catalog` 交给运行时随握手发送。运行时自己的消息键由宿主的目录翻译，SDK 自带同样的 10 种区域文案（`plugin::runtime_catalog()`）供本地渲染。

用规范仓库的黑盒运行器验证（需要 Go）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --exclude <会崩溃的操作 ID> --sample '<操作 ID>=<请求 JSON>'
```

完整的示例是仓库的 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-rust/blob/main/examples/echo/main.rs)（操作、事件订阅、健康项、配置与宿主回调）。

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

Rust 的写法与差异：

### 线上编码

```rust
use xihan_framework::primitives::{Error, WireResult};

let ok = serde_json::to_string(&WireResult(Ok::<_, Error>(7))).unwrap();
assert_eq!(ok, r#"{"ok":true,"value":7}"#);
let failed = serde_json::to_string(&WireResult(Err::<u32, _>(Error::literal("ORDERS_CONFLICT_001", "stale")))).unwrap();
assert_eq!(failed, r#"{"ok":false,"error":{"code":"ORDERS_CONFLICT_001","message":"stale"}}"#);
```

- `Result<()>` 是不带值的成功，只写 `{"ok":true}`；值类型是 `Maybe<T>`（`Option<T>`）时 `value` 可以是 `null`；`TypedId` 编码为底层值。
- 线上文本用 `primitives::json::from_slice` 读：它先过共同的 JSON 读取检查，数字按值模型。直接用 `serde_json::from_str` 读不到这些检查。写出用 `json::to_vec`（类型化，非有限的浮点写不出）或 `json::Model` / `json::to_value`（无类型，非有限的浮点写成字符串）。
- `Error::literal` 与 `Error::with_text` 的文案为空或只有空白时 panic，`try_literal` 与 `try_with_text` 返回 `PRIMITIVES_VALIDATION_001`。空白按 RFC-0001 写死的 25 个码位判断（`primitives::is_whitespace`、`is_blank`），不用 `char::is_whitespace`。

### 消息模板

`format_message` 等按不变文化；`format_message_in` / `try_format_message_in` / `format_value_in` 按区域取小数点与分组符，`Catalog` 按请求的区域格式化。内置的 10 种区域在 `primitives::LOCALES`，`Catalog::from_json` 从各区域的 JSON 文本建目录。

### 清单

清单用 `manifest::read` 读入、`manifest::write` 按规范的排版写出。读出时与 .NET 的类型化模型一样填上缺省值（`role` 为 `provider`、`activation` 为 `["onStartup"]`、绑定的 `required` 为 `true`，`$schema` 不保留），写出的清单再读回来与原来的相等。`Kind`、`Role`、`Isolation` 等按名字编码的枚举都有 `ALL`、`as_str()`、`parse()` 与 `FromStr`，serde 只按字符串读写。

## 语言映射

规范留给各语言写明的映射。

### 请求类型的成员

RFC-0011 规则 2。载荷按 serde 读入请求类型：成员名区分大小写，未知成员忽略（不要加 `deny_unknown_fields`），数字不从字符串读，整数超出类型的范围即不符，对象位置只收 JSON 对象。

| 规范 | Rust |
| --- | --- |
| 必填，不接受 `null` | 不带缺省值的非 `Option` 成员：`n: i64` |
| 必填，接受 `null` | `#[serde(deserialize_with = "operation::nullable")] name: Option<String>`：缺少即不符，`null` 为 `None` |
| 可选，不接受 `null`（整数、浮点、布尔） | `#[serde(default)] count: i64`：缺少取缺省值，`null` 不符 |
| 可选，接受 `null`（字符串、数组、对象） | `Option<T>`：缺少与 `null` 都是 `None` |
| 接受 `null` 的数组项 | `Vec<Option<String>>` |
| 任意 JSON | `primitives::json::Json`（照原文保留数字），接受 `null` 表示没有时 `Option<Json>`；`serde_json::Value` 会把 `1e400` 读成 `null`、把超出 64 位的整数读成近似值 |

缺少必填成员在所在对象结束时报该对象的路径，类型不符按文本顺序报第一处；这两条都由 serde 派生的读入顺序给出，自定义的 `Deserialize` 要照样在读完对象之后再报缺少的成员。

### 值模型

RFC-0010「模板」。模板参数与错误细节是 `serde_json::Value`：

- 整数：`Value` 里 `i64` 范围内的整数是整数；`u64` 的上半段（大于 `i64::MAX`）是双精度浮点；`i128` / `u128` 经 `json::to_value` 转，64 位之外的是双精度浮点。
- 浮点：`f64` 原样；`f32` 先换成最接近的 `f64`（`json::to_value(&0.1f32)` 是 `0.10000000149011612`）。`Value` 装不下非有限的浮点：`Value::from(f64::NAN)` 是 `null`，`json::to_value(&f64::NAN)` 是字符串 `"NaN"`；读入时上溢的数（`1e400`）进 `f64` 成员是无穷，进 `Value` 是 `null`，进 `json::Json` 照原文保留。
- 字符：`char` 经 `json::to_value` 是一个字符的字符串。`BTreeMap<String, _>`、`HashMap<String, _>` 与结构体是对象，`Vec`、数组、元组是数组。
- 模型之外的类型（`chrono` 的时间、`uuid::Uuid`、枚举等）：先按它们的 `Serialize`（`json::to_value`）或 `to_string()` 转成值，SDK 不另行格式化；要跨语言格式化时先转成字符串。

### 文法

规范里的正则都是手写的逐字节判定，整串匹配，结尾的换行不算匹配。

## 设计取舍

依赖：

| crate | 用途 |
| --- | --- |
| `serde`（`derive`）、`serde_json`（`raw_value`） | `Result` / `Error` / `Maybe` 的线上编码、XGI 载荷编解码、清单读写；`serde_json::Value` 是错误细节与模板参数的值类型；`raw_value` 让 `json::Json` 照原文写出 |
| `getrandom` | 追踪 ID、Span ID 与插件端点名的随机数 |
| `serde_path_to_error` | 载荷不合法时给出出错位置的 JSON 路径：RFC-0011 规则 2 要求 `reason` 只写路径，`serde_json` 本身只报行列 |
| `tonic`、`tonic-prost`、`prost` | 只在 feature `plugin`：`xihan.plugin.v1` 的 gRPC 服务端与回调客户端、protobuf 编解码 |
| `tokio`、`tokio-stream`、`hyper-util` | 只在 feature `plugin`：监听端点、在途调用、健康推送、停止宽限与 SIGTERM，回调时自建连接；只用到当前线程的运行时，多线程与否由插件作者选 |

- JSON 文本的共同读取检查是自带的逐字节读取器（`primitives::json`），读出保留文档顺序的值树，再经自带的 serde `Deserializer` 读入目标类型：`serde_json` 对重复成员取最后一个、不查孤立的代理项转义、把 `-0` 读成浮点、对 `1e400` 报错，都与规范不符。
- 规范里的文法是手写的逐字节判定，不依赖正则库；取消令牌与截止时间是自带的轻量实现，不依赖异步运行时。没有开发依赖。
- Rust 没有异常：编程错误 panic `FrameworkError`，可恢复失败是 `Err(Error)`，配置期失败是 `Err(FrameworkError)`。
- 协议代码 `src/plugin/generated/xihan.plugin.v1.rs` 由 `spec/plugin/plugin.proto` 生成并提交，使用者不需要 protoc。

## 一致性

- 现状：原语、清单、调用上下文、管道、操作与进程外插件运行时可用，示例 echo 通过规范黑盒运行器的全部用例。
- 认领的档（RFC-0024）：`core` 与 `plugin`；档的定义见规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。
- 缺口：无。
- 钉的规范版本：SDK 仓库 `spec/` 子模块指向的规范标签。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 尚未提供（不在认领的档里）：流式操作（操作契约还没有流式形态，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束）、Wasm 插件（RFC-0018，见 [Wasm 插件](/plugins/wasm)）与远程 Provider 的操作集（RFC-0019，首个是 `caching.distributed@1`）。
- 怎样验证：测试直接读取 `spec/conformance/vectors/`；[`tests/conformance_vectors.rs`](https://github.com/XiHanFun/framework-sdk-rust/blob/main/tests/conformance_vectors.rs) 按档表核对认领的档要求的每个向量要么被回放、要么列为缺口；[`tests/plugin_conformance.rs`](https://github.com/XiHanFun/framework-sdk-rust/blob/main/tests/plugin_conformance.rs) 构建示例 echo，用黑盒运行器跑全部用例。CI 在 ubuntu 与 windows 上跑这些测试，另以最低 Rust 版本编译库、测试与示例。

## 破坏性改动与迁移

首个对外版本是 `v0.2.0-rc.1`。之后的变化：

- `plugin::Network`、`Level`、`SubscriptionMode`、`HealthStatus` 与 `manifest::Kind` 加了 `#[non_exhaustive]` → crate 外穷尽的 `match` 加上通配分支。
- `manifest::Kind` / `Role` / `Isolation` 的 serde 只按字符串读写，不再接受 `{"plugin":null}` 这种写法 → 自己用 serde 存这些枚举的数据改成字符串。
- `Registry::add` / `add_with` 拒绝五个协议名之外的值与写了两次的协议名（`PRIMITIVES_VALIDATION_001`），原先能登记 → `with_expose` 只用 `EXPOSE_*` 常量，去掉重复。
- `Contract.expose` 的文档改准：空表示不对外发布（原先写成由宿主按缺省处理，行为没变）→ 要对外发布的操作补上 `with_expose`。
- `with_*`（`Error`、`Contract`、`Plugin`、`Subscription`、`Envelope`、`Context`）标了 `#[must_use]` → 丢掉返回值的调用本来就没有效果，改成接住返回值。
- 弃用 `Context::cancellation()`（旧名仍可用）→ 改用 `Context::cancellation_error()`。
- 弃用 `plugin::ERROR_CODE_UNEXPECTED`（旧名仍可用）→ 改用 `plugin::ERROR_CODE_PLUGIN_UNEXPECTED`。
