# Java SDK

[framework-sdk-java](https://github.com/XiHanFun/framework-sdk-java) 是 [XiHan Spec](/appendix/spec) 的 Java 实现：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。用它可以把与 .NET 宿主（参考实现 [XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)）相同的原语用在 JVM 应用里，也能写在 .NET 宿主里运行的 Java 插件。规范以 submodule 放在 SDK 仓库的 `spec/`，一致性由规范仓库的向量与黑盒运行器验证，规范写到的行为与 .NET 一致。宿主一侧怎样加载插件见[进程外插件](/plugins/out-of-process)。

## 安装

需要 JDK 21+。Maven 坐标 `com.xihanfun:xihan-framework`，发布在 Maven Central；目前只有预发布版，`${xihan-framework.version}` 换成 [Releases](https://github.com/XiHanFun/framework-sdk-java/releases) 里的版本（不带 `v`）：

```xml
<dependency>
  <groupId>com.xihanfun</groupId>
  <artifactId>xihan-framework</artifactId>
  <version>${xihan-framework.version}</version>
</dependency>
```

### 插件运行时的依赖

插件运行时另用 grpc-java 与 protobuf-java。它们在本构件里是可选依赖，不传递给使用者，其余包不引用它们；写插件的应用自己加上，版本与 SDK 仓库 [`pom.xml`](https://github.com/XiHanFun/framework-sdk-java/blob/main/pom.xml) 的 `grpc.version`、`protobuf.version` 相同：

```xml
<dependency>
  <groupId>io.grpc</groupId>
  <artifactId>grpc-netty-shaded</artifactId>
  <version>${grpc.version}</version>
</dependency>
<dependency>
  <groupId>io.grpc</groupId>
  <artifactId>grpc-protobuf</artifactId>
  <version>${grpc.version}</version>
</dependency>
<dependency>
  <groupId>io.grpc</groupId>
  <artifactId>grpc-stub</artifactId>
  <version>${grpc.version}</version>
</dependency>
<dependency>
  <groupId>com.google.protobuf</groupId>
  <artifactId>protobuf-java</artifactId>
  <version>${protobuf.version}</version>
</dependency>
```

在模块路径上用插件运行时，这些自动模块（`io.grpc`、`io.grpc.stub`、`io.grpc.protobuf`、`io.grpc.netty.shaded`、`com.google.protobuf`、`com.google.common`）要进入模块图：应用自己 `requires` 它们，或用 `--add-modules`。

## 模块

整个 SDK 是一个 Maven 构件，JPMS 模块名 `com.xihanfun.framework`，按 Java 包划分（下表省略前缀 `com.xihanfun.framework.`）。外部依赖只有 `jackson-databind`。

| Java 包 | 内容 | 规范 |
| --- | --- | --- |
| `primitives` | `Error` / `ErrorCode`、`Result<T>`、`Maybe<T>`、`TypedId<Tag, V>`、`LifecycleState`、`PipelineStage`、`Stability`、`BaggageKeys`、消息键与模板（`MessageTemplate`、`MessageCatalog`、`MessageScope`、`KernelMessages`）、`FrameworkException` | RFC-0001、RFC-0010、`conventions/` |
| `primitives.json` | 线上 JSON 编码：Jackson 模块 `PrimitivesModule` 与读取入口 `PrimitivesJson` | RFC-0001 |
| `manifest` | `xihan.manifest.json` 的模型、按 Schema 严格读取、规范化输出与校验，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `invocation` | `Context`：取消与截止时间（可登记回调）、W3C 追踪、已登记的 Baggage、对象袋 `Items`、按派生传递的值 | RFC-0003 |
| `pipeline` | 十一阶段的 `PipelineBuilder`：`use` / `useAt` / `useNamedInstance`、`before` / `after`、`describe` / `dump`、`build` | RFC-0004 |
| `operation` | `OperationContract`、`OperationSchema`、`OperationHandler<Req, Res>`、容量 10000 的 `OperationRegistry`、经完整管道执行的 `OperationInvoker`（XGI：JSON 请求与响应，另有类型化入口） | RFC-0011 |
| `plugin` | 进程外插件运行时 `PluginRuntime.serve(PluginOptions)`（协议 `xihan.plugin.v1`）、生命周期回调 `PluginHooks`、订阅 `Subscription` 与 `EventHandler`、消息目录 `PluginCatalog`、宿主回调 `Host`、stderr 的 JSON 行日志 `PluginLogger` | RFC-0017、`plugin/` |

## 快速开始

```java
record CreateOrder(int quantity) { }
record OrderCreated(String id, int quantity) { }

OperationRegistry registry = new OperationRegistry();
registry.add(OperationContract.of("orders.create").withStability(Stability.EXPERIMENTAL).withExpose(Expose.HTTP),
        CreateOrder.class, OrderCreated.class,
        (context, request) -> request.quantity() > 0
                ? Result.ok(new OrderCreated("o-" + context.correlation().orElse(""), request.quantity()))
                : Result.fail(Error.validation("ORDERS_VALIDATION_001", "orders.quantity.invalid", Map.of("quantity", request.quantity()))));

PipelineBuilder pipeline = new PipelineBuilder()
        .useAt(Middleware.of("Observe", (context, next) -> next.invoke(context)), PipelineStage.OBSERVE, Placement.END, PipelineBuilder.APP_OWNER);
OperationInvoker invoker = new OperationInvoker(registry, pipeline);

Context context = Context.builder().timeout(Duration.ofSeconds(2)).baggage(BaggageKeys.CORRELATION, "c1").build();
OperationResponse response = invoker.invoke(context, OperationRequest.of("orders.create", "{\"quantity\":2}"));
// response.payloadText() == Optional[{"id":"o-c1","quantity":2}]
```

1. 可恢复失败返回 `Result` / `Error`，不抛异常。错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键 + 参数；`Error.message()` 先按 `MessageScope` 的进程默认目录解析，再用 SDK 自带的内核消息英文模板（`KernelMessages`），都翻译不出时是 `键 (参数)`。
2. 配置错误（清单不合法、管道成环、操作重复登记、登记表已满）抛带错误码的 `FrameworkException`；参数格式不合法这类编程错误抛 `IllegalArgumentException`，状态不允许抛 `IllegalStateException`。
3. 中间件、处理器与调用器都是同步方法，按虚拟线程设计，可以阻塞。取消与截止时间只经令牌通知，调用器不中断线程：处理器自己看 `context.isCancelled()`、`context.cancellationError()`（`KERNEL_TIMEOUT_001` 或 `KERNEL_CANCELLED_001`）、`onCancelled` 或 `awaitCancellation`。`withCancellation` / `withDeadline` / `withTimeout` 返回的 `CancellableContext` 用完要关闭（try-with-resources）。
4. 中间件用 `Operations.find(context)`、`findRequest`、`findDecodedRequest`（Validate 阶段解码好的请求）与 `findResponse`（编码之前的响应对象）看这次调用。自己报内核错误时用工厂 `OperationErrors.notFound(id)`、`OperationErrors.invalidPayload(id, reason)`。
5. 线上编码：`new ObjectMapper().registerModule(new PrimitivesModule())`；从线上收到的文本用 `PrimitivesJson.read(mapper, bytes, type)` 读，它先过共同的 JSON 文本检查。

## 写一个插件

这一节是插件一侧用 Java 的写法；宿主怎样发现、启动与降级插件，见[进程外插件](/plugins/out-of-process)。插件是宿主启动的子进程：`main` 里登记操作与订阅，交给 `PluginRuntime.serve`，它读工作目录里的清单与宿主下发的环境变量，监听本机端点，向 stdout 写握手行，然后服务到 `Stop`、stdin 关闭或 SIGTERM。

```java
public final class GreetingPlugin {
    record Greet(String name) { }
    record Greeting(String text) { }

    public static void main(String[] args) throws Exception {
        OperationRegistry registry = new OperationRegistry();
        registry.add(OperationContract.of("greetings.create").withStability(Stability.EXPERIMENTAL).withExpose(Expose.HTTP),
                Greet.class, Greeting.class,
                (context, request) -> Result.ok(new Greeting("hello, " + request.name())));

        PluginRuntime.serve(PluginOptions.builder()
                .operations(registry)
                .subscribe(Subscription.of("greetings.requested"), (context, envelope, attempt) -> Result.success())
                .catalog(PluginCatalog.load("greetings", locale -> GreetingPlugin.class.getResourceAsStream("Localization/" + locale + ".json")))
                .build());
    }
}
```

清单 `xihan.manifest.json` 放在插件目录里：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.greeting",
  "version": "0.1.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "contributes": { "operations": [ "greetings.create" ], "health": [ "greeting" ] },
  "permissions": [ "capability:kernel.pipeline" ],
  "activation": [ "onStartup" ],
  "entry": "greeting"
}
```

1. 清单与启动前的核对：`kind` 是 `plugin`、`isolation` 是 `out-of-process`，`primitives` 等于本 SDK 支持的原语层大版本 `1`，不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；登记的操作都在 `contributes.operations` 里，健康项在 `contributes.health` 里，订阅的（主题，类型）不重复。不合即拒绝启动：不监听、不写握手行，在 stderr 记一条 `error`，`serve` 抛出带错误码的 `FrameworkException`。
2. 启动脚本：`entry` 是相对插件目录的可执行文件，Java 插件指向启动脚本：Unix 上是带 shebang、可执行的 sh 脚本，Windows 上是 `.cmd`。宿主只继承少数环境变量，`JAVA_HOME` 不在其中，脚本里写 `java` 的绝对路径与完整的类路径，例如 `exec /opt/java/bin/java -cp '/opt/greeting/lib/*' GreetingPlugin "$@"`。JDK 23 起 Netty 的 `sun.misc.Unsafe` 访问会在 stderr 打告警，脚本里加 `--sun-misc-unsafe-memory-access=allow`。
3. 订阅：至少一次投递，处理器必须幂等；`Subscription` 可以带类型与订阅方式（`SubscriptionMode`），事件处理器抛出的异常翻译为 `KERNEL_UNEXPECTED_001`。
4. 钩子与配置：`PluginHooks` 的 `configure`、`start`、`stop`、`health` 都有缺省实现，返回的失败原样交给宿主；抛出（或返回 `null`）时报成 `plugins.process.failed`，`reason` 只是异常的简单类型名（`IllegalStateException`，匿名类如 `EchoPlugin$1`），异常只写进 stderr。`PluginConfiguration.section()` 是配置节原文，`decode(type)` 按 Jackson 读：成员名按 ASCII 不区分大小写，字符串叶子按 JSON 数字的文法读成数字，布尔只认 `true` / `false`，未知成员忽略。绑定：`PluginConfiguration.bindings()` 与 `findBinding(name)` 给出 `ResolvedBinding`：`name()`、`type()`、主值 `value()` 与属性 `properties()`（只读的 `Map<String, String>`，属性名区分大小写、原样；没有属性时是空表；只挂属性的绑定 `value()` 为空串）。主值与属性值可能是凭据：`toString()` 只带绑定名、类型与属性名；`ResolvedBinding` 实现 Jackson 的 `JsonSerializable`，单个绑定、绑定的列表与映射、含绑定的记录写成 JSON 都只有 `{"name":…,"type":…,"propertyNames":[…]}`（属性名按升序），不带主值与属性值，关掉注解的映射器同样。遮过的 JSON 读不回 `ResolvedBinding`，要序列化值的自己取 `value()` 与 `properties()`。清单里的绑定缺省必需（`required` 缺省 `true`），部署里没有它时宿主不配置插件；可以没有的写 `"required": false`，这时 `findBinding(name)` 可能找不到它。
5. 回调宿主：处理器与钩子里用 `Host.of(context)` 取 `Host`，`invoke` 经宿主的完整管道，`publish` 是批量、返回与信封一一对应的 `List<Result<Unit>>`。`Envelope.time()` 返回 `Optional<Instant>`，`Envelope.Builder.time(Instant)` 设置：发布时换算成 UTC 写成 RFC 3339，以 `Z` 结尾，秒的小数去掉末尾的 0，最多九位；不在本地核对，0001–9999 年之外的时刻照写，宿主以 `messaging.envelope.invalid_time` 拒绝那一项。收到的信封里，宿主写来的文本按 RFC-0003 规则 8 读成 `Instant`（纳秒精度，不截断）；读不出时投递以 gRPC `INVALID_ARGUMENT` 拒绝。`Envelope.dataSchema()` 与 `Builder.dataSchema(String)` 是 CloudEvents 的 `dataschema`，收到与发布都原样，算进 `equals`、`hashCode` 与 `toString`。宿主写来的错误 `details` 严格编码成 UTF-8，孤立的代理项当作没有细节。清单 `permissions` 要声明 `Host.PERMISSION_INVOKE`（`capability:kernel.pipeline`）或 `Host.PERMISSION_PUBLISH`（`capability:messaging.queue`）并由宿主授予；权限、租户与截止时间在发出之前先在本地核对，不合不发请求。
6. 端点：Linux 上 grpc-netty-shaded 自带的 epoll 原生库可用时（x86_64、aarch_64）监听 Unix 域套接字，放进新建的私有目录（`0700`，套接字 `0600`）；Windows、macOS 与其余情形监听 `127.0.0.1` 的随机端口，隔离只靠会话令牌。grpc-java 不支持命名管道：本 SDK 不监听它，回调也只连回环 TCP 与 Linux 上的 Unix 域套接字；宿主总在回调端点列表的最后给出回环 TCP，Windows 上的 .NET 宿主因此也能回调。没有能连的回调端点时插件照常服务，回调以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable` 失败。
7. 日志与退出：stdout 只有握手行，日志用 `PluginLogger` 写 stderr 的 JSON 行，`java.util.logging` 的根日志缺省也改写成这种行（`bridgePlatformLogging(false)` 关掉）。`Stop`、stdin 关闭与 SIGTERM 按规范的时限收尾；收到 SIGTERM 时关闭钩子收尾后以 `Runtime.halt(0)` 结束进程（否则 JVM 以 143 退出），服务期间作者自己调用 `System.exit` 也经过这个钩子，同样以退出码 0 结束。
8. 模块路径上的应用要把请求与响应类型所在的包 `opens` 给 `com.fasterxml.jackson.databind`。

用规范仓库的黑盒运行器验证（需要 Go，参数见规范仓库的 [`conformance/README.md`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/README.md)）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --sample 'greetings.create={"name":"hi"}'
```

完整的示例在 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-java/tree/main/examples/echo)：回声、经宿主回调转发、故意崩溃（验证时排除）、列出收到的事件四个操作，一个事件订阅、一个健康项与配置节 `XiHan:Echo`。测试 `EchoConformanceTest` 把它放进临时目录、按当前平台生成启动脚本，再用黑盒运行器跑全部用例。

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

Java 的写法与差异：

- **JSON 文本**：共同检查 `JsonText.check` 是手写的逐字节检查器，不经 Jackson（它会跳过 BOM、按字节猜 UTF-16、放过孤立的代理项）。收字符串的入口（`JsonText.check(String)`、`ManifestJson.tryRead(String)`、`PrimitivesJson.read(mapper, String, …)`）先编成 UTF-8，字符串里有孤立的代理项按语法错误处理，不像 `String.getBytes` 那样换成 `?`。`OperationRequest.of(String id, String json)` 同样严格编码：文本里有孤立的代理项时请求照样建得出，`hasUnencodableText()` 为真，载荷是文本的广义 UTF-8（WTF-8，不是合法的 UTF-8），本地调用器读它报 `KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`（`reason` 为 `$`）；`Host.invoke` 在回调端点、权限、租户、截止时间与取消的核对之后以同一个错误在本地失败、不发请求；`OperationRequest.of(String, byte[])` 的字节原样发出，由宿主核对（`plugin/invocation.md` §5，ADR-0312）。
- **发布的数据**：`Envelope.Builder.data(String)` 严格编码成 UTF-8：文本里有孤立的代理项时信封照样建得出，`hasUnencodableData()` 为真，`data()` 是文本的广义 UTF-8（WTF-8，不是合法的 UTF-8）；`Host.publish` 在回调端点、权限、租户、截止时间与取消的核对之后逐项核对，这一项不发出，结果为 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid`（`reason` 为 `the data has a lone surrogate and cannot be encoded as UTF-8`），其余各项照常组成一次请求、结果放回原位，每一项都编不成时不发请求，结果数按发出的信封数核对；`data(byte[])` 的字节原样发出，由宿主核对（`plugin/invocation.md` §5，ADR-0315）。处理器没有以字符串交回应答的接口：类型化的响应由调用器编码并过 JSON 文本检查，§1 的严格编码一条对 Java 不用改。
- **写给宿主的错误细节**：插件交回或回调里写出的 `Error.details` 有孤立的代理项、嵌套超过 64 层时，这个错误不带细节、其余字段照写，内部错误的细节各自核对，不再经 protobuf 把孤立的代理项写成 `?`（`plugin/invocation.md` §3，ADR-0320）。
- **回调客户端关闭之后**：插件停止、运行时收尾之后的 `Host.invoke` 与 `Host.publish` 不发请求、不重新连接，结果是 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `Unavailable: the plugin runtime has stopped`）。它排在本地核对与载荷、`data` 的编码之后：载荷编不成的照旧是 `kernel.operation.invalid_payload`，`publish` 里编不成的那一项照旧是 `messaging.envelope.invalid`、其余各项是已关闭的错误（`plugin/invocation.md` §5，ADR-0328）。
- **Baggage 的值**：`BaggageKeys.isAcceptableValue` 还拒收孤立的代理项（成对的照收）：已登记键的这类值经 `Context.Builder.baggage` 进来时丢弃这个成员，`withBaggage` 抛 `IllegalArgumentException`；幂等键照旧不按这条丢弃（ADR-0321）。
- **线上编码**：无值成功是 `Result<Unit>`，`Result<Maybe<T>>` 的无值是 `"value":null`。解码器自己核对重复成员、64 层与孤立的代理项，不看 `ObjectMapper` 的配置。`PrimitivesModule` 写出 `Result` 与 `Error` 时先写进缓冲、整段文本过共同检查（RFC-0001「写出与转交」，ADR-0320）：字符串（成员名与值，含细节、内部错误与成功值）有孤立的代理项、从结果或错误的根算起嵌套超过 64 层，或任何一层有重复的成员名（自定义序列化器、`@JsonRawValue` 原样嵌进去的文本）时抛 `JsonMappingException`，外层的写出器一个字符也不写，不写成 U+FFFD 或 `\ud800` 这样的转义。`Error.literal` 与 `withText` 的文案、`FrameworkException` 的说明为空或只有空白时抛 `IllegalArgumentException`。
- **空白**：按 RFC-0001 写死的 25 个码位判断，不用 `Character.isWhitespace` 或 `String.isBlank`（它们不含 U+0085、U+00A0、U+2007、U+202F，却含 U+001C–U+001F）。
- **模板与区域**：`MessageTemplate.format(template, arguments, locale)` / `tryFormat(…, locale)` / `text(value, locale)` 与 `MessageCatalog.resolve(key, locale, arguments)` 按区域取小数点与分组符，不给区域按不变文化；`Error.message()` 按 `MessageScope` 的进程默认区域。
- **调用器**：内置步骤的归属是 `OperationInvoker.KERNEL_OWNER`，调用器复制使用者的 `PipelineBuilder` 再加内置步骤，不改动它。Observe 阶段翻译异常时 `java.lang.Error` 不翻译，`FrameworkException` 的错误进 `inner`。响应在管道结束之后编码，编码时关掉 Jackson 的 `WRITE_NAN_AS_STRINGS`，非有限的浮点写不出、调用以未处理失败结束。响应里的字符串（值、成员名与映射的键）有孤立的代理项同样写不出，错误不带 `inner`（ADR-0318）。Jackson 把值与键里的孤立代理项写成转义，由写出之后的 JSON 文本检查拦下；`@JsonProperty` 这类预先编码的成员名由 Jackson 先抛出，原因链里没有框架异常。写出的整段文本过 JSON 文本检查：嵌套超过 64 层（`@JsonRawValue` 原样嵌进去的文本也算，从响应的根算起）或任何一层有重复的成员名（自定义 `JsonSerializer` 两次写同名成员、`@JsonRawValue`）同样写不出、不带 `inner`（ADR-0319）；Jackson 写出时不查重复成员、嵌套上限远大于 64，靠的是这道检查。
- **载荷**：调用器在反序列化之前按请求类型以文本顺序核对载荷（`RequestShape`），报错的先后不随 Jackson 的内部路径变。
- **清单**：`ManifestJson.tryRead` / `tryReadFile` 以 `Result<Manifest>` 返回读取失败（`KERNEL_CAPABILITY_006`），`read` / `readFile` 是它们的包装，失败时抛 `FrameworkException`；文件存在却读不出来时两者都照常抛 `IOException`。`tryRead(byte[])` 不跳过 BOM，`tryReadFile` 跳过。`Manifest.Builder.validate()` 以结果返回跨字段约束的失败（`PRIMITIVES_VALIDATION_001`），`build()` 把它抛出。`Manifest` 按生效的值比较，写出的清单再读一遍与原来的相等。
- **文法与枚举**：规范的正则一律用 `Matcher.matches()` 整串匹配；操作 ID 另有长度上限 128 个字符（`OperationId.MAX_LENGTH`）。按名字编码的枚举（`LifecycleState`、`PipelineStage`、`Stability`、`ManifestKind`、`ManifestRole`、`PluginIsolation`、`Expose`、`Network`、`SubscriptionMode`）都是 `parse(name)` 返回 `Optional`、`wireName()` 给出线上的名字。
- **插件运行时**：`Instant` 精确到纳秒，`deadline` 的九位小数不截断。运行时的消息键目录 `PluginMessages` 带 10 种区域，只供本地渲染，不随握手发送；另收了本地报出的 `messaging.envelope.invalid`（`PluginMessages.ENVELOPE_INVALID`，ADR-0315），`catalog()` 与 `templates()` 多这个键。

## 语言映射

规范把一部分写法交给各语言自己声明，Java 的写法如下。

- **载荷的请求类型**：用记录或带 Jackson 注解的类声明。必填成员写 `@JsonProperty(required = true)`，可选成员不加注解（缺少时取 Java 的缺省值：基本类型为 0 / `false`，引用类型为 `null`）。`String`、集合与数组、`Map`、嵌套的记录与 `JsonNode` / `Object` 接受 `null`；整数、浮点与布尔不论是基本类型还是包装类型都不接受 `null`，它们的列表项与字典值同样不接受。认不出的类型（枚举、`java.time`、`Optional`、`Maybe` / `TypedId`、自定义解码器、多态类型）在读到的位置单独交给 Jackson；`byte[]` 读 Base64 字符串。任意 JSON 的成员（`JsonNode`、`Object`）原样保留读到的值，写回时 JSON 值相等。
- **模板参数的值模型**（RFC-0010「模板」）：`null` 是空；`Boolean` 是布尔；`String`、其他 `CharSequence` 与 `Character` 是字符串；`Byte`、`Short`、`Integer`、`Long`、`AtomicInteger`、`AtomicLong` 是 64 位整数，`BigInteger` 超出 64 位时是浮点；`Double` 是浮点，`Float` 与 `BigDecimal` 先换成最接近的 `double`（`0.1f` 写 `0.10000000149011612`）；键都是 `String` 的 `Map` 是对象，`Collection` 与 Java 数组是数组。模型之外的值（枚举、`UUID`、`java.time` 的类型等）写成 `toString()` 的文本，不跨语言，插件要跨语言格式化时先转成字符串。
- **线上编码的数字**：`Error.details()` 里的数字写出时按上面的值模型；读回来时 64 位范围内的整数是 `Integer` 或 `Long`，其余是 `Double`，非有限值读回来是字符串。`Result.value` 的内容由值类型自己的 Jackson 配置写出。
- **同名的 `Error`**：`com.xihanfun.framework.primitives.Error` 与 `java.lang.Error` 同名，在用到它的文件里显式导入；它不是 `Throwable`，误用在编译期就报错。
- **可空性**：公开成员不接受也不返回 `null`：可能没有的值以 `Optional` 返回（`findBaggage`、`tenant()`），需要线上编码的数据字段用 `Maybe`，集合没有时是空集合；参数传 `null` 抛 `NullPointerException`。

## 设计取舍

- **并发：虚拟线程上的同步签名，不用 `CompletableFuture`。** 中间件、处理器与调用器都是同步方法，按 Java 21 虚拟线程设计：阻塞等待 I/O 时让出载体线程，调用栈与异常栈可读。构建出的 `Step`、`OperationInvoker`、`OperationRegistry` 不可变或线程安全；`Context` 不可变、按派生传递，每次操作调用的状态随派生的上下文传递（`Context.withValue`），嵌套与并发调用互不干扰；锁一律用 `ReentrantLock`，不在 `synchronized` 里阻塞。
- **取消与截止时间自己建模。** `Context` 有两个令牌：截止令牌由定时器在截止时间触发，外部令牌由 `CancellableContext.cancel()` 触发；父级取消连带子级，子级取消不影响父级。归因只看令牌：截止令牌已触发即 `KERNEL_TIMEOUT_001`，不论调用方是否也已取消。
- **JSON：Jackson。** Java 标准库没有 JSON，`jackson-databind` 是 JVM 上的事实标准、本身是 JPMS 模块、支持记录类型。原语核心类型不依赖 Jackson（原语层不含序列化代码），编码在 `primitives.json` 包里；清单只用树模型按 Schema 逐项核对、不做反射绑定，写出是手写的排版；操作用 `OperationJson.newMapper()`（注册原语编码、忽略未知字段、类型不符一律失败）。
- **有界。** 对象袋最多 1024 个条目、Baggage 最多 180 个、按派生的值最多 256 个键、操作登记表 10000 个操作，超出是编程或配置错误。管道的中间件与顺序约束不设上限，与 .NET 一致：它们由代码在启动时登记，不来自外部输入。
- **插件运行时：grpc-java，生成代码提交进仓库。** 传输是 `grpc-netty-shaded`（自带改了包名的 Netty，不与应用自己的 Netty 冲突）加 `protobuf-java`，服务端以虚拟线程执行每一次调用，处理器、钩子与回调都可以阻塞。协议代码由 `spec/plugin/plugin.proto` 生成并提交进不导出的包 `plugin.v1`：构建不需要下载 protoc，各平台一致，CI 重新生成后核对没有差异。

## 一致性

- 现状：原语、清单、调用上下文、管道、操作与进程外插件运行时可用，示例插件 `examples/echo` 通过规范黑盒运行器的全部 15 个用例（Linux 上经 Unix 域套接字，Windows 上经回环 TCP 与 `.cmd` 启动脚本）；`events.list` 回显处理器拿到的信封（`envelopes`），以 `--envelopes events.list` 跑 `event-envelope`（ADR-0327）。
- 认领的档（RFC-0024）：`core` 与 `plugin`；档的定义见规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。
- 缺口：无。
- 钉的规范版本：SDK 仓库 `spec/` 子模块指向的提交；发版时子模块必须正好在规范标签上。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 尚未提供：流式操作（操作契约还没有流式形态，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束）、Wasm 载体与远程 Provider 的插件侧（如 `caching.distributed@1`）。
- 怎样验证：测试直接读取 `spec/conformance/vectors`，期望值逐字相等；`VectorCoverageTest` 按档表核对认领的档要求的每个向量要么有测试回放、要么列为缺口；`EchoConformanceTest` 用 submodule 里的黑盒运行器跑示例插件的全部用例。CI 在 Linux 与 Windows 上都跑。
- 信封的 `time`：回放 `plugin/sdk-envelope-times.json` 的纳秒一列（ADR-0310）。写出的文本与 `text.nanoseconds` 逐字相同，读出的时刻与 `utc.nanoseconds` 相等，`unreadable` 的每一条投递都以 `INVALID_ARGUMENT` 拒绝。`Instant` 是纳秒精度，不截断，没有跳过的用例。`plugin/envelope-times.json` 现在同在 `plugin` 档。
- Baggage 的值：`primitives/baggage-keys.json` 的 `values` 另有以码元写出的 `acceptableUtf16` 与 `unacceptableUtf16`，按 UTF-16 码元直接组成字符串回放：前一组照收，后一组 `isAcceptableValue` 为假、`withBaggage` 抛出（ADR-0321）。

## 破坏性改动与迁移

首个对外版本是 `v0.2.0-rc.1`。之后的变化：

- 弃用 `Context.cancellation()`（旧名仍可用）→ 改用 `Context.cancellationError()`。
- 弃用 `Host.INVOKE_PERMISSION` / `Host.PUBLISH_PERMISSION`（旧名仍可用）→ 改用 `Host.PERMISSION_INVOKE` / `Host.PERMISSION_PUBLISH`。
- 弃用 `LifecycleState.stateName()` 与 `PipelineStage.manifestName()`（旧名仍可用）→ 改用各枚举统一的 `wireName()`。
- 弃用 `OperationErrors.UNEXPECTED`（旧名仍可用）→ 改用 `InvocationErrors.UNHANDLED_EXCEPTION`。
- 管道终点报的「操作未登记」（`kernel.operation.unknown`）补上了参数 `id`，文案能填出操作 ID → 不用改。
- `OperationId.validate`、`OperationContract.of` 与读清单不再接受超过 128 个字符的操作 ID（抛 `IllegalArgumentException`，读清单为 `kernel.manifest.invalid`），原先不设上限；新增常量 `OperationId.MAX_LENGTH` → 把 ID 缩到 128 个字符以内。
- 清单 `activation` 里 `onEvent:` 的模式可以带下划线（如 `onEvent:order_events.*`），原先读不出 → 不用改。
- `ResolvedBinding` 新增记录组件 `properties`（绑定的属性，ADR-0285），三个参数的构造保留 → 按三个组件解构的记录模式补上第四个；`toString()` 多了属性名一段，匹配旧文本的地方改掉。
- `ResolvedBinding` 实现 Jackson 2 的 `JsonSerializable`：Jackson 写出的只有 `name`、`type` 与按升序排列的 `propertyNames`，不再写 `value` 与 `properties`，也读不回 `ResolvedBinding` → 要序列化值的自己取 `value()` 与 `properties()`。
- `Envelope.time()` 改为 `Optional<Instant>`，`Builder.time(String)` 换成 `time(Instant)`（ADR-0306）→ 传 `Instant`（`Instant.now()`、`OffsetDateTime.parse(text).toInstant()`），要文本时自己格式化。
- 宿主写来的 `time` 读不出时投递以 `INVALID_ARGUMENT` 拒绝 → 不用改。
- `Envelope.toString()` 有 `dataschema` 时多一段 `, dataSchema=…` → 按旧文本断言的测试要更新。
- 新增 `dataSchema()`、`Builder.dataSchema(String)`（ADR-0305）→ 不用改。
- 错误细节里的孤立代理项当作没有细节，原先换成 `?` 读出（ADR-0301）→ 不用改。
- `OperationRequest.of(String, String)` 严格编码成 UTF-8：孤立的代理项不再换成 `?`，本地调用得到 `KERNEL_VALIDATION_001` + `kernel.operation.invalid_payload`（`reason` 为 `$`），`Host.invoke` 不发请求、在本地以同一个错误失败；新增 `OperationRequest.hasUnencodableText()`（ADR-0312）→ 不用改；要发出的 JSON 文本里不要有孤立的代理项。
- `Envelope.Builder.data(String)` 严格编码成 UTF-8：孤立的代理项不再换成 `?`，`Host.publish` 不发出这一项、在本地以 `MESSAGING_VALIDATION_002` + `messaging.envelope.invalid` 失败，其余照常发布；新增 `Envelope.hasUnencodableData()`，`equals` 与 `hashCode` 也比较它（ADR-0315）→ 不用改；要发布的文本里不要有孤立的代理项。
- `PrimitivesModule` 写出 `Result` 与 `Error` 时，字符串有孤立的代理项、从根算起嵌套超过 64 层或有重复成员即抛 `JsonMappingException`、什么也不写，原先孤立的代理项写成 `\ud800` 这样的转义、深层与重复成员照写（ADR-0320）→ 写出之前去掉这类值。
- 已登记 Baggage 键的值有孤立的代理项时 `BaggageKeys.isAcceptableValue` 为假：`Context.Builder.baggage` 丢弃这个成员、`withBaggage` 抛 `IllegalArgumentException`，原先照收（ADR-0321）→ 从外部输入拼值之前先去掉孤立的代理项。
- 插件写给宿主的错误细节有孤立的代理项或超过 64 层时这个错误不带细节、其余照写，原先孤立的代理项被 protobuf 写成 `?`（ADR-0320）→ 不用改。
- `PluginMessages` 新增 `ENVELOPE_INVALID`，`catalog()` 与 `templates()` 多了 `messaging.envelope.invalid` 这个键（ADR-0315）→ 按键集断言的测试要更新。
- 回调客户端关闭之后的 `Host.invoke` 与 `Host.publish` 不发请求、不重新连接，以 `plugins.host.unavailable` 结束，排在载荷与 `data` 的编码之后（ADR-0328）→ 不用改。
