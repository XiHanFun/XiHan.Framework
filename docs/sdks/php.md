# PHP SDK

[framework-sdk-php](https://github.com/XiHanFun/framework-sdk-php) 是 [XiHan Spec](/appendix/spec) 的 PHP 实现，Composer 包名 `xihanfun/xihan-framework`：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。用它可以在 PHP 应用里按同一套契约执行操作与管道，也可以写由 .NET 宿主启动的进程外插件。规范以 submodule 放在 SDK 仓库的 `spec/`，一致性由规范仓库的向量与黑盒运行器验证；语义、错误码、消息键与线上编码与 .NET 参考实现（[XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)）一致，API 按 PHP 习惯。宿主一侧怎样加载插件见[进程外插件](/plugins/out-of-process)。

## 安装

```bash
composer require xihanfun/xihan-framework:<版本>
```

要求 PHP 8.3+，运行时只依赖 PHP 核心与 `ext-json`。插件运行时另需可选依赖（见[写一个插件](#写一个插件)）。目前只有预发布版，Composer 默认只装稳定版，要写明版本号：`<版本>` 换成 [Releases](https://github.com/XiHanFun/framework-sdk-php/releases) 里的版本（不带 `v`）。

## 模块

命名空间都在 `XiHan\Framework\` 之下。

| 命名空间 | 内容 | 规范 |
| --- | --- | --- |
| `Primitives` | `Error` / `ErrorCode`、`Result<T>`、`Maybe<T>`、`Unit`、`TypedId`、`LifecycleState`、`PipelineStage`、`Stability`、`BaggageKeys`、`OperationId`、`JsonPath`、消息键与模板（`MessageTemplate`、`MessageScope`、`MessageCatalog`、`LocaleName`）、内核的错误码与消息（`KernelErrors`、`KernelMessages`）、带错误码的异常（`FrameworkException`） | RFC-0001、RFC-0010、`conventions/` |
| `Manifest` | `xihan.manifest.json` 的模型、读取、按规范排版写出与跨字段校验，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `Invocation` | `Context`：取消令牌与截止时间（`Cancellation`、`CancellationSource`、`Clock`）、W3C 追踪、Baggage、对象袋（`Items`） | RFC-0003 |
| `Pipeline` | 十一阶段的 `PipelineBuilder`：`use` / `useAt` / `useNamedInstance`、`before` / `after`、`describe` / `dump`、`build` | RFC-0004 |
| `Operation` | `Contract`、`Schema`、`Shape`（请求的形状）、`Codec`、`Registry`（容量 10000）、经完整管道执行的 `Invoker`（XGI：JSON 请求与响应，另有类型化入口）、`Current` | RFC-0011 |
| `Plugin` | 进程外插件运行时 `Runtime::serve()`（协议 `xihan.plugin.v1`）、`Options`、`Hooks`、`Subscription` / `Envelope`、`Health`、`Configuration`、`Catalog`、宿主回调客户端 `Host`、写 stderr 的 JSON 日志 `Logger`；协议消息 `Protocol\V1` 由 `spec/plugin/plugin.proto` 生成 | RFC-0017、`plugin/` |

## 快速开始

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

1. 操作用 `Registry::add` 登记：契约、处理函数、`Codec` 与可选的 `Schema`。PHP 没有不经反射的类型化 JSON 解码，所以 `Codec::typed` 显式给出请求类型名、请求的形状（`Shape`）、创建函数与响应类型名；不需要类型时用缺省的 `Codec::json()`，请求是按值模型读出的 `JsonObject`。`Invoker::invokeTyped($context, $id, $request, $responseType)` 不经 JSON 直接以请求对象执行。
2. 可恢复失败返回 `Result::fail(...)`，不抛异常。错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键；`Error::message()` 经 `MessageScope::setDefault(new MessageCatalog([...]), 'zh-CN')` 设置的目录按区域解析，再找 SDK 自带的内核消息英文模板（`KernelMessages`），都没有时是「键 (参数)」。内核的错误码与工厂集中在 `KernelErrors`。
3. 编程错误与配置错误抛带错误码的异常（`FrameworkException` 接口）：参数非法是 `InvalidArgumentException`，清单、管道约束与操作重复登记这类装配错误是 `ConfigurationException`，强行从失败的 `Result` 取值是 `FailureException`。
4. 中间件与处理函数的签名是同步的。取消只经令牌通知，调用器不中止处理函数：处理函数看 `$context->isCancelled()`，`$context->cancellationError()` 给出 `KERNEL_TIMEOUT_001` 或 `KERNEL_CANCELLED_001`。`withCancellation($token)` 派生一个还能被另一个令牌取消的上下文，`withDeadline($deadline)` 派生截止时间更早的上下文（只能收紧）。
5. 中间件用 `Current::descriptor($context)`、`request`、`requestObject`（Validate 阶段解码好的请求）与 `responseObject`（编码之前的响应）看这次调用。
6. 清单与线上编码：

   ```php
   use XiHan\Framework\Manifest\Manifest;
   use XiHan\Framework\Primitives\{Error, Result};

   $manifest = Manifest::readFile(__DIR__ . '/xihan.manifest.json'); // 不合法抛 ConfigurationException（KERNEL_CAPABILITY_006）
   $read = Manifest::tryReadFile(__DIR__ . '/xihan.manifest.json');  // 不抛异常：Result<Manifest>，失败的原因在 $read->error()
   echo $manifest->toJson();                                         // 按 RFC-0002「写出」排版，各语言逐字相同

   echo Result::fail(Error::literal('ORDERS_CONFLICT_001', 'stale'))->toJson();
   // {"ok":false,"error":{"code":"ORDERS_CONFLICT_001","message":"stale"}}
   $result = Result::fromJson('{"ok":true,"value":{"id":"o1"}}');   // 有值结果；对象是 JsonObject，$result->valueOrThrow()['id']
   $done = Result::unitFromJson('{"ok":true}');                       // 无值结果
   $found = Result::maybeFromJson('{"ok":true,"value":null}');        // 值类型为 Maybe 的结果，null 即无值
   ```

## 写一个插件

这一节是插件一侧用 PHP 的写法；宿主怎样发现、启动与降级插件，见[进程外插件](/plugins/out-of-process)。插件运行时的依赖是可选的，用到时再装：

```bash
composer require xihanfun/xihan-framework:<版本> amphp/socket amphp/http google/protobuf
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

清单 `xihan.manifest.json` 放在插件目录里：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.greeting-php",
  "version": "0.1.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "contributes": { "operations": [ "greetings.create" ] },
  "activation": [ "onStartup" ],
  "entry": "greeting"
}
```

1. 清单与启动前的核对：`kind` 为 `plugin`、`isolation` 为 `out-of-process`，`primitives` 写本 SDK 支持的原语层大版本 `1`，不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；插件服务的每个操作都列在 `contributes.operations`，健康项列在 `contributes.health`。声明与清单不符、缺 `XIHAN_PLUGIN_SESSION` 等环境变量或监听失败时 `serve()` 拒绝启动：不监听、不写握手行，在 stderr 记一条 `error` 日志，返回 `Runtime::EXIT_REFUSED`（1）。
2. 启动脚本：`entry` 是相对插件目录的可执行文件。宿主只继承少数环境变量，脚本要写 PHP 与入口脚本的绝对路径：Unix 上是带 shebang、可执行的 sh 脚本（`exec /usr/local/bin/php /srv/greetings/plugin.php "$@"`），Windows 上是 `.cmd`（`@"C:\php\php.exe" "C:\srv\greetings\plugin.php" %*`）。
3. 订阅、回调与目录都在 `Options` 里给出：事件订阅（`Subscription`，至少一次投递，处理器收到 `Context`、`Envelope` 与投递次数，必须幂等）、生命周期回调（`Hooks`：`configure`、`start`、`stop`、`health`）与插件自己的消息目录（`Catalog::fromDirectory()`，要有 `en-US`）。回调抛出时，交给宿主的 `reason` 只是异常的简单类名（匿名类写成 `父类@anonymous`），文本只写进 stderr；返回的不是 `Result` 按抛出 `TypeError` 处理。
4. 配置：配置回调收到的 `Configuration` 原样带着配置节，另有两种类型化读取：按键路径逐个读（`string()`、`int()`、`float()`、`bool()`、`value()`，键按 `:` 分段，找不到为 `null`，读不成抛 `InvalidArgumentException`），或按形状整节读（`decode(Shape)`，返回与 `Codec::typed()` 交给创建函数相同的数组）。两种读法都按 ASCII 不区分大小写匹配成员名，字符串叶子按 JSON 数字的文法读成数字，布尔只认 `true` / `false`。绑定：`Configuration::$bindings` 与 `binding($name)` 给出 `ResolvedBinding`：`$name`、`$type`、主值 `$value` 与属性 `$properties`（属性名 → 属性值，区分大小写、原样；没有属性时是空数组；只挂属性的绑定 `$value` 为空串）。PHP 的数组把纯数字的属性名（`0`、`8080`）存成整数键：按名字读用 `property($name)`（没有为 `null`）与 `hasProperty($name)`，要属性名用 `propertyNames()`，一律按字符串。主值与属性值可能是凭据：`var_dump()` 与 `print_r()` 只带绑定名、类型与属性名；`ResolvedBinding` 实现 `JsonSerializable`，`json_encode()` 与 SDK 的 `Logger` 写绑定（含数组里、对象属性里的绑定与含绑定的 `Configuration`）只有 `{"name":…,"type":…,"propertyNames":[…]}`，属性名一律是字符串、按字节升序，读不回原来的绑定。`var_export()`、`serialize()`、`(array)` 与 `get_object_vars()` 不经钩子，仍带全部值，不要把它们的结果写进日志。清单里的绑定缺省必需（`required` 缺省 `true`），部署里没有它时宿主不配置插件；可以没有的写 `"required": false`，这时 `binding($name)` 可能找不到它。
5. 回调宿主：处理函数里用 `Host::of($context)` 取宿主回调客户端，`invoke` 经宿主的完整管道，`publish` 是批量、逐项结果。`Envelope::$time` 是 `?DateTimeImmutable`，构造参数收 `?DateTimeInterface`（复制成 `DateTimeImmutable`）：发布时换算成 UTC 写成 RFC 3339，以 `Z` 结尾，秒的小数去掉末尾的 0，最多六位；不在本地核对，0001–9999 年之外的照写，宿主以 `messaging.envelope.invalid_time` 拒绝那一项。收到的信封里，宿主写来的文本按 RFC-0003 规则 8 读成 UTC 的 `DateTimeImmutable`，比微秒更细的小数截断；读不出时投递以 gRPC `INVALID_ARGUMENT` 拒绝。`Envelope::$dataSchema`（构造参数在最后，用 `dataSchema:` 传，空串是没有）是 CloudEvents 的 `dataschema`，收到与发布都原样。清单 `permissions` 要声明 `capability:kernel.pipeline`（`Host::invoke`）或 `capability:messaging.queue`（`Host::publish`）并由宿主授予；权限、租户与截止时间在发出之前先在本地核对，不合不发请求。回调端点取 `XIHAN_PLUGIN_HOST_ADDRESS` 里第一个能连的项：Unix 域套接字（Windows 之外）与回环 TCP，PHP 连不了命名管道；宿主总在最后给出回环 TCP，所以 Windows 上回调经回环 TCP 进行。一个都没有时插件照常服务，回调以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable` 失败，`Host::endpoint()` 为 `null`。
6. 监听端点：Linux 与 macOS 上是 Unix 域套接字，放在新建的私有目录里（`0700`，套接字 `0600`，停止后删除；临时目录的路径含空白、不是合法的 UTF-8 或太长时改用 `/tmp`）；Windows 上是回环 TCP（`127.0.0.1` 的随机端口），隔离只靠会话令牌。`Options` 的 `network` 与 `address` 可以改；PHP 不能以非阻塞方式服务命名管道，不支持 `npipe`。
7. 处理函数会阻塞事件循环：运行时每个调用一个 Fiber，处理函数仍是同步签名，阻塞的处理函数会挡住同一进程里的其他调用，耗时的 I/O 用 amphp 的非阻塞接口。
8. 日志与退出：stdout 只有握手行，日志写 stderr 的 JSON 行，`Logger` 也可以给插件自己用。`Stop`、stdin 关闭与 SIGTERM 按规范的时限收尾后以退出码 0 退出。SIGTERM 要 pcntl 扩展（只在 Unix 上有），没有时按系统缺省终止进程；Windows 上宿主以关闭 stdin 作为终止信号。

用规范仓库的黑盒运行器验证（需要 Go，参数见规范仓库的 [`conformance/README.md`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/README.md)）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --sample 'greetings.create={"name":"hi"}'
```

完整的示例在 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-php/tree/main/examples/echo)：一元操作、故意崩溃的操作、两种订阅方式、健康项、配置节与宿主回调；[`tests/Plugin/ConformanceTest.php`](https://github.com/XiHanFun/framework-sdk-php/blob/main/tests/Plugin/ConformanceTest.php) 把它放进临时目录、生成启动脚本，并用黑盒运行器跑全部用例。

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

PHP 的写法与差异：

- **`Result` 与 `Error` 的读写**：读取按结果的种类选入口（`Result::fromJson`、`unitFromJson`、`maybeFromJson`），它们先过 JSON 文本的共同检查；`fromWire` 收已解码的值，看不出重复成员。写出用 `toJson()`，数字按值模型；自己 `json_encode` 时要带 `JSON_PRESERVE_ZERO_FRACTION`，并把 `serialize_precision` 设为 `-1`。`Error::literal()` 与 `withText()` 的文案为空或只有空白时抛 `InvalidArgumentException`。
- **空白**：按 RFC-0001 写死的 25 个码位、以 UTF-8 码位判断，不用只认 ASCII 的 `ctype_space` 与 `trim`。
- **调用器**：传入的 `PipelineBuilder` 不变，三个内置步骤登记在它的副本上，再登记同名的中间件是重复登记。下游抛出 amphp 的 `Amp\CancelledException`（SDK 不依赖 amphp，按类名认出它）按取消归因，不当作未处理的异常。响应写不出（浮点是 NaN 或无穷、非法 UTF-8、编码函数抛出）时调用以未处理失败结束，不抛给调用方。中间件以成功短路时，可以经 `Current::setResponsePayload` 交出记录的响应。
- **上下文**：`Context::create(baggage: …)` 是外部输入，丢弃不合的成员；`withBaggage()` 给出不合的值抛 `InvalidArgumentException`。`sampled()` 在没有上游的采样决定时是 `null`，`isSampled()` 给出布尔。没有事件循环时，截止时间在轮询令牌时核对时钟。
- **清单**：`Manifest::tryRead` / `tryReadFile` 以 `Result` 返回读取失败（`KERNEL_CAPABILITY_006`），`read` / `readFile` 是它们的包装，失败时抛 `ConfigurationException`，`toError()` 是逐字的错误。`tryRead` 不跳过 BOM，`tryReadFile` 跳过。跨字段约束在 `Manifest::validate()`（`PRIMITIVES_VALIDATION_001`），构造清单时不核对。
- **模板**：`MessageTemplate::format()` / `tryFormat()` 与 `MessageCatalog` 按区域取小数点与分组符（`MessageTemplate::numberSymbols()`），`describe` 按不变文化。
- **插件运行时**：PHP 的时刻精确到微秒，`deadline` 更细的小数截断。运行时消息键的 10 种区域模板在 `PluginMessages::catalog()`，只供本地渲染，不随握手发送。

## 语言映射

规范把取决于语言的地方交给各 SDK 写明，PHP 的做法：

- **JSON 文本的共同检查**：语法、嵌套层数、孤立的代理项转义与原始控制字符交给 `json_decode`，非法 UTF-8 由 PCRE 的 UTF-8 校验拒绝，重复成员在原文上找（`json_decode` 遇到重复取最后一个）。过了检查之后按内部的读取器读成保留文本顺序与原文写法的树，不经 `stdClass`。
- **文法的匹配**：PCRE 的 `$` 在结尾的换行之前也算匹配，所以规范文法的正则都带 `D` 修饰符。操作 ID 另有长度上限 128 个字符（`OperationId::MAX_LENGTH`）。
- **值模型**（RFC-0010「模板」）：`null`、`bool`、`string`、`int`（PHP 的 `int` 就是 64 位）、`float` 原样。SDK 读出的 JSON 对象是 `JsonObject`：不可变，成员按文本顺序，`{}` 与 `[]` 不混淆，用 `$object['name']`、`get()` 取成员，`with()` 得到改了一个成员的新对象。PHP 自己的数组是列表（`array_is_list`，含空数组 `[]`）时是数组，否则是对象；`stdClass` 也是对象。模型之外的值写成 PHP 的文本：`BackedEnum` 取值的文本，`UnitEnum` 取名字，`Stringable` 取 `(string)`，`DateTimeInterface` 取 ATOM 格式，`JsonSerializable` 取 `jsonSerialize()` 再归入，其余对象写类型名。它们不跨语言，插件要跨语言格式化时先转成字符串。
- **请求的形状**（RFC-0011 规则 2）：成员用 `Shape` 声明，成员名区分大小写，未知成员忽略。
  - 必填：`Shape::string()->required()`；只要求出现，值为 `null` 也算出现。
  - 可选：不加 `required()`，缺少时交给创建函数的数组里没有这个键。
  - 接受 `null`：`string()`、`listOf()`、`mapOf()`、`object()` 与 `any()` 接受 `null`（等于没有值），`int32()`、`int64()`、`float()`、`bool()` 与它们的数组项、字典值不接受，加 `nullable()` 才接受。
  - 交给创建函数的值：对象与字典是以成员名为键的数组（PHP 会把纯数字的键转成整数）。
  - 任意 JSON：`any()` 交出 `RawJson`，`json` 是读到的原文，`decode()` 取 `json_decode` 的对象模式结果；写出时把原文原样放回，`1e400`、超出双精度的整数与 `2.0` 写回时 JSON 值不变。`RawJson::of()` 包装自己的 JSON 文本。
  - `Codec::json()` 不核对形状，请求是按值模型读出的 `JsonObject`：`1e400` 这类上溢的数字读成双精度无穷，作为响应原样返回时写不出。要原样保留原文时用 `typed()` 与 `any()`。

## 设计取舍

- **同步签名与取消**：中间件与处理函数的签名是同步的，可以运行在 Fiber 里。取消令牌可轮询、可登记回调；没有事件循环时截止时间在轮询时核对时钟，接入事件循环时宿主在截止时刻调用 `DeadlineCancellation::poll()`。
- **泛型**：以 PHPStan 的 `@template` 标注。
- **插件运行时**：运行在 amphp 的事件循环（Revolt）上。PHP 的 gRPC 扩展只有客户端，所以 gRPC 的服务端与客户端（明文 HTTP/2，按先验知识）由本 SDK 实现：帧的解析与 HPACK 用 amphp/http，连接、流、流量控制与 gRPC 的消息、状态与尾部在 `Plugin\Internal\Grpc`。插件协议的连接都在本机、长期保持，不设空闲超时这类面向公网的限制；内存由流的接收窗口、每条消息 4 MiB 的上限与 100 个并发流限定。协议消息用 google/protobuf 的纯 PHP 运行时，不需要额外的 PHP 扩展。
- **Windows**：PHP 读非阻塞的管道时，管道开着却没有数据会一直等，所以 Windows 上另起一个只阻塞读 stdin 的 PHP 子进程，定时看它是否退出，插件退出时终止它。Windows 没有 pcntl，SIGTERM 不处理。

## 一致性

- 现状：原语、清单、调用上下文、管道、操作与进程外插件运行时可用，示例插件 `examples/echo` 通过规范黑盒运行器的全部用例。
- 认领的档（RFC-0024）：`core` 与 `plugin`；档的定义见规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。
- 缺口：无。
- 钉的规范版本：SDK 仓库 `spec/` 子模块指向的提交；发版时子模块必须正好在规范标签上。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 尚未提供：流式操作（操作契约还没有流式形态，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束）、Wasm 载体与远程 Provider 的插件侧（如 `caching.distributed@1`）；管道运行期的成组登记与按归属撤回是宿主能力，不提供。
- 怎样验证：测试直接回放 `spec/conformance/vectors`；[`tests/VectorCoverageTest.php`](https://github.com/XiHanFun/framework-sdk-php/blob/main/tests/VectorCoverageTest.php) 按档表核对认领的档要求的每个向量要么被测试回放、要么列为缺口；`tests/Plugin/ConformanceTest.php` 用黑盒运行器对示例插件跑全部用例，要求全部通过、没有跳过。CI 在 ubuntu 与 windows 上都跑，并核对插件协议的生成代码与 `plugin.proto` 一致。
- 信封的 `time`：回放 `plugin/sdk-envelope-times.json` 的微秒一列（ADR-0310）。写出的文本与 `text.microseconds` 逐字相同，读出的时刻与 `utc.microseconds` 相等（比微秒更细的小数截断），`unreadable` 的每一条投递都以 `InvalidArgument` 拒绝。`plugin/envelope-times.json` 现在同在 `plugin` 档。

## 破坏性改动与迁移

首个对外版本是 `v0.2.0-rc.1`。之后的变化：

- 内核错误码集中到 `Primitives\KernelErrors`，`Context`、`PipelineBuilder`、`Manifest` 与 `OperationErrors` 上的错误码常量（如 `Context::ERROR_CODE_CANCELLED`、`OperationErrors::NOT_FOUND`）弃用，旧名仍可用 → 改用 `KernelErrors::*`。
- `Context` 与 `OperationErrors` 上的消息键常量（`MESSAGE_*`）弃用，旧名仍可用 → 改用 `KernelMessages::*`。
- 弃用 `OperationErrors::deadlineBeforeStart()` 与 `unhandledException()`（旧名仍可用）→ 改用 `KernelErrors` 上的同名工厂。
- `Configuration::string()` 读浮点叶子的文本不再随进程的 `serialize_precision` 变化（原先不是 `-1` 时 `0.1` 写成 `0.10000000000000001`）→ 不用改。
- `OperationId::isValid`、`Operation\Contract` 与读清单不再接受超过 128 个字符的操作 ID（抛 `InvalidArgumentException`，读清单为 `kernel.manifest.invalid`），原先不设上限；新增常量 `OperationId::MAX_LENGTH` → 把 ID 缩到 128 个字符以内。
- 清单 `activation` 里 `onEvent:` 的模式可以带下划线（如 `onEvent:order_events.*`），原先读不出 → 不用改。
- `var_dump()` / `print_r()` 打印 `ResolvedBinding` 不再带 `value`，只带绑定名、类型与属性名（ADR-0285 新增的属性同样只列名字）→ 确实要看主值的地方直接读 `$binding->value`。
- `json_encode()` 写 `ResolvedBinding`（含 SDK 的 `Logger` 记下的）只有 `name`、`type` 与按升序排列的 `propertyNames`，不再带 `value` 与 `properties` → 要序列化值的自己取 `$binding->value` 与 `$binding->properties`。
- `Envelope::$time` 改为 `?\DateTimeImmutable`，构造参数改为 `?\DateTimeInterface`（缺省 `null`，ADR-0306）→ 传 `new \DateTimeImmutable(...)`；原先传 `''` 的改为不传或传 `null`；要文本时自己 `format()`。
- 宿主写来的 `time` 读不出时投递以 `INVALID_ARGUMENT` 拒绝 → 不用改。
- 新增 `Envelope::$dataSchema`（ADR-0305）→ 不用改。
