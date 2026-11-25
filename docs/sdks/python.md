# Python SDK

[framework-sdk-python](https://github.com/XiHanFun/framework-sdk-python) 是 [XiHan Spec](/appendix/spec) 的 Python 实现：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。规范以 submodule 放在 SDK 仓库的 `spec/`，一致性由规范仓库的向量与黑盒运行器验证；参考实现是 .NET（[XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)），规范写到的行为与 .NET 宿主一致。宿主一侧怎样加载插件见[进程外插件](/plugins/out-of-process)。

## 安装

```bash
pip install "xihan-framework==<版本>"
```

导入名是 `xihan_framework`。核心五块运行时零依赖；写进程外插件要装 extra `plugin`（见[写一个插件](#写一个插件)）。

目前只有预发布版，要写明版本号：`<版本>` 换成 PyPI 上的版本，按 PEP 440 写（标签的 `-rc.N` 写作 `rcN`），版本列表见 [Releases](https://github.com/XiHanFun/framework-sdk-python/releases)；不写版本会装到 PyPI 上占名用的旧版本。

## 模块

| 包 | 内容 | 规范 |
| --- | --- | --- |
| `primitives` | `Error` / 错误码、`Result[T]`、`Maybe[T]`、`TypedId[V]`、`LifecycleState`、`PipelineStage`、`Stability`、Baggage 键、消息键与模板、`Catalog`、`FrameworkError` | RFC-0001、RFC-0010、`conventions/` |
| `manifest` | `xihan.manifest.json` 的模型（冻结的 dataclass，构造时校验各字段，`validate()` 核对字段之间的约束）、读取与写出，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `invocation` | 不可变的 `Context`：截止时间、可跨线程触发的 `Cancellation`、W3C 追踪、已登记的 Baggage、类型化键的对象袋与按派生携带的值 | RFC-0003 |
| `pipeline` | 固定阶段的 `Builder`：`use` / `use_at` / `use_named_instance`、`before` / `after`、`extend`、`describe` / `dump`、`build` | RFC-0004 |
| `operation` | `Contract`、`Schema`、异步处理器、容量 10000 的 `Registry`、经完整管道执行的 `Invoker`（XGI：JSON 请求与响应）与类型化入口 `invoke_typed` | RFC-0011 |
| `plugin` | 进程外插件运行时 `Plugin`（协议 `xihan.plugin.v1`，基于 `grpc.aio`）、宿主回调客户端 `Host`、stderr 上的 JSON 日志、运行时的消息目录；协议代码在 `plugin.v1`。依赖 grpcio 与 protobuf，装 extra `plugin` | RFC-0017、`plugin/` |

## 快速开始

```python
import asyncio
from dataclasses import dataclass

from xihan_framework import operation, pipeline
from xihan_framework.invocation import Context
from xihan_framework.primitives import BAGGAGE_TENANT, Error, PipelineStage, Result, Stability, Unit, fail, ok


@dataclass(frozen=True)
class CreateOrder:
    quantity: int


@dataclass(frozen=True)
class OrderCreated:
    id: str
    tenant: str | None


async def create_order(ctx: Context, request: CreateOrder) -> Result[OrderCreated]:
    if request.quantity <= 0:
        return fail(
            Error.validation("ORDERS_VALIDATION_001", "orders.quantity.invalid", {"quantity": request.quantity})
        )
    return ok(OrderCreated(id="o-1", tenant=ctx.tenant))


async def observe(ctx: Context, next_step: pipeline.Step) -> Result[Unit]:
    outcome = await next_step(ctx)
    print(operation.current(ctx), outcome)
    return outcome


async def main() -> None:
    registry = operation.Registry()
    registry.add(
        operation.Contract("orders.create", stability=Stability.EXPERIMENTAL, expose=(operation.Expose.HTTP,)),
        create_order,
        request_type=CreateOrder,
        response_type=OrderCreated,
    )
    builder = pipeline.Builder().use_at(pipeline.func("Observe", observe), PipelineStage.OBSERVE)
    invoker = operation.Invoker(registry, builder)

    ctx = Context(timeout=2, baggage={BAGGAGE_TENANT: "t1"})
    response = await invoker.invoke(ctx, operation.Request("orders.create", b'{"quantity":2}'))
    print(response.outcome.is_ok, response.payload)  # True b'{"id":"o-1","tenant":"t1"}'


asyncio.run(main())
```

1. 可恢复失败返回 `fail(Error...)`，不抛异常；错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键 + 参数。`Error.message` 先经 `set_default_messages(Catalog(...), locale)` 安装的目录解析，再找 SDK 自带的内核错误英文模板（`BUILT_IN_CATALOG`），都解析不了时是 `键 (参数)`。`result.match(on_ok, on_failed)` 按成功或失败求值；必须成功才能继续的地方（启动、配置）用 `result.raise_if_failed()`。
2. 配置与编程错误（非法参数、清单字段不合格式、管道约束成环、操作重复登记）抛 `FrameworkError`，它是 `ValueError` 的子类，带错误码；操作重复登记与登记表已满的经 `to_error()` 交回带消息键与参数的错误。
3. 契约的 `expose` 是 `operation.Expose` 成员的元组（`EXPOSE_HTTP` 等常量是成员的别名），缺省为空，即不对外发布；五个协议之外的名字在构造时被拒绝。
4. 请求与响应是 dataclass（成员怎样对应 JSON 见[语言映射](#语言映射)），成员名用 `field(metadata=operation.json_field("ttlMs"))` 改写。
5. 处理器与中间件都是 `async def`，`invoker.dump()` 打印实际执行的管道（含三个内置步骤）。排在 `ObserveMiddleware` 之后抛出的异常（`Exception`）翻译为 `KERNEL_UNEXPECTED_001`，完整异常经 `logging` 的 `xihan_framework.operation` 记下；排在它之前的原样交给调用方。
6. 取消与截止时间只经信号通知：`ctx.cancellation.cancel()` 可以在任何线程调用，调用器不取消处理器的任务，处理器读 `ctx.is_cancelled`、`await ctx.cancellation.wait()` 或调用 `ctx.cancellation.raise_if_cancelled()` 自己停下；任务被别处取消时 `CancelledError` 照常传播。
7. 每次调用的操作描述、请求与响应随派生的上下文携带（`Context.with_value` / `find_value`），处理器或中间件里再调另一个操作、或在同一个上下文上并发调用互不覆盖。中间件用 `operation.current(ctx)`、`request_of`、`decoded_request_of`（Validate 阶段解码好的请求）与 `response_of`（编码之前的响应对象）读它们。
8. 模板：`format_message` 是宽松格式化（缺值的占位符原样保留），`try_format_message` 与 `Catalog` 是严格格式化（缺值即失败，退回描述）；它们与 `format_argument` 可带 `locale`。区域名按 RFC-0010 的文法校验并归一（`validate_locale_name`：`zh-cn` 即 `zh-CN`），查找不区分大小写。
9. 清单：`manifest.read(data)` / `manifest.read_file(path)` 返回 `Result[Manifest]`，失败都是 `KERNEL_CAPABILITY_006`；`read` 不跳过 BOM，`read_file` 按 UTF-8 读并跳过开头的 BOM。`manifest.write(m)` 按规范的排版写出。

## 写一个插件

这一节是插件一侧用 Python 的写法；宿主怎样发现、启动与降级插件，见[进程外插件](/plugins/out-of-process)。

```bash
pip install "xihan-framework[plugin]==<版本>"
```

grpcio 与 protobuf 只在 extra `plugin` 里，核心五块不依赖它们；没有装就导入 `xihan_framework.plugin` 时，错误里给出这条安装命令。

```python
import sys
from dataclasses import dataclass
from pathlib import Path

from xihan_framework import operation
from xihan_framework.invocation import Context
from xihan_framework.plugin import Envelope, Plugin, Subscription, SubscriptionHandler, run
from xihan_framework.primitives import Result, Unit, ok, success


@dataclass(frozen=True)
class Echo:
    text: str


async def echo(ctx: Context, request: Echo) -> Result[Echo]:
    return ok(request)


async def on_event(ctx: Context, envelope: Envelope, attempt: int) -> Result[Unit]:
    # 至少一次投递：处理器必须幂等，可按信封 id 去重
    return success()


def build() -> Plugin:
    registry = operation.Registry()
    registry.add(operation.Contract("echoes.create"), echo, request_type=Echo, response_type=Echo)
    return Plugin(
        Path(__file__).with_name("xihan.manifest.json").read_bytes(),
        operations=registry,
        subscriptions=[SubscriptionHandler(Subscription("echo.events"), on_event)],
    )


sys.exit(run(build))
```

清单 `xihan.manifest.json` 与脚本放在插件目录里：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.echo-python",
  "version": "0.1.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "contributes": { "operations": ["echoes.create"] },
  "activation": ["onStartup"],
  "entry": "echo"
}
```

1. 清单与启动前的核对：`kind` 为 `plugin`、`isolation` 为 `out-of-process`，`primitives` 是本 SDK 实现的原语层大版本 `1`，不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；登记的每个操作都要列在 `contributes.operations`，健康项列在 `contributes.health`，配置节写在 `contributes.config.section`。`Plugin` 构造时核对这些，以及订阅（同一（主题，类型）只声明一次）与消息目录（含 `en-US`），不合即抛出 `FrameworkError`。进程入口 `run(build)` 在构造失败时不监听、不写握手行，在 stderr 记一条 `error` 日志后以退出码 1 结束。握手交给宿主的清单必须与插件目录里的文件一致；`Plugin` 的清单参数省略时读工作目录里的文件。
2. 启动脚本：`entry` 是相对插件目录的可执行文件。宿主只继承 `PATH`、用户目录、临时目录等少数环境变量，所以脚本里写解释器与插件脚本的绝对路径，不依赖 `VIRTUAL_ENV`。Unix 上是带 shebang 的 sh 脚本，并 `chmod +x`；Windows 上是 `.cmd`，`entry` 写 `echo.cmd`（没有扩展名时宿主补的是 `.exe`）。

   ```sh
   #!/bin/sh
   exec /path/to/.venv/bin/python /path/to/echo/echo.py "$@"
   ```

   ```bat
   @echo off
   "C:\path\to\.venv\Scripts\python.exe" "C:\path\to\echo\echo.py" %*
   ```

3. 监听端点：默认是 Unix 域套接字，Linux、macOS 与 Windows 都用它（grpcio 在 Windows 上同样能监听 `unix:` 地址）。套接字放在本进程新建的私有目录里（`mkdtemp`，权限 `0700`，套接字 `0600`，停止后删除）；临时目录的路径带空白（如带空格的用户名）时改为监听回环 TCP。`Plugin(network=Network.TCP)` 只绑定 `127.0.0.1` 的随机端口，隔离只靠会话令牌。grpcio 不支持 Windows 命名管道，插件不监听 `npipe`。
4. 生命周期与配置：`Plugin(hooks=Hooks(configure=…, start=…, stop=…, health=…))` 接入回调，前三个是异步的，`health` 是同步的、应当只读状态。配置节原样交给作者，`Configuration.decode(T)` 按类型读：成员名按 ASCII 不区分大小写，字符串叶子按 JSON 数字的文法读成 `int` / `float`，`true` / `false` 读成 `bool`，读不出时抛出带 JSON 路径的 `FrameworkError`。回调抛出时，交给宿主的 `reason` 只是异常的简单类型名（如 `ValueError`），文本只写进 stderr。`Configuration.bindings` 是清单声明并获批的资源绑定，每个 `Binding` 带 `name`、`type`、主值 `value` 与只读的属性映射 `properties`：属性名区分大小写、原样交出，没有属性时是空映射，只给了属性的绑定 `value` 为空串。主值与属性值可能是凭据，不要写进日志；`Binding` 的 `repr` 与 `repr(binding.properties)` 只带绑定名、类型与属性名（值写成 `...`），`json.dumps(binding.properties)`、`dict(binding.properties)` 与 `dataclasses.asdict(binding)` 是自己取值，照样带出值。`Binding.properties` 与 `Envelope.extensions` 是 SDK 自己的只读映射（禁写的 `dict` 子类，对外声明为 `Mapping[str, str]`）：`Binding`、`Configuration` 与 `Envelope` 可以 `copy.deepcopy`、`pickle`、`dataclasses.asdict`（结果里是普通 `dict`），`Binding` 与 `Envelope` 还可以哈希。清单里的绑定缺省必需（`required` 缺省 `true`），部署里没有它时宿主不配置插件；可以没有的写 `"required": false`，这时 `bindings` 里可能没有它。
5. 回调宿主：`host_of(ctx)` 取宿主客户端（不是运行时建的上下文上抛出 `FrameworkError`；在运行时之外也要能跑的处理器用 `find_host(ctx)`，没有时是 `None`），`await host.invoke(ctx, "orders.create", payload)` 与 `await host.publish(ctx, envelopes)`（批量，逐项结果）。`Envelope.time` 是带时区的 `datetime`（`None` 为没有）：发布时换算成 UTC 写成 RFC 3339，以 `Z` 结尾，秒的小数去掉末尾的 0（全为 0 不写小数点），最多六位，如 `2026-11-25T03:25:00.5Z`。不带时区的 `datetime` 构造 `Envelope` 时抛 `FrameworkError`，不猜时区；年份范围不在本地核对，换算成 UTC 后落到 0001 年之前或 9999 年之后的照写，宿主以 `messaging.envelope.invalid_time` 拒绝那一项。收到的信封里 `time` 是 UTC 的 `datetime`，宿主写来的文本按 RFC-0003 规则 8 读，比微秒更细的小数截断；读不出时运行时以 gRPC `InvalidArgument` 拒绝这次投递。`Envelope.data_schema` 是 CloudEvents 的 `dataschema`（`None` 为没有），收到与发布都原样。清单 `permissions` 要声明 `capability:kernel.pipeline`（`Host.invoke`）或 `capability:messaging.queue`（`Host.publish`）并由宿主授予；权限、租户与截止时间在发出之前先在本地核对，不合不发请求。
6. 回调端点：SDK 按顺序取 `XIHAN_PLUGIN_HOST_ADDRESS` 里第一个 `unix` 或 `tcp` 项（grpcio 连不了命名管道），之后不换；宿主总把回环 TCP 放在最后，所以 Windows 上的 .NET 宿主经回环 TCP 回调，其他系统经 Unix 域套接字回调。没有能连的项时插件照常服务，回调以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable` 失败。
7. 取消与退出：调用方取消或截止时间到了只触发信号，不取消处理器的任务。`Stop`、stdin 关闭与 SIGTERM 按规范的时限收尾，`run` 以退出码 0 结束进程。
8. 日志与消息键：`run` 与 `Plugin.run()` 在根日志没有处理器时接上 `JsonLogHandler`，stderr 一行一个 JSON 对象；写 `extra` 时，`Binding.properties` 在任何一层都只写按升序排列的属性名。运行时与宿主共用的消息键由宿主的目录翻译，SDK 不随握手发送它们；`plugin.CATALOG`（目录名 `plugins.protocol`，10 种区域）供本地渲染。插件自己的目录用 `load_catalog(目录)` 从 `<区域>.json` 读，以 `Plugin(catalogs={名字: 目录})` 交给运行时，名字不能是 `plugins.protocol`。

用黑盒运行器验证（在 SDK 仓库检出的 `spec/conformance` 里运行，需要 Go）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --exclude echoes.crash --sample 'echoes.create={"text":"hi"}' --config '{"greeting":"hello"}'
```

完整的示例在 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-python/blob/main/examples/echo/echo.py)：操作、故意崩溃的 `echoes.crash`、事件订阅、健康项、类型化读取的配置与宿主回调。

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

Python 的写法与差异：

- **`Result` 的读取**分三种：`Result.from_json_value`（有值，`value` 必填且不能是 `null`）、`Result.unit_from_json_value`（无值，带了 `value` 即拒绝）、`Result.maybe_from_json_value`（值是 `Maybe`，`null` 即无值）。无值成功是 `success()`，只写 `{"ok":true}`。`Result` 可散列。
- **JSON 读取**：不用 `json.loads`（它接受 `NaN`、孤立的代理项、重复成员与很深的嵌套），由 SDK 自己按 RFC 8259 读；`str` 里的代理项码点按不合法的 Unicode 处理。
- **调用器**：进入 Resilience 阶段的 `DeadlineMiddleware` 就是 `Context.run`；`ctx.run(call)` 把一个派生的上下文交给 `call`，它的信号在调用方取消或到达截止时间时触发。响应里有 NaN 或无穷时调用以未处理失败结束，不写成 `null` 或字符串。
- **插件运行时**：调用与事件处理器在 `grpc.aio` 的同一个事件循环里执行。grpcio 按截止时间中止的回调报的就是 `DeadlineExceeded`，映射之外不另按时钟改写。
- **回调的载荷**：`Host.invoke` 的载荷与发布的信封 `data` 都只收 `bytes`，处理器也没有以 `str` 交回应答的接口，`plugin/invocation.md` §1、§5 的严格编码（ADR-0312、ADR-0315）对它们不适用：字节原样发出，由宿主核对。

## 语言映射

规范把一部分写法交给各语言自己声明，Python 的写法如下。

- **请求与响应的类型**：dataclass，也可以是原语、`list` / `tuple` / 键为 `str` 的 `dict`、枚举、`X | None`、`Maybe[T]`、`TypedId` 子类，或自带 `from_json_value` / `to_json_value` 的类型，用 SDK 自己的编解码。
- **请求类型的必填与可选**：dataclass 里没有缺省值的字段是必填的（`Maybe` 字段例外，没写时为无值）；有缺省值的字段（`= 0`、`= None`、`field(default_factory=list)`）是可选的，缺少时取缺省值。必填只要求成员出现，值为 `null` 也算出现。
- **接受 `null` 的成员**：只有 `X | None`、`Maybe[X]`、`JsonValue` / `Any` 接受 `null`，`null` 即没有值（`None`）。规范里接受 `null` 的字符串、数组与对象在 Python 里写成 `str | None`、`list[X] | None`、`Inner | None`，数组项要接受 `null` 时写 `list[str | None]`；写成 `str`、`list[str]` 的成员遇到 `null` 按类型不符报出。
- **数字类型**：`int` 是 64 位有符号整数；32 位整数写成 `Annotated[int, operation.Int32]`，其他范围用 `operation.IntRange(最小, 最大)`，超出范围即类型不符。`float` 是双精度浮点，也接受整数写法；`bool` 不是数字。`JsonValue` / `Any` 成员里的数字是保留原文的 `JsonNumber`（`float` 的子类，`text` 是原文；64 位范围内的整数仍是 `int`），写回时与读到的 JSON 值相等。
- **模板的值模型**：`None`、`bool`、`str`（含 `StrEnum`）、`int`（含 `IntEnum`；超出 64 位的是浮点）、`float`；`Decimal`、`Fraction` 等其他实数换成最接近的双精度浮点；`Mapping` 是对象，`list`、`tuple` 与其他序列、`set` / `frozenset`、`bytes` 是数组。模型之外的值取 `str(value)`：`datetime(2026, 11, 16, 8, 30)` 写 `2026-11-16 08:30:00`，普通 `Enum` 写 `Color.RED`；它们不跨语言，插件要跨语言格式化时先转成字符串。
- **线上编码的值**：Result 的值与错误细节里，超出 64 位的 `int` 按值模型写成浮点，`float('nan')` 与无穷写成字符串；`JsonNumber` 写回原文。
- **文法与空白**：规范的正则一律 `re.fullmatch`，结尾的换行不会被放过。空白指 RFC-0001 写死的 25 个码位，不用 `str.isspace`（它另把 U+001C–U+001F 算作空白），握手行与 `Error` 的文案共用这张表（`xihan_framework.plugin.is_whitespace`）。`Error.literal` 与 `with_text` 的文案为空或只有空白时抛出 `FrameworkError`。

## 设计取舍

- 面向 Python 应用、脚本型插件与 AI 工具链：Python 3.12+，处理器与中间件基于 asyncio，核心五块运行时零依赖。
- 进程外插件运行时基于 `grpc.aio`，与处理器在同一个事件循环里；grpcio 与 protobuf 只在 extra `plugin` 里。协议代码 `plugin.v1` 由 `spec/plugin/plugin.proto` 生成并提交。
- 取消只是通知：调用器与运行时不取消处理器的任务，处理器自己看信号；`Cancellation` 可以从任何线程触发。
- 清单与契约是冻结的 dataclass，构造时校验；配置与编程错误抛 `FrameworkError`，可恢复失败走 `Result`。

## 一致性

- 现状：原语、清单、调用上下文、管道、操作与进程外插件运行时可用，示例插件 echo 通过规范黑盒运行器的全部用例。
- 认领的档（RFC-0024）：`core` 与 `plugin`；档的定义见规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。
- 缺口：无。
- 钉的规范版本：SDK 仓库 `spec/` 子模块指向的提交。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 尚未提供：流式操作（操作契约还没有流式形态，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束）、Wasm 载体与远程 Provider 的插件侧（如 `caching.distributed@1`）。
- 怎样验证：单元测试直接读取 `spec/conformance/vectors/` 下的向量回放；`tests/test_vectors.py` 按档表核对认领的档要求的每个向量要么有测试回放、要么列为缺口；示例插件 echo 由规范的黑盒运行器跑全部用例。CI 在 Linux 与 Windows 上都跑。
- 信封的 `time`：回放 `plugin/sdk-envelope-times.json` 的微秒一列（ADR-0310）。写出的文本与 `text.microseconds` 逐字相同，读出的时刻与 `utc.microseconds` 相等（比微秒更细的小数截断），`unreadable` 的每一条投递都以 `InvalidArgument` 拒绝。`plugin/envelope-times.json` 现在同在 `plugin` 档。

## 破坏性改动与迁移

首个对外版本是 `v0.2.0-rc.1`。之后的变化：

- `Contract` 的 `expose` 在构造时校验并存成 `operation.Expose` 成员：五个协议之外的名字、单个字符串都抛 `FrameworkError`，原先不校验 → 写成元组 `(Expose.HTTP,)`，去掉不认识的名字；`EXPOSE_*` 常量照常可用。
- 区域名按 RFC-0010 校验并归一：`set_default_messages` 与 `Catalog` 遇到不合文法的区域名、只差大小写的两个区域名抛 `FrameworkError`，原先接受 → 区域名写成 `zh-CN` 这样的 BCP 47 形式。
- 区域名的查找不区分大小写：请求 `zh-cn` 命中 `zh-CN` 的模板，原先落回 `en-US` → 不用改；按旧文案断言的测试更新期望。
- 调用状态按调用隔离：处理器或中间件里再调另一个操作、或在同一个上下文上并发调用时，`operation.current` 等读到的是自己这次调用，原先互相覆盖 → 不用改。
- `operation.is_id`、`Contract(...)`、`Manifest(...)` 与 `manifest.read` 不再接受超过 128 个字符的操作 ID（抛 `FrameworkError`，读清单为 `kernel.manifest.invalid`），原先不设上限；新增常量 `operation.MAX_ID_LENGTH` → 把 ID 缩到 128 个字符以内。
- 清单 `activation` 里 `onEvent:` 的模式可以带下划线（如 `onEvent:order_events.*`），原先读不出 → 不用改。
- `plugin.Binding` 多了字段 `properties`（只读映射，第四个位置参数，缺省为空，ADR-0285），`repr` 不再带 `value` → 读绑定与三个位置参数的构造不用改；要把值写出来的直接读 `binding.value`。
- `Envelope.extensions` 由 `MappingProxyType` 换成 SDK 自己的只读映射（禁写的 `dict` 子类，`Binding.properties` 同样），`isinstance(..., MappingProxyType)` 不再成立、`isinstance(..., dict)` 为真；`Envelope` 因此可以 `copy.deepcopy`、`pickle`、`dataclasses.asdict` 与哈希 → 判断只读映射改用 `collections.abc.Mapping`。
- `Envelope.time` 由 `str | None` 改为带时区的 `datetime | None`（ADR-0306）；不带时区的抛 `FrameworkError`，给字符串抛 `TypeError` → 改传 `datetime`（如 `datetime.now(UTC)`、`datetime.fromisoformat(text)`），要文本时自己 `isoformat()`。
- 宿主写来的 `time` 读不出时投递以 `InvalidArgument` 拒绝，原先原样交给处理器 → 不用改。
- 新增 `Envelope.data_schema`（关键字参数，缺省 `None`，ADR-0305）→ 不用改。
- `JsonLogHandler` 写 `extra` 里的 `Binding.properties` 时只写属性名，原先写出值（ADR-0306）→ 确实要值的自己取 `dict(binding.properties)`。
