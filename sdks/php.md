来源：https://framework.docs.xihanfun.com/sdks/php

# PHP SDK

PHP SDK 是 [XiHan Spec](https://github.com/XiHanFun/framework-spec) 的 PHP 实现，仓库在 [XiHanFun/framework-sdk-php](https://github.com/XiHanFun/framework-sdk-php)，Composer 包名 `xihanfun/xihan-framework`：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。语义、错误码、消息键与线上编码按规范，与 .NET 参考实现（XiHan.Framework）一致，API 按 PHP 习惯；规范以 submodule 放在仓库的 `spec/`，一致性由规范仓库的向量验证。用它可以在 PHP 应用里按同一套契约执行操作与管道，也可以写由 .NET 宿主启动的进程外插件，宿主一侧的做法见[进程外插件](/plugins/out-of-process)，规范的组织与各语言的现状见[规范与多语言](/appendix/spec)。

## 安装

```bash
composer require xihanfun/xihan-framework:<版本>
```

要求 PHP 8.3+，运行时只依赖 PHP 核心与 `ext-json`。插件运行时另需可选依赖（`composer.json` 的 `suggest` 列出了它们，见[写一个插件](#写一个插件)）。目前只有预发布版，Composer 默认只装稳定版，要写明版本号：`<版本>` 换成 [Releases](https://github.com/XiHanFun/framework-sdk-php/releases) 里的版本（不带 `v`）。

## 模块

| 命名空间 | 内容 | 规范 |
| --- | --- | --- |
| `XiHan\Framework\Primitives` | `Error` / `ErrorCode`、`Result<T>`、`Maybe<T>`、`Unit`、`TypedId`、`LifecycleState`、`PipelineStage`、`Stability`、`BaggageKeys`、`OperationId`（操作 ID 文法，操作契约与清单共用）、`JsonPath`（出错位置的 JSON 路径）、消息键与模板（`MessageTemplate`、`MessageScope`、`MessageCatalog`、`LocaleName`）、内核消息的共用表（`KernelMessages`）、带错误码的异常（`FrameworkException`）、`FrameworkInfo` | RFC-0001、RFC-0010、RFC-0011、`conventions/` |
| `XiHan\Framework\Manifest` | `xihan.manifest.json` 的模型、读取（按 RFC-0002 的检查顺序报第一处问题，错误码 `KERNEL_CAPABILITY_006`）、按规范排版写出与跨字段校验，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `XiHan\Framework\Invocation` | `Context`：取消令牌与截止时间（`Cancellation`、`CancellationSource`、`DeadlineCancellation`、`Clock`）、W3C 追踪（`TraceParent`）、Baggage、对象袋（`Items`）；超时与取消翻译为 `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001` | RFC-0003 |
| `XiHan\Framework\Pipeline` | 十一阶段的 `PipelineBuilder`：`use` / `useAt` / `useNamedInstance`、`before` / `after`（两端是名字：按名字登记的实例名，或没有实例名的登记的中间件名，与 .NET 一致；约束主体在声明时必须已登记）、`describe` / `dump`、`build`；排序规则与错误码 `KERNEL_PIPELINE_00x`，重复登记、未知阶段与约束主体未登记或有歧义在登记时立即报，约束目标未登记、跨阶段与成环在排序时报；错误消息是 .NET 的英文原文，按名字登记的实例写成 `中间件名[实例名]`，成环时列出全部仍被卡住的中间件；运行期的成组登记与按归属撤回是宿主能力，不提供 | RFC-0004 |
| `XiHan\Framework\Operation` | `Contract`、`Schema`、`Shape`（请求的形状）、`Codec`、`Registry`（容量 10000）、经完整管道执行的 `Invoker`（XGI：JSON 请求与响应，带三个内置步骤）、`Current` | RFC-0011 |
| `XiHan\Framework\Plugin` | 进程外插件运行时：`Runtime::serve()`（启动前的核对、握手行、会话令牌、`Plugin` 服务的全部 RPC、生命周期状态机、stdin 关闭与 SIGTERM 时收尾退出）、`Options`、`Hooks`、`Subscription` / `Envelope`、`Health` / `HealthItem`、`Configuration`（配置节与类型化读取）、`Catalog`、`Endpoint`、宿主回调客户端 `Host`（`invoke`、`publish`）、写 stderr 的 JSON 日志 `Logger`、`PluginErrors` / `PluginMessages`（10 种区域）；协议消息 `Protocol\V1` 由 protoc 从 `spec/plugin/plugin.proto` 生成。依赖可选，见[写一个插件](#写一个插件) | RFC-0017、`spec/plugin/` |

## 快速开始

在 PHP 应用里用操作与管道：

```php
use XiHan\Framework\Invocation\Context;
use XiHan\Framework\Operation\{Codec, Contract, Expose, Invoker, Registry, Request, Shape};
use XiHan\Framework\Pipeline\{ClosureMiddleware, PipelineBuilder, Placement};
use XiHan\Framework\Primitives\{BaggageKeys, Error, PipelineStage, Result, Stability};

final readonly class CreateOrder
{
    public function __construct(public int $quantity) {}

    /** 请求的形状：quantity 是必填的 32 位整数 */
    public static function shape(): Shape
    {
        return Shape::object(['quantity' => Shape::int32()->required()]);
    }

    /** 用核对过的值创建请求 */
    public static function create(mixed $value): self
    {
        /** @var array{quantity: int} $value */
        return new self($value['quantity']);
    }
}

final readonly class OrderCreated
{
    public function __construct(public string $id, public int $quantity) {}
}

$registry = new Registry();
$registry->add(
    new Contract('orders.create', Stability::Stable, [Expose::Http], permission: 'orders.write'),
    static function (Context $context, CreateOrder $request): Result {
        if ($request->quantity <= 0) {
            return Result::fail(Error::validation('ORDERS_VALIDATION_001', 'orders.quantity.invalid', ['min' => 1]));
        }

        return Result::ok(new OrderCreated('o-' . $context->correlation(), $request->quantity));
    },
    Codec::typed(CreateOrder::class, CreateOrder::shape(), CreateOrder::create(...), OrderCreated::class),
);

$observe = new ClosureMiddleware('App\Observe', static function (Context $context, \Closure $next): Result {
    $outcome = $next($context);
    // 在这里记日志与指标；$outcome->error() 是失败原因

    return $outcome;
});
$invoker = new Invoker($registry, (new PipelineBuilder())->useAt($observe, PipelineStage::Observe, Placement::End, 'app'));

$context = Context::create(timeout: 2.0, baggage: [BaggageKeys::CORRELATION => 'c1']);
$response = $invoker->invoke($context, new Request('orders.create', '{"quantity":2}'));
echo json_encode($response->outcome), ' ', $response->payload; // {"ok":true} {"id":"o-c1","quantity":2}
```

- 操作用 `Registry::add` 登记：契约、处理函数、`Codec` 与可选的 `Schema`。PHP 没有不经反射的类型化 JSON 解码，所以 `Codec::typed` 显式给出请求类型名、请求的形状（`Shape`，根是 `Shape::object()` 或 `Shape::mapOf()`）、创建函数与响应类型名；不需要类型时用缺省的 `Codec::json()`，请求是按值模型读出的 `JsonObject`。
- 错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键；`Error::message()` 经 `MessageScope::setDefault(new MessageCatalog([...]), 'zh-CN')` 设置的目录按区域解析，没有时是「键 (参数)」。
- `Invoker::invokeTyped($context, $id, $request, $responseType)` 不经 JSON 直接以请求对象执行。

清单与原语：

```php
use XiHan\Framework\Manifest\Manifest;
use XiHan\Framework\Primitives\{Error, Result};

$manifest = Manifest::readFile(__DIR__ . '/xihan.manifest.json'); // 不合法抛 ConfigurationException（KERNEL_CAPABILITY_006）
echo $manifest->toJson();                                         // 按 RFC-0002「写出」排版，各语言逐字相同

echo Result::fail(Error::literal('ORDERS_CONFLICT_001', 'stale'))->toJson();
// {"ok":false,"error":{"code":"ORDERS_CONFLICT_001","message":"stale"}}
$result = Result::fromJson('{"ok":true,"value":{"id":"o1"}}');   // 有值结果；对象是 JsonObject，$result->valueOrThrow()['id']
$done = Result::unitFromJson('{"ok":true}');                       // 无值结果
$found = Result::maybeFromJson('{"ok":true,"value":null}');        // 值类型为 Maybe 的结果，null 即无值
```

## 写一个插件

进程外插件是宿主启动的子进程，以 gRPC 协议 `xihan.plugin.v1` 与宿主通信（RFC-0017）。宿主怎样发现、启动和接入插件见[进程外插件](/plugins/out-of-process)。插件运行时的依赖是可选的，用到时再装：

```bash
composer require xihanfun/xihan-framework amphp/socket amphp/http google/protobuf
```

缺依赖时 `Runtime::serve()` 拒绝启动：在 stderr 记一条 `error` 日志（写明要装的包），返回非零的退出码。入口脚本：

```php
use XiHan\Framework\Invocation\Context;
use XiHan\Framework\Operation\{Contract, Registry};
use XiHan\Framework\Plugin\{Options, Runtime};
use XiHan\Framework\Primitives\{JsonObject, Result, Stability};

require __DIR__ . '/vendor/autoload.php';

$registry = new Registry();
$registry->add(
    new Contract('greetings.create', Stability::Experimental),
    static fn (Context $context, JsonObject $request): Result => Result::ok(['text' => 'Hello, ' . $request['name']]),
);

// 宿主以插件目录为工作目录启动插件：握手时原样发出这份清单，宿主逐字段与目录里的文件比对
exit(Runtime::serve(new Options(
    manifest: (string) file_get_contents(getcwd() . '/xihan.manifest.json'),
    registry: $registry,
)));
```

- 清单（`xihan.manifest.json`）：`kind: plugin`、`isolation: out-of-process`，`primitives` 写本 SDK 支持的原语层大版本 `1`；插件服务的每个操作都列在 `contributes.operations`（插件只提供一元操作，流式操作等 RFC-0011 的流式契约），健康项列在 `contributes.health`；回调宿主要在 `permissions` 里声明 `capability:kernel.pipeline`（`Host::invoke`）或 `capability:messaging.queue`（`Host::publish`）；不能写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）。声明与清单不符时 `serve()` 拒绝启动：不监听、不写握手行，在 stderr 记一条 `error` 日志，返回 `Runtime::EXIT_REFUSED`（1）。
- 入口：`entry` 是相对插件目录的可执行文件。宿主只继承 `handshake.md` §1 列出的环境变量，启动脚本要写 PHP 与入口脚本的绝对路径：Unix 上是带 shebang、可执行的 sh 脚本（`exec /usr/local/bin/php /srv/greetings/plugin.php "$@"`），Windows 上是 `.cmd`（`@"C:\php\php.exe" "C:\srv\greetings\plugin.php" %*`）。
- 生命周期回调（`Hooks`：`configure`、`start`、`stop`、`health`）、事件订阅（`Subscription`，处理器收到 `Context`、`Envelope` 与投递次数）与插件自己的消息目录（`Catalog::fromDirectory()`）都在 `Options` 里给出。配置回调收到的 `Configuration` 除了原样的配置节，还能按键读 `string()`、`int()`、`float()`、`bool()`（成员名不区分 ASCII 大小写，.NET 宿主写来的叶子都是字符串）。处理函数里用 `Host::of($context)` 取宿主回调客户端。
- 用规范仓库的黑盒运行器验证（需要 Go）：在 `spec/conformance` 下运行 `go run ./cmd/xihan-conformance plugin --dir <插件目录> --json`，用 `--config`、`--sample <操作 ID>=<请求 JSON>` 与 `--exclude <操作 ID>` 给出配置节、请求样例与不调用的操作（参数见规范仓库的 [conformance/README.md](https://github.com/XiHanFun/framework-spec/blob/main/conformance/README.md)）。

完整的示例在 [examples/echo](https://github.com/XiHanFun/framework-sdk-php/tree/main/examples/echo)：一元操作、故意崩溃的操作、两种订阅方式、健康项、配置节与宿主回调；[tests/Plugin/ConformanceTest.php](https://github.com/XiHanFun/framework-sdk-php/blob/main/tests/Plugin/ConformanceTest.php) 把它放进临时目录、生成启动脚本，并用黑盒运行器跑全部用例。

## 与宿主共用的行为

### 操作、上下文与原语

- **载荷的解码**（RFC-0011 规则 2，PHP 的做法见[语言映射](#语言映射)）：先过 JSON 文本的共同检查，再按形状以文本顺序核对，第一处问题返回 `KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`，参数 `id` 与 `reason`。`reason` 只写出错位置的 JSON 路径：语法错误、`null`、不是对象与根上缺少必填成员写 `$`，重复成员写第一处重复的路径，值的类型不符写它的路径（如 `$.lines[1].quantity`，含空格、点、引号等字符的成员名写成 `$['unit price']`，`JsonPath` 按同一写法拼）。空载荷等价于 `{}`，只有空白不是 JSON。
- **类型化调用**：`invokeTyped` 进入管道前只核对两项，按这个顺序：未登记返回 `KERNEL_NOTFOUND_001`，类型与登记不符返回 `KERNEL_VALIDATION_002` + `kernel.operation.type_mismatch`，参数只有 `id`。
- **内置步骤**：调用器带三个内置步骤，位置与 .NET 的内置中间件相同（RFC-0011 规则 10）：都按归属 `xihan.kernel.core` 放在所在阶段的 `Beginning`，与同位置的其他中间件按 RFC-0004 排序（归属的序数序，`app` 排在它前面）。
  - Observe 阶段的 `ObserveMiddleware` 把排在它之后的中间件与处理函数抛出的异常翻译为 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`，框架异常的错误带在 `inner` 里（规则 5）；排在它之前的（Ingress 阶段，或 Observe 阶段 `Beginning` 上归属排在 `xihan.kernel.core` 之前的）抛出的异常原样抛给调用方。要记下原始异常，在 Observe 阶段的 `End` 放自己的中间件接住再抛出。
  - Resilience 阶段的 `DeadlineMiddleware` 在进入时按上下文的时钟核对截止时间，已到即返回 `KERNEL_TIMEOUT_001` + `kernel.invocation.deadline_before_start`，后面的中间件与处理函数不执行；进入时不核对取消，构造上下文时也不做这项判断。途中的取消按 RFC-0003 第 1 条归因：下游抛出 amphp 的 `Amp\CancelledException`（SDK 不依赖 amphp，按类名认出它；Observe 步骤不把它当未处理的异常），或把取消写成 `KERNEL_CANCELLED_001` 的结果时，截止时间已到即 `KERNEL_TIMEOUT_001` + `kernel.invocation.deadline_exceeded`，否则调用方的令牌已触发即 `KERNEL_CANCELLED_001` + `kernel.invocation.cancelled`；两个都没有触发的取消异常原样抛给调用方，与 .NET 一致。
  - Validate 阶段的 `ValidationMiddleware` 在原始调用里解码载荷（`Current::requestObject()` 之后可取），失败即返回 `KERNEL_VALIDATION_001`；类型化调用跳过。
  - 传入的 `PipelineBuilder` 不变，内置步骤登记在它的副本上；再登记同名的中间件是重复登记。
- **响应的编码**：处理函数成功时响应对象写进对象袋（`Current::responseObject()`），XGI 入口在管道结束之后才把它编码成载荷，中间件看到的是编码之前的结果（RFC-0011 规则 10「编码」）。写不出时（浮点是 NaN 或无穷、非法 UTF-8、编码函数抛出、不支持的值）返回 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`，框架异常的错误带在 `inner` 里，不抛给调用方，没有载荷；类型化的 NaN 不写成 `null` 或字符串。
- **没有响应**：管道以成功结束却没有响应（中间件以成功短路、处理函数没有执行，又没有经 `Current::setResponsePayload` 交出记录的响应）时，两种入口都返回 `KERNEL_UNEXPECTED_001` + `kernel.operation.no_response`（RFC-0011 规则 9）。
- **取消与截止时间**：只经令牌通知，调用器不强行中止中间件或处理函数：令牌触发后处理函数照常返回的结果就是调用的结果（RFC-0003 第 1 条）。
- **Baggage 与 `traceparent`**：已登记 Baggage 键的值要非空、按 UTF-16 码元计不超过 256、不含 U+0000–U+001F 与 U+007F–U+009F（conventions/baggage-keys.md）：`Context::create(baggage: …)` 是外部输入，丢弃不合的成员；`withBaggage()` 是程序内逐项派生，给出不合的值抛 `InvalidArgumentException`；`xihan.idempotency-key` 两种情形都例外。`traceparent` 只认版本 `00` 与小写十六进制，不合法的当作没有给出、生成新的追踪；是否采样只看标志的最低位，写出时只写采样位（`00` / `01`），没有上游时写 `01`。
- **内核错误**：码、键、参数与 en-US 模板与规范的共用表（`kernel-messages.json`）逐字一致，`KernelMessages` 自带这些英文模板（另有清单读取的 `kernel.manifest.*` 与 `primitives.manifest.*`，取自 .NET 的 en-US 语言文件）：目录解析不出内核键时用它们，配置期异常的消息也用它们写成英文。重复登记与登记表已满带共用表的键与参数（`kernel.operation.duplicate` 的 `id`、`owner`，`kernel.operation.registry_full` 的 `capacity`）。
- **模板**：按 RFC-0010「模板」格式化。
  - 值先归到值模型（PHP 的归法见[语言映射](#语言映射)），数组与对象写成紧凑的 JSON，对象成员按名字的 UTF-16 码元序排列，超过 64 层的容器写 `null`。
  - 格式说明符只认 `^[DdFfNnXx][0-9]{0,2}$`：`D` 与 `X` / `x` 只用于整数（`X` 只用于非负整数），`F` 与 `N` 用于整数与浮点，按浮点数的精确值舍入、正好一半取偶（`{n:F2}` 取 0.125 得 `0.12`、取 2.675 得 `2.67`）；其余说明符（`E2`、`G`、`0.00`、`yyyy`、printf 写法、`D100`）与不适用的值一律按无格式输出。
  - 值为 `null` 写空串，浮点在 1e-4 到 1e17 之间写最短往返的定点形式、范围外写成 `1E+17`、`9.9E-05` 这样的指数，负零写 `-0`，非数与无穷写 `NaN`、`Infinity`、`-Infinity`。
  - `MessageTemplate::format()` / `tryFormat()` 与 `MessageCatalog` 按区域取小数点与分组符（`MessageTemplate::numberSymbols()`）：de-DE、es-ES、pt-BR 是 `,` 与 `.`，fr-FR 是 `,` 与 U+202F，ru-RU 是 `,` 与 U+00A0，其余区域与不给区域都是 `.` 与 `,`；小数点用在无格式的浮点与 `F`、`N` 上，分组符只用在 `N` 上，整数无格式时不分组，`D`、`X` 与数组、对象里的数字不受区域影响。
  - `describe` 按不变文化，参数按名字的序数顺序排列；消息键最长 128 个字符。
- **清单的读取**：按 RFC-0002 解析规则 2 的顺序只报第一处问题，失败都抛 `ConfigurationException`，`toError()` 是逐字的错误，码都是 `KERNEL_CAPABILITY_006`，`message()` 与异常消息是共用表（`kernel-messages.json`）里这些键的英文模板格式化后的句子：
  1. JSON 文本的共同检查：不是 JSON 为 `kernel.manifest.invalid_json`，`reason` 为 `$`，任何一层有重复成员时为第一处重复的路径。`read()` 按原样检查文本，开头的 BOM 是语法错误；`readFile()` 跳过开头的 UTF-8 BOM，文件不存在为 `kernel.manifest.file_missing`（参数 `path`）。
  2. 根是 `null`：`kernel.manifest.empty`，没有参数。
  3. 按 Schema 逐项核对：`kernel.manifest.invalid`，`reason` 是第一处问题的路径。成员按文本顺序，每个成员依次查 Schema 之外的成员、`null`、类型、取值，对象与数组进到里面再查；对象的成员查完后按 `required` 的顺序报缺少的成员。接口版本不带前导零、不超过 2147483647，只有 `requires` 的版本可带一个 `^`。
  4. 跨字段约束（`Manifest::validate()`，构造清单时不核对）：按 `isolation_required`、`isolation_not_allowed`、`duplicate_provides`、`duplicate_requires`、`duplicate_bindings` 的顺序，键是 `primitives.manifest.*`，参数只有 `id`。只有 Wasm 插件能贡献中间件这一条由宿主在发现插件时核对，读取时不核对。
- **清单的写出**：`toJson()` 的排版写死：两个空格缩进，换行只用 LF，末尾不换行，`": "`，空数组写 `[]`；成员顺序固定，可选成员没有值不写，列表总写，`$schema` 写 `https://spec.xihanfun.com/manifest/v1.json`，`role` 缺省写 `provider`，`activation` 缺省写 `["onStartup"]`，`requires` 的版本写成不带 `^` 的数字；字符串只转义必须转义的字符。
- **Result 与 Error 的编码**：`Result<T>`、`Error`、`Maybe<T>` 的 JSON 编码按 RFC-0001「Result」（ADR-0233），与 .NET 参考实现一致：有值成功 `{"ok":true,"value":…}`，无值成功只写 `{"ok":true}`，失败 `{"ok":false,"error":{…}}`。读取时按结果的种类选入口：先过 JSON 文本的共同检查（任何一层的重复成员都拒绝，含 `value`、`details` 与 `inner` 的内部），再拒绝无值结果带了 `value`（`{}` 与 `null` 也算）、失败带 `value`、成功带 `error`、可选成员写成 `null`、`Error` 缺 `message`、`message` 为空或只有空白。空白是 RFC-0001「空白字符」的 25 个码位，按 UTF-8 码位判断，不用只认 ASCII 的 `ctype_space` 与 `trim`；`Error::literal()` 与 `withText()` 的文案同样，不合时抛 `InvalidArgumentException`；未知成员忽略；共同检查只在 JSON 文本入口（`fromJson` 一类）进行，`fromWire` 收到的已解码值看不出重复。写出用 `toJson()`：数字按值模型往返，64 位整数精确，整数值的浮点写成 `2.0`、`-0.0`、`1.0e+17`，非有限的浮点写成字符串 `NaN`、`Infinity`、`-Infinity`；自己 `json_encode` 时要带 `JSON_PRESERVE_ZERO_FRACTION`，并把 `serialize_precision` 设为 `-1`。

### 进程外插件

以下各条依据 RFC-0017 与规范仓库的 `plugin/` 目录：[handshake.md](https://github.com/XiHanFun/framework-spec/blob/main/plugin/handshake.md)、[lifecycle.md](https://github.com/XiHanFun/framework-spec/blob/main/plugin/lifecycle.md) §7 与 [invocation.md](https://github.com/XiHanFun/framework-spec/blob/main/plugin/invocation.md)。

- **启动**（`handshake.md` §1）：在监听与写握手行之前核对清单与声明：`kind` 是 `plugin`、`isolation` 是 `out-of-process`、没有 `contributes.pipeline`、`primitives` 等于本 SDK 支持的原语层大版本 `1`、登记的操作都在 `contributes.operations` 里、订阅的（主题，类型）不重复；主题文法、操作不重复与目录含 `en-US` 在构造 `Subscription`、`Registry`、`Catalog` 时已经核对，健康项在运行期按清单过滤。清单不合法、缺 `XIHAN_PLUGIN_SESSION` 或 `XIHAN_PLUGIN_PROTOCOL_VERSIONS`、监听失败同样拒绝启动。拒绝启动时不监听、不写握手行，在 stderr 记一条 `error` 日志，`serve()` 返回 `Runtime::EXIT_REFUSED`（1），宿主看到的是进程在握手之前退出。监听之后向 stdout 写一行 `xihan-plugin/1 <network> <address>`，之后不再写 stdout。
- **端点**（§2、§4）：Linux 与 macOS 上是 Unix 域套接字，放在本进程新建的私有目录里（`0700`、名字随机，已存在就换一个名字），套接字 `0600`，停止后删除；Windows 上是回环 TCP（`127.0.0.1` 的随机端口），同机的其他用户连得上，隔离只靠会话令牌。自己生成的地址不含规范所说的空白（RFC-0001「空白字符」，即 §2 的集合，与 .NET 正则的 `\s` 相同），临时目录的路径含空白、不是合法的 UTF-8 或太长时改用 `/tmp`。`Options` 的 `network` 与 `address` 可以改：`tcp` 只接受 `127.0.0.0/8` 的点分十进制（不带前导零）或 `[::1]` 加 1–65535 的端口，`npipe` 不支持——PHP 不能以非阻塞方式服务命名管道，所以本 SDK 不监听命名管道，也就不涉及命名管道的访问控制。`Endpoint` 按同样的空白集合与 tcp 文法解析握手行与端点。
- **回调端点**（§4）：`XIHAN_PLUGIN_HOST_ADDRESS` 是按宿主的偏好排序、以 `;` 分隔的列表，任何一项不合法整个值就不合法。插件启动时按顺序取第一个本 SDK 能连的项，之后不换：Unix 域套接字（Windows 之外）与回环 TCP，PHP 连不了命名管道。.NET 宿主总在最后给出一个回环 TCP 端点（Windows 上是命名管道加回环 TCP），所以 Windows 上回调经回环 TCP 进行。变量缺失、不合法或没有能连的项时照常服务，启动时在 stderr 记一条 `warning`，每次回调都以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `no supported callback endpoint`）失败，不发请求；`Host::endpoint()` 为 `null`。
- **会话令牌**（§4）：每个 RPC（含 `Handshake`）先按常数时间（`hash_equals`）核对元数据 `xihan-plugin-session`，必须恰好一个值：没有、不匹配或有两个及以上（即使其中一个正确）都以 `Unauthenticated` 拒绝，不执行；令牌的核对先于次序核对与流式调用读第一帧。
- **握手**（§3）：选宿主列表里最高的共同版本（本 SDK 支持 `1`），没有共同版本以 `FailedPrecondition` 拒绝；`primitives` 写清单的值，`manifest` 写清单原文、去掉开头的 UTF-8 BOM；操作声明来自 `Registry`，只有一元操作（`streaming: false`，按 ID 的字节序），连同契约与 JSON Schema；订阅按声明的顺序，`mode` 总写 `competing` 或 `broadcast`；`catalogs` 只有插件自己的目录（区域按名字排序），运行时的消息键由宿主提供，不随握手发送。
- **调用次序**（`lifecycle.md` §7.1）：`Handshake` 只在 `Declared` 接受（之后 `Resolved`），`Configure` 只在 `Resolved`（之后 `Configured`，失败即 `Failed`），`Start` 只在 `Configured`，`Stop` 在 `Declared`、`Draining`、`Stopped` 之外都接受；`Health`、`WatchHealth`、`Invoke`、`InvokeStream`、`HandleEvent` 任何状态都接受。不在表里的组合以 `FailedPrecondition` 拒绝，什么都不做，状态不变。
- **配置**（§7.2）：`configuration` 为空等同 `{}`；通不过 JSON 文本的共同检查或不是对象时不调配置回调，返回 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为 `configure`，`reason` 为 `the configuration is not a JSON object`），插件进入 `Failed`。`Configuration` 原样带着配置节，另有类型化读取（`value()`、`string()`、`int()`、`float()`、`bool()`）：键按 `:` 分段，数组的段是下标，成员名按 ASCII 不区分大小写匹配；字符串叶子读成数字时按 JSON 数字的文法（读整数时不带小数与指数、在 64 位范围内），读成布尔时只认 `true`、`false`（ASCII 不区分大小写）；找不到为 `null`，读不成时抛 `InvalidArgumentException`。回调前的权限核对以 `granted_permissions` 为准。
- **启动与就绪**（§7.3）：启动回调成功后进入 `Started`；有健康回调就立即求一次，按结果进入 `Ready`、`Degraded` 或 `Failed`（报其他状态的按 `Ready`，抛出的按 `Degraded`），没有就进入 `Ready`，然后才应答。只有 `Ready`、`Degraded` 可服务，其余状态下到达的调用与投递以 `PLUGINS_UNAVAILABLE_001` + `plugins.process.unavailable` 失败（`state` 是此刻的状态名，如 `Configured`、`Draining`、`Failed`）。
- **健康**（§7.4）：`Start` 之前报本地状态（`Declared`、`Resolved`、`Configured`），没有健康项，不调健康回调；可服务期间每次 `Health` 都求一次健康回调，只有它报的 `Ready`、`Degraded`、`Failed` 改变状态（按合法迁移表，`Failed` 之后不再回来），`contributes.health` 之外的健康项丢掉、同一个名字在进程里只记一条 `warning`；健康回调抛出时这一次是 `Degraded`，`error` 为 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为 `health`），没有健康项，状态也迁到 `Degraded`，之后的结果照常决定状态。`WatchHealth` 先推当前状态，之后状态、健康项或 `error` 变化时推送，可服务期间每 10 秒重新求一次健康回调，进入 `Draining` 时推一次，应答 `Stop` 后结束流。
- **停止**（§7.5）：启动回调没有成功过时不调停止回调，应答 `ok` 后以退出码 0 退出；`Started`、`Ready`、`Degraded` 进入 `Draining`，新的调用与投递以 `PLUGINS_UNAVAILABLE_001` 拒绝，等在途调用到宽限期（`grace_seconds` 为 0 按 5 秒），到时触发它们的取消信号、不再等，然后调停止回调（原因原样、剩余的宽限秒数），应答，以 0 退出；启动成功过之后进入 `Failed` 的同样调停止回调，不迁移状态。停止回调在进程里至多调一次，失败或抛出写进 `StopResponse.status`，照常退出。
- **回调抛出**（§7.6）：配置、启动、停止与健康回调抛出时，异常的文本只写进 stderr 日志，交给宿主的 `reason` 是异常的简单类名（不带命名空间；匿名类写成 `父类@anonymous`，去掉 PHP 附在名字后面的文件路径）；返回的不是 `Result`（健康回调不是 `Health`）按抛出 `TypeError` 处理。
- **收尾**（§7.7）：stdin 读到结尾与 SIGTERM 都是收尾请求：可服务的插件进入 `Draining`，立即触发全部在途调用的取消信号并等它们结束，启动回调成功过而停止回调还没调过的调一次（原因 `shutdown`）；从收到请求起最多 3 秒，到时不论是否完成都以 0 退出，进行中的 `Stop` 同样受这 3 秒限制。SIGTERM 要 pcntl 扩展（Revolt 靠它分派信号，只在 Unix 上有）：没有 pcntl 时 SIGTERM 按系统缺省终止进程；Windows 没有 SIGTERM，宿主以关闭 stdin 作为终止信号。
- **调用与上下文**（`invocation.md` §1、§2）：`Invoke` 依次核对会话令牌、操作在握手声明里（否则 `Unimplemented`）、插件可服务（否则 `PLUGINS_UNAVAILABLE_001`），然后从 `InvocationMetadata` 还原上下文，经 SDK 的调用器与完整管道执行，`payload` 为空等同 `{}`。还原上下文时：`trace_parent` 按 RFC-0003 的文法取，不合文法即没有父 Span、开始新的追踪；`trace_state` 不进上下文；`baggage` 只留已登记的键（`xihan.principal` 原样交给作者）；`deadline` 按严格文法读（年 0001–9999、日期按公历、不认闰秒、换算成 UTC 后在范围内；PHP 的时刻精确到微秒，更细的小数截断），读不出当作没有，生效的截止时间取它与 gRPC 截止时间中较早的一个。宿主重置流即触发取消令牌，到截止时刻令牌准时触发（事件循环的定时器先于上下文的时钟到达截止时间时，运行时按剩下的时间再等）。业务失败写进 `Status.error`，gRPC 状态只表示协议层的失败。
- **流式**（§1）：RFC-0011 定义流式操作契约之前，插件不实现流式调用、也不声明流式操作，本 SDK 不提供流式操作的公开 API：`InvokeStream` 读第一帧，流在第一帧之前结束或第一帧不是 `start` 时以 `InvalidArgument` 结束，是 `start` 时以 `Unimplemented` 结束，不论操作是否声明、插件是否就绪。
- **错误的写出与读入**（§3）：带消息键的错误 `text` 留空，由宿主按目录翻译成调用方的语言，只有自由文本错误写 `text`；`details` 是参数与细节的 JSON 对象文本，没有时为空；`inner` 照写。读宿主写来的 `Error`（回放 `error-reading.json`）：`code` 不合文法时整个错误换成 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 一律为 `invoke`，`Host.Publish` 的结果也一样，`reason` 为 `invalid error code '<code>'`，原来的 `inner` 丢掉）；`message_key` 合文法的按键处理（不取 `text`），否则是自由文本错误，文案取 `text`，为空或只有空白时取 `code`；`details` 为空、只有空白、是空对象、通不过共同检查、不是对象，或任何一层有读成双精度会溢出（±∞）的数时当作没有（下溢的数读成 0，超出 64 位的整数读成双精度），`inner` 同此；`ok` 为假却没有 `error`、或整个 `status` 缺失时为 `plugins.process.failed`，`phase` 为操作 ID 或 `publish`，`reason` 为 `the host reported a failure without an error`。
- **投递**（§4）：依次核对会话令牌；没有 `envelope` 或 `mode` 不是空、`competing`、`broadcast` 时以 `InvalidArgument` 拒绝；按（主题，方式，类型）找订阅，一个也没有时为 `KERNEL_NOTFOUND_001` + `plugins.event.unsubscribed`（`id`、`topic`、`type`、`mode`，空方式写 `competing`），不改交给另一种方式的订阅；不可服务时为 `PLUGINS_UNAVAILABLE_001`；然后还原上下文，匹配的订阅按声明顺序依次交给各自的处理器，第一个失败即作为结果，处理器抛出为 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`。
- **回调**（§5）：`Host::invoke` 与 `Host::publish` 发出之前在本地依次核对，不合即不发：有没有可用的回调端点（见「回调端点」）；权限（Invoke 要 `capability:kernel.pipeline`，Publish 要 `capability:messaging.queue`，既在清单 `permissions` 里、也在 `Configure` 授予的权限里，否则 `PLUGINS_DENIED_002` + `plugins.callback.denied`）；回调上下文的 `xihan.tenant` 是不是插件此刻正在处理的某个调用或投递的租户（否则 `PLUGINS_DENIED_002` + `plugins.callback.tenant_denied`）；最后，回调上下文的截止时间已过或已被取消时也不发请求，结果与下面映射表相应的一行相同（截止时间已过按 `DeadlineExceeded`，已被取消按「`Cancelled`，取消信号已触发」）。`publish` 的这几项都是整批的结果；信封的 `xihantenant` 不在本地核对，由宿主逐项判定。回调带会话令牌，`metadata` 取自所在的调用上下文：按 RFC-0003 规则 7 写出的 traceparent（本 SDK 不带 Observe 阶段的 Span，按规则取上下文的 parent-id 与标志，标志只写采样位，上游的 `00` 原样传下去），按 UTC 七位小数 `+00:00` 写的 `deadline`、已登记的 Baggage、空的 `trace_state`，gRPC 截止时间与 `deadline` 相同，调用方的取消即取消回调；上下文的令牌在截止时刻触发时不取消回调，由 gRPC 截止时间以 `DeadlineExceeded` 结束。本 SDK 的 gRPC 客户端按截止时间在本端中止调用时报 `DeadlineExceeded`，回调客户端只按表映射、不另按时钟或令牌改写结果。gRPC 状态按 §5 的表映射：`Unauthenticated` 为 `plugins.callback.session_invalid`，`PermissionDenied` 为 `plugins.callback.denied`，`DeadlineExceeded` 为 `KERNEL_TIMEOUT_001`，调用上下文已取消的 `Cancelled` 为 `KERNEL_CANCELLED_001`，没有取消的 `Cancelled`、`Unavailable` 与连不上为 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `<状态名>: <说明>`），`Host.Invoke` 的 `Unimplemented` 为 `kernel.operation.unknown`、`InvalidArgument` 为 `kernel.operation.invalid_payload`，其余（含 `Host.Publish` 的这两种）为 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为操作 ID 或 `publish`）。`publish` 空列表不发；RPC 失败时每项同一个错误，结果数与信封数不同时每项都是 `plugins.process.failed`（`reason` 为 `the host returned <n> results for <m> envelopes`）。
- **日志**：写 stderr，每行一个 JSON 对象：`level`（`trace` / `debug` / `information` / `warning` / `error` / `critical`）、`message`，其余成员是作用域属性（`plugin`、`instance` 等）；`Logger` 也可以给插件自己用。
- **消息键**（§8）：运行时的错误码、消息键与参数以规范的 `plugin-messages.json` 为准：`plugins.process.unavailable`、`plugins.process.failed`、`plugins.event.unsubscribed`、`plugins.host.unavailable`、`plugins.callback.denied`、`plugins.callback.tenant_denied`、`plugins.callback.session_invalid`；参数就是错误的 `details`，`id` 一律是清单 ID，`reason` 是不本地化的英文。10 种区域的模板在 `src/Plugin/Localization`（`PluginMessages::catalog()`，供本地渲染），en-US 另有内置模板；宿主自己有这些键的目录，握手不发送它们。

## 语言映射

规范把取决于语言的地方交给各 SDK 写明（ADR-0236），PHP 的做法：

- **JSON 文本的共同检查**（RFC-0001「JSON 文本」）：语法、嵌套层数、孤立的代理项转义与原始控制字符交给 `json_decode`，非法 UTF-8（含过长编码与编码成 UTF-8 的代理项）由 PCRE 的 UTF-8 校验拒绝，重复成员在原文上找（`json_decode` 遇到重复取最后一个）。载荷与清单过了检查之后按内部的读取器读成保留文本顺序与原文写法的树，不经 `stdClass`，以 U+0000 开头的成员名照常核对、报路径。线上编码（`Result::fromJson`、`Error::fromJson`）与无类型的载荷（`Codec::json()`）用同一个读取器按值模型读，同样不经 `stdClass`。
- **文法的匹配**（RFC-0001「文法的匹配」）：PCRE 的 `$` 在结尾的换行之前也算匹配，所以规范文法的正则都带 `D` 修饰符，`xihan.tenant\n`、带换行的 `traceparent` 与 `kernel.hosting@1\n` 都不合法。
- **值模型**（RFC-0010「模板」）：`null`、`bool`、`string`、`int`（PHP 的 `int` 就是 64 位）、`float` 原样；`json_decode` 把 64 位范围内的整数写法读成 `int`，其余读成 `float`，与模型一致。SDK 读出的 JSON 对象是 `JsonObject`：不可变，成员按文本顺序，成员名原样（以 U+0000 开头、纯数字的都行），`{}` 与 `[]` 不混淆，用 `$object['name']`、`get()` 取成员，`with()` 得到改了一个成员的新对象，`toJson()` 与响应编码按成员原样写出；自己 `json_encode` 时取 `stdClass`（成员名以 U+0000 开头时退成关联数组）。PHP 自己的数组是列表（`array_is_list`，含空数组 `[]`）时是数组，否则是对象，键写成字符串；`stdClass` 也是对象。模型之外的值写成 PHP 的文本：`BackedEnum` 取值的文本，`UnitEnum` 取名字，`Stringable` 取 `(string)`，`DateTimeInterface` 取 ATOM 格式（`2026-11-16T10:00:00+08:00`），`JsonSerializable` 取 `jsonSerialize()` 再归入，其余对象写类型名。它们不跨语言，插件要跨语言格式化时先转成字符串。
- **请求的形状**（RFC-0011 规则 2）：成员用 `Shape` 声明，成员名区分大小写，未知成员忽略，数字不从字符串读。
  - 必填：`Shape::string()->required()`，缺少时在所在对象结束时报该对象的路径；只要求出现，值为 `null` 也算出现。
  - 可选：不加 `required()`，缺少时交给创建函数的数组里没有这个键。
  - 接受 `null`：`string()`、`listOf()`、`mapOf()`、`object()` 与 `any()` 接受 `null`（等于没有值），`int32()`、`int64()`、`float()`、`bool()` 与它们的数组项、字典值不接受，加 `nullable()` 才接受。
  - 整数不接受 `1.0` 与 `1e2`，超出范围即不符；`float()` 接受整数写法，`1e400` 是无穷，`1e-400` 是 0。
  - 交给创建函数的值：对象与字典是以成员名为键的数组（PHP 会把纯数字的键转成整数）。
  - 任意 JSON：`any()` 交出 `RawJson`，`json` 是读到的原文，`decode()` 取 `json_decode` 的对象模式结果；响应与 `toJson()` 写出时把原文原样放回，`1e400`、超出双精度的整数与 `2.0` 写回时 JSON 值不变（RFC-0011 规则 2）。`RawJson::of()` 包装自己的 JSON 文本，先过共同检查。
  - `Codec::json()` 不核对形状，请求是按值模型读出的 `JsonObject`：64 位范围内的整数精确，`1e400` 这类上溢的数字照 .NET 读成双精度无穷——放进错误细节这类无类型的值时写成字符串 `"Infinity"`，作为响应原样返回时写不出（`unhandled_exception`）。要原样保留原文时用 `typed()` 与 `any()`。

## 设计取舍

- **失败**：可恢复失败返回 `Result::fail(...)`，不抛异常；编程错误与配置错误抛带错误码的异常（`FrameworkException` 接口）：参数非法是 `InvalidArgumentException`，清单文件、管道约束与操作重复登记这类装配错误是 `ConfigurationException`，强行从失败的 `Result` 取值是 `FailureException`。
- **同步签名与取消**：中间件与处理函数的签名是同步的，可以运行在 Fiber 里。取消令牌可轮询、可登记回调：`$context->cancellation()->isRequested()`，`$context->cancellationError()` 给出 `KERNEL_TIMEOUT_001` 或 `KERNEL_CANCELLED_001`；没有事件循环时截止时间在轮询时核对时钟，接入事件循环时宿主在截止时刻调用 `DeadlineCancellation::poll()`。
- **泛型**：以 PHPStan 的 `@template` 标注。
- **插件运行时**：运行在 amphp 的事件循环（Revolt）上，每个调用一个 Fiber；处理函数仍是同步签名，阻塞的处理函数会挡住同一进程里的其他调用，耗时的 I/O 用 amphp 的非阻塞接口。PHP 的 gRPC 扩展只有客户端，所以 gRPC 的服务端与客户端（明文 HTTP/2，按先验知识，不经升级）由本 SDK 实现：帧的解析与 HPACK 用 amphp/http，连接、流、流量控制与 gRPC 的消息、状态与尾部在 `Plugin\Internal\Grpc`。插件协议的连接都在本机、长期保持（`WatchHealth` 一直开着），所以不设空闲超时与 PING 次数这类面向公网的限制；内存由流的接收窗口、每条消息 4 MiB 的上限与 100 个并发流限定。协议消息用 google/protobuf 的纯 PHP 运行时。不需要额外的 PHP 扩展：amphp/socket 要的 openssl 与 amphp/http 间接要的 mbstring 在官方的 `php:8.3-cli` 镜像里都有。
- **Windows**：PHP 读非阻塞的管道时，管道开着却没有数据会一直等，`stream_select` 也分不出管道是否可读，所以 Windows 上另起一个只阻塞读 stdin 的 PHP 子进程，定时看它是否退出，插件退出时终止它。PHP 连不了命名管道：.NET 宿主在 Windows 上给出命名管道加回环 TCP 两个回调端点，插件取回环 TCP，回调照常进行。Windows 没有 pcntl，SIGTERM 不处理，宿主以关闭 stdin 作为终止信号。

## 一致性

按 RFC-0024：

- 认领的档：`core` 与 `plugin`（规范仓库的 [conformance/profiles.json](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)）。
- 缺口：无；认领的档的向量全部回放，`plugin` 运行器子命令通过。
- 规范版本：仓库的 `spec/` 子模块钉的提交；发预发布或正式版时它必须正好在规范标签上。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 范围：原语、清单、调用上下文、管道、操作与进程外插件运行时 `plugin`（`xihan.plugin.v1`）可用；`caching.distributed@1` 等远程 Provider 的操作集还没有提供。

怎样验证：

- 生命周期迁移表、管道阶段顺序、错误码、Baggage 键与值、能力依赖文本、JSON 文本的检查、线上编码、消息模板，以及插件的握手行、协议版本协商、回调端点列表、截止时间、回调失败的映射与运行时的消息键由 `spec/conformance/vectors` 验证，插件协议的行为由规范仓库的黑盒运行器验证。
- 向量全集测试 [tests/VectorCoverageTest.php](https://github.com/XiHanFun/framework-sdk-php/blob/main/tests/VectorCoverageTest.php) 按档表核对：认领的档要求的每个向量要么被测试回放，要么列为缺口并写明理由。
- 插件运行时的一致性测试 [tests/Plugin/ConformanceTest.php](https://github.com/XiHanFun/framework-sdk-php/blob/main/tests/Plugin/ConformanceTest.php) 用规范的黑盒运行器对示例插件 `examples/echo` 跑全部用例，要求全部通过、没有跳过。
- CI 在 ubuntu 与 windows 上以 PHP 8.3、8.4 跑格式检查、静态检查与全部测试，并核对插件协议的生成代码与 `plugin.proto` 一致。

## 破坏性改动与迁移

目前只有预发布版，正式版之前公开 API 仍可能变化。首个发布版之前、开发期间已有的破坏性改动：

- **流式操作的 API 撤掉**（ADR-0237）：`XiHan\Framework\Plugin\StreamingOperation` 与 `OperationStream` 已删除，`InvokeStream` 只读第一帧，是 `start` 时以 `Unimplemented` 结束（见[进程外插件](#进程外插件)的「流式」）。迁移：插件只提供一元操作，清单不声明流式操作，等 RFC-0011 定义流式契约。
- **`Error` 的文案不能只有空白**（RFC-0001「空白字符」，ADR-0245）：读线上编码时 `message` 为空或只有空白都拒绝（上一版只拒绝空串），`Error::literal()` 与 `withText()` 给出只有空白的文案抛 `InvalidArgumentException`；读宿主写来的 proto `Error` 时，只有空白的 `text` 与空串一样取 `code`。迁移：自由文本错误给出含非空白字符的文案。
