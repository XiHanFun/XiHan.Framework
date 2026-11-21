# Java SDK

[framework-sdk-java](https://github.com/XiHanFun/framework-sdk-java) 是 XiHan Spec 的 Java 实现：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。用它可以把与 .NET 宿主（参考实现 XiHan.Framework）相同的原语用在 JVM 应用里，也能写在 .NET 宿主里运行的 Java 插件。规范以 submodule 放在仓库的 `spec/`，规范写到的行为与 .NET 一致，由规范仓库的一致性向量与黑盒运行器验证；规范怎样组织、各语言 SDK 的现状见[规范与多语言](/appendix/spec)。

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

插件运行时另用 grpc-java 与 protobuf-java。它们在本构件里是可选依赖（`<optional>true</optional>`，模块描述里是 `requires static`），不传递给使用者，其余包不引用它们；写插件的应用自己加上，版本与 SDK 仓库 [`pom.xml`](https://github.com/XiHanFun/framework-sdk-java/blob/main/pom.xml) 的 `grpc.version`、`protobuf.version` 相同（生成代码要求 protobuf-java 不低于生成它的版本）：

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

整个 SDK 是一个 Maven 构件 `com.xihanfun:xihan-framework`，与其他语言的 SDK 一样只有一个包，JPMS 模块名 `com.xihanfun.framework`，按 Java 包划分：

| Java 包 | 内容 | 规范 |
| --- | --- | --- |
| `com.xihanfun.framework.primitives` | `Error` / `ErrorCode`、`Result<T>`、`Maybe<T>`、`TypedId<Tag, V>`、`LifecycleState`、`PipelineStage`、`Stability`、`BaggageKeys`、消息键与模板（`MessageTemplate`、`MessageCatalog`、`MessageScope`）、`FrameworkException`、`FrameworkInfo` | RFC-0001、RFC-0010、`conventions/` |
| `com.xihanfun.framework.primitives.json` | 线上 JSON 编码：Jackson 模块 `PrimitivesModule` | RFC-0001「线上编码」 |
| `com.xihanfun.framework.manifest` | `xihan.manifest.json` 的模型、按 Schema 严格读取、规范化输出与校验，能力声明与依赖文本 | RFC-0002、`manifest/manifest.schema.json` |
| `com.xihanfun.framework.invocation` | `Context`：取消与截止时间（按令牌归因为 `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001`，可登记回调）、W3C 追踪、已登记的 Baggage、对象袋 `Items`、按派生传递的值 | RFC-0003 |
| `com.xihanfun.framework.pipeline` | 十一阶段的 `PipelineBuilder`：`use` / `useAt` / `useNamedInstance`、`before` / `after`（类型名或实例名）、`describe` / `dump`、`build`；排序规则与错误码 `KERNEL_PIPELINE_00x` | RFC-0004 |
| `com.xihanfun.framework.operation` | `OperationContract`、`OperationSchema`、`OperationHandler<Req, Res>`、容量 10000 的 `OperationRegistry`、经完整管道执行的 `OperationInvoker`（XGI：JSON 请求与响应，另有类型化入口） | RFC-0011 |
| `com.xihanfun.framework.plugin` | 进程外插件运行时：`PluginRuntime.serve(PluginOptions)`、生命周期回调 `PluginHooks`（配置、启动、停止、健康）、事件订阅 `Subscription` 与 `EventHandler`、握手交出的消息目录 `PluginCatalog`、宿主回调 `Host`（`Host.Invoke` 与批量的 `Host.Publish`）、stderr 的 JSON 行日志 `PluginLogger`；协议代码在不导出的 `com.xihanfun.framework.plugin.v1` | RFC-0017、`plugin/plugin.proto`、`plugin/handshake.md`、`plugin/lifecycle.md`、`plugin/invocation.md` |

包之间的依赖方向：`plugin → operation → pipeline → invocation → primitives`，`plugin → manifest → primitives`。外部依赖只有 `jackson-databind`，模块描述里是 `requires transitive`。

## 快速开始

在 Java 应用里用原语：

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

- 可恢复失败返回 `Result` / `Error`，不抛异常。错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键 + 语言文件（RFC-0010），`Error.message()` 在没有已解析文案时按 `MessageScope` 的进程默认目录解析，翻译不出时用 SDK 自带的内核消息英文模板（`KernelMessages`，与 `kernel-messages.json` 逐字相同），再退回 `键 (参数)` 的描述。
- 配置错误（清单不合法、管道成环、操作重复登记、登记表已满）抛出带错误码的 `FrameworkException`；参数格式不合法这类调用方的编程错误抛 `IllegalArgumentException`，状态不允许抛 `IllegalStateException`。消息是英文句子，标识符加单引号。
- `Result<T>`、`Error`、`Maybe<T>`、`TypedId` 的 JSON 编码按 RFC-0001「Result」：`new ObjectMapper().registerModule(new PrimitivesModule())`；从线上收到的文本用 `PrimitivesJson.read(mapper, bytes, type)` 读，它先过共同的 JSON 文本检查。生命周期迁移表、管道阶段顺序、错误码、Baggage 键与值、能力依赖文法、JSON 文本检查、线上编码、消息模板、内核错误、清单的读取与写出、载荷解码与管道排序都由 `spec/conformance/vectors` 验证，期望值逐字相等。

## 写一个插件

宿主一侧怎样发现、启动与降级进程外插件，见[进程外插件](/plugins/out-of-process)；这一节讲用本 SDK 写插件一侧。

插件是宿主启动的子进程：`main` 里登记操作与订阅，交给 `PluginRuntime.serve`，它读宿主下发的环境变量、监听本机端点、向 stdout 写一行握手行，然后服务到 `Stop` 完成、stdin 关闭或收到 SIGTERM：

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

清单 `xihan.manifest.json` 放在插件目录里，运行时从工作目录（即插件目录）读它，握手时原样交给宿主，宿主核对它与目录里的文件逐字段一致：

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

- `kind` 是 `plugin`、`isolation` 是 `out-of-process`；不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；`primitives` 等于本 SDK 支持的原语层大版本 `1`；登记的操作不重复且都在 `contributes.operations` 里，订阅的（主题，类型）不重复。不合即拒绝启动（`handshake.md` §1）：不监听、不写握手行，在 stderr 记一条 `error`，`serve` 抛出带错误码的 `FrameworkException`（如 `PLUGINS_VALIDATION_001`、`PLUGINS_VALIDATION_004`）。
- 回调报的健康项要出现在 `contributes.health`，不在的丢掉、同名只记一条 `warning`；回调宿主要在 `permissions` 里声明 `capability:kernel.pipeline`（`Host.Invoke`）或 `capability:messaging.queue`（`Host.Publish`），并由宿主在 `Configure` 里授予：运行时发出回调之前在本地核对，缺一即 `PLUGINS_DENIED_002` + `plugins.callback.denied`、不发请求。
- `entry` 是相对插件目录的可执行文件（Windows 上没有扩展名时宿主补 `.exe`）。Java 插件的 `entry` 指向启动脚本：Unix 上是带 shebang、可执行的 sh 脚本，Windows 上是 `.cmd`。宿主只继承 `handshake.md` §1 列出的环境变量，`JAVA_HOME` 不在其中，所以脚本里写 `java` 的绝对路径与完整的类路径，例如 `exec /opt/java/bin/java -cp '/opt/greeting/lib/*' GreetingPlugin "$@"`。
- 处理器里用 `Host.of(context)` 回调宿主：`invoke(context, "orders.create", payload)` 经宿主的完整管道调用操作，`publish(context, envelopes)` 批量发布、逐项结果。

用规范仓库的黑盒运行器验证（需要 Go，参数见规范仓库的 [`conformance/README.md`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/README.md)）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --sample 'greetings.create={"name":"hi"}'
```

完整的示例在 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-java/tree/main/examples/echo)：回声、经宿主回调转发、故意崩溃（验证时排除）、列出收到的事件四个操作，一个事件订阅、一个健康项与配置节 `XiHan:Echo`。测试 `EchoConformanceTest` 把它放进临时目录、按当前平台生成启动脚本，再从 submodule 运行黑盒运行器跑全部用例。

## 与宿主共用的行为

按规范 ADR-0233、ADR-0235、ADR-0236、ADR-0237 统一的行为，各语言 SDK 与 .NET 相同：

### 文本、编码与消息

- **JSON 文本**：Result 与 Error 的线上编码、操作载荷与清单先过同一遍检查（`JsonText.check`，向量 `json-text.json`）：语法按 RFC 8259（无注释、尾逗号、`NaN`、前导零、正号、多根值，空文本与只有空白也不合法），文本是 UTF-8、开头的 BOM 是语法错误；嵌套不超过 64 层；字符串与成员名必须是合法 Unicode（拒绝非法 UTF-8、过长编码、编码成 UTF-8 的代理项、原始控制字符、非法转义、孤立的代理项转义）；任何一层都不能有重复成员（按转义还原后比较）。语法错误优先，位置只写 `$`；否则写第一处重复的路径。Jackson 会跳过 BOM、按字节猜 UTF-16、放过孤立的代理项，所以这一遍不经 Jackson。
- **线上编码**：无值成功（`Result<Unit>`）只写 `{"ok":true}`；`Result<Maybe<T>>` 的无值是 `"value":null`。`Error` 的 `message` 必填，且至少含一个空白之外的字符：空串与只有空白都不合法（上一版只拒绝空串）。空白是 RFC-0001「空白字符」的 25 个码位，按这张表判断，不用 `Character.isWhitespace` 或 `String.isBlank`：它们不含 U+0085、U+00A0、U+2007、U+202F，却含 U+001C–U+001F。`Error.literal` 与 `withText` 的文案、`FrameworkException` 的说明（它是 `toError()` 的文案）同样，不合时抛 `IllegalArgumentException`。`messageKey`、`details`、`inner` 缺失表示没有，写成 `null` 不合法；未知成员忽略。重复成员在任何一层都不合法，含 `value`、`details` 与 `inner` 的内部（上一版不查 `value` 里面）；解码器自己核对重复、64 层与孤立的代理项，不看 `ObjectMapper` 的配置。数字按值模型：64 位范围内的整数写法原样往返（9007199254740993 不会变成 9007199254740992），其余是双精度浮点；细节里整数值的浮点写成 `2.0`、`-0.0`、`1.0E17`，非有限的写成字符串 `"NaN"`、`"Infinity"`、`"-Infinity"`（以前细节里不许放非有限值）。
- **模板**：消息键最长 128 个字符。说明符只认 `^[DdFfNnXx][0-9]{0,2}$`：`D` 与 `X` 只用于整数，`X` 只用于非负整数，`F` 与 `N` 用于整数与浮点，不适用或不认的（`E2`、`G`、`0.00`、`yyyy`、`D100`、带空格）一律按无格式写出（上一版精度可到 99）。`F` 与 `N` 按精确十进制展开、正好一半取偶（0.125 写 `F2` 得 `0.12`，2.675 写 `F2` 得 `2.67`），写出全部整数位，负数舍入成零保留负号。数组与对象写成紧凑 JSON，对象成员按名字的 UTF-16 码元序排列，超过 64 层的容器写 `null`（上一版用 Java 的 `toString`）。浮点 1e-4 ≤ |x| < 1e17 不用指数，范围外写 `1E+17`、`9.9E-05`、`5E-324`，负零 `-0`。`Describe` 的参数按名字的序数顺序。
- **区域**：`MessageTemplate.format(template, arguments, locale)` / `tryFormat(…, locale)` / `text(value, locale)` 与 `MessageCatalog.resolve(key, locale, arguments)` 按区域取小数点与分组符：de-DE、es-ES、pt-BR 是 `,` 与 `.`，fr-FR 是 `,` 与 U+202F，ru-RU 是 `,` 与 U+00A0，其余区域与不给区域是 `.` 与 `,`；区域名不区分大小写。小数点用在无格式的浮点（含指数的尾数，de-DE 的 1.5e20 写 `1,5E+20`）与 `F`、`N` 上，分组符只用在 `N` 上；整数无格式时不分组，`D`、`X` 与数组、对象里的数字不随区域变化。`Error.message()` 按 `MessageScope` 的进程默认区域取符号，回退到内置 en-US 模板时也一样。
- **内核错误**：错误码、消息键与参数照 `kernel-messages.json`，内置的英文模板与它逐字相同，含清单读取的 9 个键，所以清单读取失败的 `message` 是与 .NET 相同的英文句子；`kernel.operation.type_mismatch` 只带 `id`（上一版带 Java 的类型名 `expected` / `actual`）。配置期的重复登记与登记表已满仍抛 `FrameworkException`，`toError()` 交出带键与参数的错误。

### 调用器与载荷

- **调用器的内置步骤**：位置与 .NET 的内置中间件相同，都按归属 `xihan.kernel.core`（`OperationInvoker.KERNEL_OWNER`）放在所在阶段的 `Beginning`，与同位置的其他中间件按归属的序数序、同归属按登记顺序排列（内置步骤登记在使用者的中间件之后）。Observe 阶段翻译异常（`KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`，`FrameworkException` 的错误进 `inner`，取消引起的异常按令牌归因，`java.lang.Error` 不翻译），只接住排在它之后的异常：Ingress 阶段与 Observe 阶段 `Beginning` 上归属排在它之前（如 `app`）的中间件抛出的异常原样交给调用方。Resilience 阶段核对截止时间，进入时已到即 `KERNEL_TIMEOUT_001` + `kernel.invocation.deadline_before_start`（上一版在第一个中间件之前核对，已撤回）；下游把取消写成了结果（`KERNEL_CANCELLED_001`）而截止令牌已触发时改报 `KERNEL_TIMEOUT_001` + `kernel.invocation.deadline_exceeded`，调用方同时取消也一样（RFC-0003 规则 1；上一版原样返回取消），抛出的取消在 Observe 阶段按同一规则归因。Validate 阶段解码原始调用的载荷，类型化调用跳过。进入管道前只核对未登记（`KERNEL_NOTFOUND_001`）与类型化调用的类型不符（`KERNEL_VALIDATION_002`），按这个顺序。调用器复制使用者的 `PipelineBuilder`（`copy()`）再加内置步骤，不改动它。
- **响应的编码**：XGI 入口在管道结束之后才把响应编码成载荷，中间件看到的是编码之前的结果；`Operations.findResponse(context)` 给出 handle 阶段执行成功的响应对象（上一版的 `findResponsePayload` 给载荷，已撤回）。写不出时（类型化的浮点成员是 NaN 或无穷、序列化不支持的值）以 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception` 结束，异常链上有 `FrameworkException` 时它的错误进 `inner`，不抛给调用方，没有载荷（上一版是 `Error.literal` 的英文消息）。NaN 不写成字符串：编码时关掉 Jackson 的 `WRITE_NAN_AS_STRINGS`，写出的文本再过共同的 JSON 文本检查。类型化入口不编码。
- **取消只经令牌**：调用器与内置步骤不中断线程、不取消 future，只让上下文的令牌触发；令牌触发之后处理器照常返回结果的，调用以那个结果结束。处理器要尽早结束时自己看 `context.cancellation()`、`onCancelled` 或 `awaitCancellation`。
- **载荷解码**：先过共同检查（语法错误写 `$`，重复成员写第一处重复的路径，先于一切类型问题）；空载荷等价于 `{}`，只有空白是语法错误（上一版当作 `{}`）；然后按请求类型以文本顺序核对（`RequestShape`，不靠 Jackson 的读取顺序）：成员名区分大小写、未知成员忽略、数字不从字符串读、整数不接受 `1.0` 与 `1e2`、超出类型范围即不符、浮点接受整数写法且 1e400 上溢为无穷、1e-400 为 0；必填成员缺少时在所在对象结束时报该对象的路径。载荷的文本按 JSON 值比较，空白、转义与数字的写法不是契约；任意 JSON 类型的成员（`JsonNode`、`Object`）原样保留读到的值：超出双精度的整数精确，`2.0` 与 `2`、`-0.0` 与 `0` 分得开，`1e400`、`1e-400` 这类双精度表示不了的数保留原文的精确十进制，写回时 JSON 值相等，不变成 `null`、无穷或 0。`reason` 只写路径：载荷是 `null`、不是对象或根上就不合时写 `$`；成员名含 `.`、空格、`'`、`/`、`"`、`[`、`]`、`(`、`)`、`\`、制表、换行、回车、换页、退格、U+0085、U+2028、U+2029 时写 `$['名字']`，其余写 `$.名字`。

### 清单与文法

- **清单的读取**：按 JSON 文本 → 字面量 `null` → 按 Schema 逐项核对 → 跨字段约束的顺序，每一步只报第一处问题，错误码都是 `KERNEL_CAPABILITY_006`（上一版是 `PRIMITIVES_VALIDATION_001` 加英文消息）：`kernel.manifest.invalid_json`（`reason` 是 `$` 或第一处重复的路径）、`kernel.manifest.empty`（根是 `null`，没有参数）、`kernel.manifest.invalid`（`reason` 是第一处问题的路径：成员按文本顺序，每个成员依次查 Schema 之外 → `null` → 类型 → 取值，对象查完后再按 `required` 的顺序查缺少的成员）；跨字段约束按 `primitives.manifest.isolation_required` → `isolation_not_allowed` → `duplicate_provides` → `duplicate_requires` → `duplicate_bindings` 的顺序，参数只有 `id`。`ManifestJson.read(byte[])` 不跳过 BOM；`readFile` 按 UTF-8 读并跳过开头的 UTF-8 BOM，文件不存在为 `kernel.manifest.file_missing`（参数 `path`，以前抛 `IOException`）。接口版本不带前导零、不超过 2147483647，只有依赖的版本可带一个 `^`；中间件名非空，`contributes.health` 按 `^[a-z][a-z0-9.-]*$` 核对，`contributes.permissions` 重复时报第二次出现的项；写了空数组的 `activation` 不合法。非 Wasm 插件贡献中间件交给宿主在发现时拒绝，读取与构建清单不再核对（与 .NET 一致）。`Manifest.Builder.build()` 的跨字段约束带同样的消息键与 `id`，错误码是 `PRIMITIVES_VALIDATION_001`。
- **清单的写出**：`ManifestJson.write` 的排版照 RFC-0002「写出」逐字固定：两个空格缩进、LF、末尾不换行、`": "`；`$schema` 总写 `https://spec.xihanfun.com/manifest/v1.json`，列表总写（空列表写 `[]`），`role` 缺省写 `provider`、`activation` 缺省写 `["onStartup"]`，依赖总写 `optional`、绑定总写 `required`、中间件总写 `before` / `after`，`contributes` 总写；字符串只转义 `"`、`\`、控制字符与孤立的代理项（`<`、`&`、U+2028 与非 ASCII 原样）。上一版省略缺省值并转义 `<>&`。`Manifest` 按生效的值比较（不看 `$schema`，没写的角色、激活事件、贡献点与绑定的 `required` 按缺省），写出的清单再读一遍与原来的相等。
- **文法**：规范的正则都按整串匹配，结尾的换行不例外（`xihan.tenant\n`、带换行的 `traceparent` 不通过）；Java 一律用 `Matcher.matches()`。能力依赖的版本不接受前导零、`+`、空白与超过 2147483647 的值。

### 上下文与管道

- **Baggage 值**：已登记键的值要非空、按 UTF-16 码元计最多 256、不含 U+0000–U+001F 与 U+007F–U+009F。外部输入 `Context.builder().baggage(…)` 丢弃不合的成员、已有的同名值保留；程序内派生 `withBaggage` 给已登记的键一个不合的值抛 `IllegalArgumentException`（上一版是去掉这个键）。`xihan.idempotency-key` 两种情形都例外。
- **追踪**：`traceparent` 只认 `^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$`、标识不能全零，不合法的当作没给；只取采样位，写出只写 `00` / `01`（上一版整字节保留标志、有 `traceFlags()`，已撤回）；没有上游时写 `01`。
- **管道**：重复登记、未知阶段、约束主体未登记或按名字有歧义在登记时报；约束目标未登记或有歧义、跨阶段与成环在排序（`describe` / `dump` / `build`）时报。排序与 .NET 一样逐个阶段处理：阶段内按基准次序逐个主体解析它的约束（先 before 再 after，各按声明顺序），解析完再排序，所以几处错误同时存在时报的是同一个。按名字的约束与 .NET 同一种匹配：按名字登记的实例只认实例名，没有名字的登记认中间件名（上一版中间件名也能对上按名字登记的实例）。错误消息是 .NET 的英文原文，按名字登记的实例写成 `类型名[名字]`，成环时按基准次序列出全部仍被卡住的中间件，如 `Middleware ordering constraints in stage 'validate' form a cycle: 'Guard[a]', 'Guard[b]'.`。

### 插件

- **插件的启动与握手**（RFC-0017，`plugin/handshake.md`）：监听之前核对清单与声明，不合即拒绝启动。握手行的地址不含空白，空白是 RFC-0001「空白字符」的集合（与 .NET 正则的 `\s` 相同，含 U+0085、U+00A0、U+2007、U+202F、U+3000，不含 U+001C–U+001F 与 U+200B，与 `Character.isWhitespace` 不同）；`tcp` 只写 `127.0.0.1:<端口>`。解析握手行与端点的代码按同一文法：地址是一个或多个空白之外的字符（非 ASCII 字母与 `;` 可以出现），`tcp` 只认 `127.0.0.0/8` 的四段点分十进制（不带前导零）或 `[::1]`，端口 1–65535、不带正负号与前导零（向量 `handshake-lines.json`）。宿主的每个 RPC 先按常数时间核对元数据 `xihan-plugin-session`：必须恰好一个值，没有、不匹配或有两个及以上（即使其中一个正确）都以 `Unauthenticated` 拒绝、不执行；令牌先于一切，含次序核对与流式调用读第一帧。握手选宿主列表里最高的共同版本（向量 `protocol-versions.json`），没有交集以 `FailedPrecondition` 拒绝、状态不变；`primitives` 写清单的值，`manifest` 写清单文件原文并去掉开头的 UTF-8 BOM，只声明一元操作，订阅按声明的顺序、`mode` 总写 `competing` 或 `broadcast`，`catalogs` 只发作者自己的目录，SDK 运行时的目录不随握手发送。
- **插件的次序与生命周期**（`lifecycle.md` §7）：`Handshake` 只在 `Declared`、`Configure` 只在 `Resolved`、`Start` 只在 `Configured` 接受，各一次；`Stop` 在 `Declared`、`Draining`、`Stopped` 与另一次停止进行中都不接受；不在次序表里的组合以 `FailedPrecondition` 拒绝、什么都不做、状态不变。配置节为空等同 `{}`，不是 JSON 对象（过共同的 JSON 读取检查）时不调配置回调，以 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为 `configure`，`reason` 为 `the configuration is not a JSON object`）进入 `Failed`。启动回调成功后进入 `Started`，立即求一次健康回调，按结果进入 `Ready`、`Degraded` 或 `Failed`（没有健康回调、回调报其他状态即 `Ready`，回调抛出即 `Degraded`），然后才应答 `Start`。只有 `Ready` 与 `Degraded` 可服务，其余状态的调用与投递以 `PLUGINS_UNAVAILABLE_001` + `plugins.process.unavailable`（`state` 是状态名）失败。`Health` 任何状态都可调用：`Start` 之前报本地状态（`Declared`、`Resolved` 或 `Configured`），没有健康项、不调回调；`Ready` 与 `Degraded` 期间每次都求一次健康回调，回调报的 `Ready`、`Degraded`、`Failed` 按合法迁移改变状态（`Failed` 不再回来），报其他状态的不改变状态，回调抛出时这一次的结果是 `Degraded`、没有健康项，错误为 `plugins.process.failed`（`phase` 为 `health`），状态也与报了 `Degraded` 一样迁到 `Degraded`，之后的结果照常决定状态；`Draining`、`Stopped`、`Failed` 不再调健康回调。`WatchHealth` 先推当前状态，之后状态、健康项或错误变化时推，可服务期间每 10 秒求一次健康回调，进入 `Draining` 时推一次，应答 `Stop` 之后结束流（上一版每秒核对一次、进入 `Draining` 即结束流）。
- **插件的停止与收尾**（`lifecycle.md` §7.5–§7.7）：启动回调没成功过时 `Stop` 不调停止回调，应答 `ok`，以退出码 0 退出；`Started`、`Ready`、`Degraded` 进入 `Draining`（健康回调报过 `Failed` 的不迁移）、拒绝新调用，等在途调用到宽限期（`grace_seconds` 为 0 按 5 秒），到时触发它们的取消信号、不再等，然后调停止回调（`reason` 原样交给作者）、应答、进入 `Stopped`、退出。停止回调在进程里最多调一次，返回失败或抛出时写进 `StopResponse.status`，照常退出（上一版 `Configure` 调用过就调停止回调）。stdin 读到结尾或读出错与 SIGTERM 都是收尾请求：可服务的插件进入 `Draining`（其他状态不迁移），立即触发全部在途调用的取消信号并等它们结束，启动回调成功过而停止回调还没调过的调一次（`reason` 为 `shutdown`）；从收到起最多 3 秒，到时不论是否完成都以退出码 0 结束进程，进行中的 `Stop` 同样受这 3 秒限制。生命周期与健康回调抛出时 `reason` 是异常的简单类型名（如 `IllegalStateException`，匿名类取去掉包名的二进制名如 `EchoPlugin$1`），不带异常的文本，异常只写进 stderr。
- **插件的调用与投递**（`invocation.md` §1–§4）：`Invoke` 依次核对令牌、操作在握手声明里（否则 `Unimplemented`）、可服务（否则 `PLUGINS_UNAVAILABLE_001`），然后还原上下文，经调用器与完整管道执行，空载荷等同 `{}`（上一版先报未就绪）。`InvokeStream` 读第一帧：流在第一帧之前结束或第一帧不是 `start` 为 `InvalidArgument`，第一帧是 `start` 一律 `Unimplemented`，不论操作是否声明、插件是否就绪；本 SDK 不提供流式操作的公开 API。上下文：`trace_parent` 按 RFC-0003 的文法，不合即没有父 Span；`trace_state` 不进上下文；Baggage 只留已登记的键；`deadline` 按严格文法读（向量 `deadlines.json`，`Instant` 精确到纳秒，九位小数不截断），读不出当作没有，生效的截止时间取它与 gRPC 截止时间中较早的一个；gRPC 取消即上下文取消。`HandleEvent` 依次核对令牌、有 `envelope` 且 `mode` 是空、`competing` 或 `broadcast`（否则 `InvalidArgument`）、按（主题，方式，类型）找订阅（一个也没有为 `KERNEL_NOTFOUND_001` + `plugins.event.unsubscribed`，参数 `id`、`topic`、`type`、`mode`，空方式写 `competing`）、可服务，然后按声明顺序依次交给每个匹配的订阅，第一个失败即终止并作为结果（上一版只交给第一个），处理器抛出为 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`。
- **插件的错误、消息键与日志**（`invocation.md` §3、§8）：写出的 `Error` 里带消息键的 `text` 留空，由宿主按目录翻译成调用方的语言，只有自由文本错误写 `text`（上一版按 `XIHAN_PLUGIN_LOCALE` 渲染）；`details` 按值模型写出，没有时为空；`inner` 照写。读宿主写来的错误：`code` 不合错误码文法时换成 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 一律为 `invoke`，`Host.Publish` 的结果也一样；`reason` 为 `invalid error code '<code>'`；原来的 `inner` 丢掉）；`message_key` 合文法的按键处理、不取 `text`，否则成为自由文本错误（文案取 `text`，为空或只有空白时取 `code`）；`details` 为空、只有空白、是空对象 `{}`、通不过共同 JSON 读取检查、不是对象，或任何一层有读成双精度会溢出的数时当作没有细节（下溢读成 0，超出 64 位的整数读成双精度），`inner` 同此（上一版整个错误换成 `plugins.process.failed`）；`ok` 为假却没有错误、或整个 `status` 缺失为 `plugins.process.failed`（`phase` 为操作 ID 或 `publish`，`reason` 为 `the host reported a failure without an error`）。向量 `error-reading.json`。运行时的七个消息键、参数（就是 `details` 的成员）与错误码照 `plugin-messages.json`，`PluginMessages` 带与参考实现相同的 10 种区域目录供本地渲染。stderr 每行一个 JSON 对象（`level`、`message` 与作用域属性），`java.util.logging` 的根日志也改写成这种行。
- **回调**（`invocation.md` §5、`handshake.md` §4）：启动时解析 `XIHAN_PLUGIN_HOST_ADDRESS`：一个或多个 `<network> <address>`，以 `;` 分隔，任何一项不合法整个值就不合法（向量 `host-addresses.json`）；按宿主的偏好取第一个本 SDK 能连的端点（回环 TCP，Linux 上有 epoll 时另有 Unix 域套接字；grpc-java 连不了命名管道），之后不换。宿主总在列表最后给出一个回环 TCP，所以 Windows 上的 .NET 宿主（命名管道加回环 TCP）也能回调。变量缺失、不合法或没有能连的项时照常服务，启动时 stderr 记一条 `warning`，每次回调都以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `no supported callback endpoint`）失败、不发请求（上一版变量不合法即拒绝启动）。发出之前在本地按宿主的顺序核对：所需权限（`Host.Invoke` 为 `capability:kernel.pipeline`，`Host.Publish` 为 `capability:messaging.queue`）既在清单 `permissions` 里、也在 `Configure` 授予的权限里，否则 `PLUGINS_DENIED_002` + `plugins.callback.denied`；回调上下文的 `xihan.tenant` 是此刻在处理的某个调用或投递的租户，否则 `PLUGINS_DENIED_002` + `plugins.callback.tenant_denied`；最后，回调上下文的截止时间已过或已被取消时也不发请求，结果与映射表相应的一行相同（截止时间已过为 `KERNEL_TIMEOUT_001`，已被取消为 `KERNEL_CANCELLED_001`）；`Host.Publish` 这些都是整批的结果。顺序是端点、权限、租户、截止时间与取消。信封的 `xihantenant` 不在本地核对，交给宿主逐项判定（上一版本地逐项核对）。回调的元数据取自所在的调用上下文：当前 Span 的 traceparent、UTC 七位小数加 `+00:00` 的 `deadline`、已登记的 Baggage、空的 `trace_state`，gRPC 截止时间与 `deadline` 相同，带会话令牌；调用方取消上下文即取消回调，截止时间到了不在本地取消，由 grpc-java 按同一个截止时间以 `DeadlineExceeded` 结束，结果只按映射表、不另按时钟或令牌改写。gRPC 状态按 §5 的表映射（向量 `callback-status.json`）：`Unauthenticated` 为 `plugins.callback.session_invalid`，`PermissionDenied` 为 `plugins.callback.denied`，`DeadlineExceeded` 为 `KERNEL_TIMEOUT_001`，上下文的取消信号已触发的 `Cancelled` 为 `KERNEL_CANCELLED_001`，其余 `Cancelled`、`Unavailable` 与连接失败为 `plugins.host.unavailable`（`reason` 为 `<状态名>: <说明>`，状态名是 PascalCase），`Host.Invoke` 的 `Unimplemented` 为 `kernel.operation.unknown`、`InvalidArgument` 为 `kernel.operation.invalid_payload`（`reason` 为状态的说明），其余（含 `Host.Publish` 的这两种）为 `plugins.process.failed`（`phase` 为操作 ID 或 `publish`）。`Host.Publish` 的 RPC 失败时每一项都是同一个错误，结果数与信封数不同时每一项都是 `plugins.process.failed`（`reason` 为 `the host returned <n> results for <m> envelopes`）。

## 语言映射

Java 的写法与规范的对应：

- **模板参数的值模型**（RFC-0010「模板」）：`null` 是空；`Boolean` 是布尔；`String`、其他 `CharSequence` 与 `Character` 是字符串；`Byte`、`Short`、`Integer`、`Long`、`AtomicInteger`、`AtomicLong` 是 64 位整数，`BigInteger` 在 64 位范围内是整数、超出时是双精度浮点；`Double` 是浮点，`Float` 与 `BigDecimal` 先换成最接近的 `double`（`0.1f` 写 `0.10000000149011612`，`new BigDecimal("2.50")` 写 `2.5`）；键都是 `String` 的 `Map` 是对象，`Collection` 与 Java 数组（含基本类型数组）是数组。模型之外的值（枚举、`UUID`、`java.time` 的类型、键不全是字符串的 `Map` 等）写成 `toString()` 的文本，嵌在数组或对象里时写成这段文本的 JSON 字符串；它们不跨语言，插件要跨语言格式化时先转成字符串。
- **线上编码的数字**：`Error.details()` 里放 `Integer` / `Long` / `BigInteger` / `Float` / `Double` / `BigDecimal` 都可以，写出时按上面的值模型；读回来时 64 位范围内的整数是 `Integer` 或 `Long`，其余是 `Double`，非有限值读回来是字符串。`Result.value` 的内容由值类型自己的 Jackson 配置写出；读取时超出 64 位的整数按值模型先成为浮点，再交给值类型。
- **载荷的请求类型**：用记录或带 Jackson 注解的类声明。必填成员写 `@JsonProperty(required = true)`，可选成员不加注解（缺少时取 Java 的缺省值：基本类型为 0 / `false`，引用类型为 `null`）。`String`、`List` / 数组 / 其他集合、`Map`、嵌套的记录与 `JsonNode` / `Object` 接受 `null`（等于没有值）；整数、浮点与布尔不论是基本类型还是包装类型（`Long`、`Double`、`Boolean`）都不接受 `null`，它们的列表项与字典值同样不接受。认不出的类型（枚举、`java.time`、`Optional`、原语的 `Maybe` / `TypedId` 与其他自定义解码器、多态类型）在读到的位置单独交给 Jackson，失败时报 Jackson 给出的路径；`byte[]` 照 Jackson 读 Base64 字符串。
- **文本与字节**：`JsonText.check(String)`、`ManifestJson.read(String)`、`PrimitivesJson.read(mapper, String, …)` 先把字符串编成 UTF-8；字符串里有孤立的代理项（编不成 UTF-8）按语法错误处理，不像 `String.getBytes` 那样换成 `?`。
- **同名的 `Error`**：`com.xihanfun.framework.primitives.Error` 与 `java.lang.Error` 同名，在用到它的文件里显式导入；它不是 `Throwable`，误用在编译期就报错。
- **插件的回调与钩子**：`PluginHooks` 的方法都有缺省实现，返回 `Result<Unit>`，失败原样交给宿主；抛出（或返回 `null`）时为 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`reason` 是异常的简单类型名，`FrameworkException` 也一样，它的错误不进 `inner`（上一版 `reason` 是带包名的类名或异常消息）。`health()` 返回空表示没有健康回调。`PluginConfiguration.decode(type)` 先过共同的 JSON 文本检查，再按 Jackson 读：成员名按 ASCII 不区分大小写：交给 Jackson 之前按目标类型的属性逐层（记录与普通类、列表、数组、字典的值）把成员名改写成属性名，原样相同的优先，只把 `A`–`Z` 当作与小写相同，所以 U+212A（开尔文符号）不当作 `k`、非 ASCII 字母的大小写不算相同（上一版用 Jackson 的 `ACCEPT_CASE_INSENSITIVE_PROPERTIES`，它按 Unicode 换小写）；改写只换名字，数字保留原文；字符串叶子读成数字时按 JSON 数字的文法（`" 7"`、`+7`、`07`、`NaN` 都不行，通过的文本当作同一个 JSON 数字读），读成布尔时只认 `true`、`false`（ASCII 不区分大小写，`ſ` 这类大小写映射到 ASCII 的字符不算）；未知成员忽略。事件处理器抛出的异常翻译为 `KERNEL_UNEXPECTED_001`。运行时交给处理器与钩子的上下文里总有 `Host`（没有可用的回调端点时回调失败，而不是 `Host.of` 为空）；`Host.invoke` 返回 `OperationResponse`，`Host.publish` 返回与信封一一对应的 `List<Result<Unit>>`。收尾时限由规范定为 3 秒，`PluginOptions.Builder.shutdownGrace` 已删除。收到 SIGTERM 时关闭钩子收尾后以 `Runtime.halt(0)` 结束进程（不这样 JVM 以 143 退出）；服务期间作者自己调用 `System.exit` 也会经过这个钩子，同样以退出码 0 结束。

## 设计取舍

**并发：虚拟线程上的同步签名，不用 `CompletableFuture`。** 中间件、处理器与调用器都是同步方法（`Result<Unit> invoke(Context, Step)`、`Result<Res> handle(Context, Req)`），按 Java 21 虚拟线程（JEP 444）设计：阻塞等待 I/O 时虚拟线程让出载体线程，不占平台线程，调用栈与异常栈可读，不需要把每一层都改成回调链。为此：构建出的 `Step`、`OperationInvoker`、`OperationRegistry` 不可变或线程安全，可以被任意多个线程同时使用；`Context` 不可变、按派生传递，每次操作调用的状态随派生的上下文传递（`Context.withValue`），嵌套与并发调用互不干扰；锁一律用 `ReentrantLock`，不在 `synchronized` 里阻塞（Java 21 上 `synchronized` 会钉住虚拟线程）。

**取消与截止时间自己建模。** `Context` 有两个令牌：截止令牌由定时器在截止时间触发，外部令牌由 `CancellableContext.cancel()` 触发；父级取消连带子级，子级取消不影响父级。归因只看令牌（RFC-0003 规则 1、RFC-0004、ADR-0095）：截止令牌已触发即 `KERNEL_TIMEOUT_001`，不论调用方是否也已取消；只有外部令牌触发时为 `KERNEL_CANCELLED_001`。`onCancelled` 登记回调（已取消时立即执行），`awaitCancellation` 在虚拟线程上等待；`withCancellation` / `withDeadline` / `withTimeout` 返回的 `CancellableContext` 用完要关闭（try-with-resources），免得长寿命的父级积攒已结束的子级。

**JSON：Jackson。** Java 标准库没有 JSON；XGI 载荷、清单读取与线上编码都需要它。选 `jackson-databind` 是因为它是 JVM 上事实标准、本身是 JPMS 模块、支持记录类型，grpc-java 等常用库的使用者通常已经有它。原语核心类型不依赖 Jackson（RFC-0001：原语层不含序列化代码）：共同的 JSON 文本检查 `JsonText` 是手写的逐字节检查器，编码在 `primitives.json` 包里；manifest 只用树模型按 Schema 逐项核对（`ManifestShape`）、不做反射绑定，写出是手写的排版；operation 用 `OperationJson.newMapper()`——注册原语编码、忽略未知字段、类型不符一律失败——并在反序列化之前按请求类型以文本顺序核对载荷（`RequestShape`），报错的先后不随 Jackson 的内部路径变。模块路径上的应用要把请求与响应类型所在的包 `opens` 给 `com.fasterxml.jackson.databind`。

**可空性。** 公开成员不接受也不返回 `null`：可能没有的值以 `Optional` 返回（`findBaggage`、`find`、`tenant()`），需要线上编码的数据字段用 `Maybe`（编码为值或 `null`，对象里缺这个字段也解码为无值），集合没有时是空集合；参数传 `null` 抛 `NullPointerException`。

**有界。** 对象袋最多 1024 个条目、Baggage 最多 180 个、按派生的值最多 256 个键、操作登记表 10000 个操作，超出是编程或配置错误。管道的中间件与顺序约束不设上限，与 .NET 的 `PipelineBuilder` 一致：它们由代码在启动时登记，不来自外部输入（上一版限 1024 个中间件与 4096 条约束，已撤回）。

**插件运行时：grpc-java，生成代码提交进仓库。** 传输是 grpc-java 的 Netty 传输（`grpc-netty-shaded`，自带改了包名的 Netty，不与应用自己的 Netty 冲突）加 `protobuf-java`，服务端以 `Executors.newVirtualThreadPerTaskExecutor()` 执行每一次调用，处理器、钩子与回调都可以阻塞。协议代码由 `spec/plugin/plugin.proto` 生成并提交进 `src/main/java/com/xihanfun/framework/plugin/v1`，不在每次构建时生成：构建不需要下载 protoc 的原生程序、各平台一致，生成物可以审阅；`plugin.proto` 没有 Java 选项，`protocol` 配置先复制一份并在末尾补上 `java_package` 与 `java_outer_classname`，CI 重新生成后核对没有差异。生成代码不放宽门禁，照样过 `-Xlint:all -Werror` 与 doclint。端点：Linux 上 grpc-netty-shaded 自带的 epoll 原生库可用时（x86_64、aarch_64）监听 Unix 域套接字，套接字放进本进程新建的私有目录（名字随机、`0700`，套接字 `0600`）；Windows、macOS 与其余情形监听回环 TCP `127.0.0.1` 的随机端口（规范允许，同机的其他用户连得上，隔离只靠会话令牌）。本 SDK 不监听命名管道，所以不涉及命名管道的访问控制。回调同样只能连回环 TCP 与 Linux 上的 Unix 域套接字（grpc-java 不支持命名管道）：宿主按规范总在回调端点列表的最后给出回环 TCP，Windows 上的 .NET 宿主因此也能回调。JDK 23 起 Netty 用到的 `sun.misc.Unsafe` 内存访问会在 stderr 打告警，启动脚本里加 `--sun-misc-unsafe-memory-access=allow` 即可（JDK 21 没有这个选项）。运行时自己的消息键（`plugins.*`）是规范定下、与宿主共用的键（`invocation.md` §8），宿主有它们的目录，所以带这些键的错误 `text` 留空、目录不随握手发送；`PluginMessages` 的 10 种区域语言文件（文案与参考实现相同）供插件在本地渲染。

## 一致性

按 RFC-0024「实现钉规范」与「一致性档」：

- 认领的档：`core` 与 `plugin`（规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)）。
- 缺口：无；认领的档的向量全部回放，`plugin` 运行器子命令通过。
- 规范版本：钉在仓库 `spec/` 子模块指向的 framework-spec 提交；发版时子模块必须正好在规范标签上。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 已有的：原语、清单、调用上下文、管道、操作与进程外插件运行时（`plugin`，`xihan.plugin.v1`，grpc-java）可用，示例插件 `examples/echo` 通过规范仓库黑盒运行器的全部用例（Linux 上经 Unix 域套接字，Windows 上经回环 TCP 与 `.cmd` 启动脚本）；远程 Provider 的操作集（如 `caching.distributed@1`）尚未提供。

怎样验证：

- 测试按仓库根的相对路径直接读取 `spec/conformance/vectors`，期望值逐字相等；`VectorCoverageTest` 按档表核对认领的档：规范在认领的档里新增向量时它失败，要么补上回放，要么列为缺口并写明理由。
- `EchoConformanceTest` 用 submodule 里的黑盒运行器跑示例插件的全部用例，运行器与锁定的规范是同一个提交；CI 在 Linux 与 Windows 上都跑。
- 自己的插件用上文「写一个插件」里的命令验证。

## 破坏性改动与迁移

行为上对齐规范与 .NET 的变化，在上文各条以「（上一版…）」注明。需要使用者改代码的有：

- 上一版的 `findResponsePayload`（给出载荷）已撤回：改用 `Operations.findResponse(context)`，它给出 handle 阶段执行成功的响应对象，XGI 入口在管道结束之后才把响应编码成载荷。
- 追踪的 `traceFlags()` 已撤回：只保留采样位，写出只写 `00` / `01`。
- `PluginOptions.Builder.shutdownGrace` 已删除：收尾时限由规范定为 3 秒。
- `withBaggage` 给已登记的键一个不合的值时抛 `IllegalArgumentException`（上一版是去掉这个键）。
- `Error` 的文案为空或只有空白都不合法：`Error.literal`、`withText` 与 `FrameworkException` 的说明不合时抛 `IllegalArgumentException`，空白按 RFC-0001「空白字符」的 25 个码位判断。
- 清单读取失败的错误码都是 `KERNEL_CAPABILITY_006` 加 `kernel.manifest.*` 的消息键（上一版是 `PRIMITIVES_VALIDATION_001` 加英文消息）；`ManifestJson.readFile` 在文件不存在时抛带 `kernel.manifest.file_missing`（参数 `path`）的 `FrameworkException`（上一版抛 `IOException`）。
- `kernel.operation.type_mismatch` 的参数只有 `id`（上一版带 Java 的类型名 `expected` / `actual`）。
- 插件钩子抛出时 `reason` 是异常的简单类型名（上一版是带包名的类名或异常消息）；`PluginConfiguration.decode` 的成员名只按 ASCII 不区分大小写（上一版用 Jackson 的 `ACCEPT_CASE_INSENSITIVE_PROPERTIES`）。
