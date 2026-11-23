# TypeScript SDK

[framework-sdk-typescript](https://github.com/XiHanFun/framework-sdk-typescript) 是 [XiHan Spec](/appendix/spec) 的 TypeScript 实现：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。规范以 submodule 放在 SDK 仓库的 `spec/`，一致性由规范仓库的向量与黑盒运行器验证；参考实现是 .NET（[XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)），规范写死的行为与 .NET 宿主相同。宿主一侧怎样加载插件见[进程外插件](/plugins/out-of-process)。

## 安装

```bash
pnpm add xihan-framework
pnpm add @grpc/grpc-js @bufbuild/protobuf   # 只在写插件时需要
```

目前只有预发布版，正式版之前公开 API 还可能调整；版本列表见 [Releases](https://github.com/XiHanFun/framework-sdk-typescript/releases)。需要的 Node 版本见 `package.json` 的 `engines`。

## 模块

整个 SDK 是一个 npm 包 `xihan-framework`，各模块按子路径导入（`xihan-framework/operation`），没有根入口，全部是 ESM。核心五块运行时零第三方依赖，只用语言与平台内建的能力（`AbortSignal`、Web Crypto、`TextDecoder`、`Intl`），除 `xihan-framework/manifest/node` 外不引用 Node 专有模块，浏览器里也能用。`xihan-framework/plugin` 只用于 Node，依赖的 `@grpc/grpc-js` 与 `@bufbuild/protobuf` 是可选的 peerDependencies，其余子路径不加载它们。

| 子路径 | 内容 | 规范 |
| --- | --- | --- |
| `xihan-framework/primitives` | `ErrorInfo` / 错误码、`Result<T>`、`Maybe<T>`、`TypedId<Tag, V>`、`LifecycleState`、`PipelineStage`、`Stability`、Baggage 键、消息键与模板、`MessageCatalog`、`FrameworkError`，JSON 文本的读写（`readJsonText`、`writeJson`） | RFC-0001、RFC-0010、`conventions/` |
| `xihan-framework/manifest` | `xihan.manifest.json` 的模型、按 Schema 读取、规范化输出与校验，能力声明与依赖文本；读取文件在 `xihan-framework/manifest/node` | RFC-0002、`manifest/manifest.schema.json` |
| `xihan-framework/invocation` | `InvocationContext`：取消与截止时间（`AbortSignal`）、W3C 追踪、已登记的 Baggage、类型化对象袋 | RFC-0003 |
| `xihan-framework/pipeline` | 十一阶段的 `PipelineBuilder`：`use` / `useAt` / `useNamedInstance`、`before` / `after`、`describe` / `dump`、`build` | RFC-0004 |
| `xihan-framework/operation` | `defineContract`、`RequestShape`、`OperationRegistry`、经完整管道执行的 `OperationInvoker`（XGI：JSON 请求与响应）与类型化入口 `invokeTyped` | RFC-0011 |
| `xihan-framework/plugin` | 进程外插件运行时 `servePlugin`（协议 `xihan.plugin.v1`）、宿主回调客户端 `findHost`、类型化读取配置的 `ConfigurationSection`、stderr 日志 `PluginLogger`、消息目录 `loadCatalog` | RFC-0017、`plugin/` |

## 快速开始

```ts
import { InvocationContext } from 'xihan-framework/invocation';
import {
  defineContract,
  Expose,
  OperationInvoker,
  OperationRegistry,
  RequestShape,
  type RequestShapeType,
} from 'xihan-framework/operation';
import { middleware, PipelineBuilder, Placement } from 'xihan-framework/pipeline';
import { BaggageKey, ErrorInfo, PipelineStage, Result, writeJson } from 'xihan-framework/primitives';

// 请求的形状：调用器按它在文本顺序里核对载荷并构建请求（RFC-0011 规则 2）
const CreateOrder = RequestShape.object({ quantity: RequestShape.required(RequestShape.int32()) });
type CreateOrder = NonNullable<RequestShapeType<typeof CreateOrder>>; // { quantity: number }
interface OrderCreated { readonly id: string; readonly quantity: number }

// 契约带请求与响应类型；运行期只有 ID、稳定性、暴露的协议与授权要求
const createOrder = defineContract<CreateOrder, OrderCreated>({ id: 'orders.create', expose: [Expose.Http] });

const registry = new OperationRegistry();
registry.add({
  contract: createOrder,
  request: CreateOrder,
  handler: async (context, request) =>
    request.quantity > 0
      ? Result.ok({ id: `o-${context.tenant}`, quantity: request.quantity })
      : Result.fail(ErrorInfo.validation('ORDERS_VALIDATION_001', 'orders.quantity.invalid', { min: 1 })),
});

const builder = new PipelineBuilder().useAt(
  middleware('Audit', async (context, next) => next(context)),
  PipelineStage.Observe,
  Placement.End,
);
const invoker = new OperationInvoker(registry, builder);

const context = InvocationContext.create({ timeout: 2000, baggage: { [BaggageKey.Tenant]: 't1' } });
const response = await invoker.invoke(context, { id: 'orders.create', payload: '{"quantity":2}' });
// response.outcome.ok === true，response.payload === '{"id":"o-t1","quantity":2}'
const typed = await invoker.invokeTyped(context, createOrder, { quantity: 3 }); // Result<OrderCreated>
const wire = writeJson(typed); // 线上编码：数字按值模型写，不用 JSON.stringify
```

1. 可恢复失败经 `Result.fail(ErrorInfo…)` 返回，不抛异常；错误码按 `<领域大写>_<KIND>_<NNN>`，文案用消息键 + 参数。`ErrorInfo.message` 先经 `setDefaultMessages` 设置的目录，再找 SDK 自带的内核错误英文模板（`builtInMessages`），都解析不了时是「键 (参数)」。
2. 编程错误（参数非法、管道登记冲突、操作重复登记）抛 `FrameworkError`，带 `code`，有登记键的还带 `messageKey` 与 `details`；自己的参数校验用 `FrameworkError.invalidArgument(message)`。`result.valueOrThrow()` 把失败抛成带同样码、键、参数的 `FrameworkError`（`FrameworkError.fromErrorInfo`）。
3. 处理器与事件处理器可以返回 `Result`，也可以返回 `Promise<Result>`；中间件、步与调用器返回 `Promise`。登记表缺省容量 10000，`new OperationRegistry({ capacity })` 可以改。
4. 取消与截止时间是 `context.signal`，可以直接传给 `fetch`；调用器不与信号赛跑，处理器自己看信号后返回。`context.withSignal(signal)` 派生一个多跟一个取消信号的上下文，原来的取消、截止时间、追踪、Baggage 与对象袋照旧。
5. 线上文本用 `writeJson` 写，用 `Result.parse` / `Result.parseMaybe` / `ErrorInfo.parse` 读；`fromJSON` 系列收已解析的值，看不出重复成员，也分不出 `2` 与 `2.0`。

## 写一个插件

这一节是插件一侧用 TypeScript 的写法；宿主怎样发现、启动与降级插件，见[进程外插件](/plugins/out-of-process)。插件是宿主启动的子进程，工作目录就是插件目录：`servePlugin` 读工作目录里的清单与宿主下发的环境变量，监听本机端点，向 stdout 写握手行，然后服务到 `Stop`、stdin 关闭或 SIGTERM。

```ts
import { defineContract, Expose, OperationRegistry, RequestShape } from 'xihan-framework/operation';
import { findHost, HealthStatus, servePlugin } from 'xihan-framework/plugin';
import { ErrorInfo, LifecycleState, Result } from 'xihan-framework/primitives';

const registry = new OperationRegistry();
registry.add({
  contract: defineContract<{ text: string | null }, { text: string }>({ id: 'echoes.create', expose: [Expose.Http] }),
  request: RequestShape.object({ text: RequestShape.required(RequestShape.string()) }),
  handler: async (context, request) =>
    request?.text
      ? Result.ok({ text: request.text })
      : Result.fail(ErrorInfo.validation('ECHOTS_VALIDATION_002', 'echots.text.missing', { operation: 'echoes.create' })),
});

await servePlugin({
  registry, // Invoke 经调用器与完整管道执行；操作 ID 都要写进清单 contributes.operations
  subscriptions: [{ topic: 'echo.events', handler: async (context, envelope, delivery) => Result.success() }],
  catalogs: [{ name: 'echots', locales: { 'en-US': { 'echots.text.missing': 'The text of {operation} is required.' } } }],
  hooks: {
    // configuration.section 是清单 contributes.config.section 节；configuration.values.number('retries') 等按类型读
    configure: (configuration, { signal }) => Result.success(),
    health: () => ({ state: LifecycleState.Ready, items: [{ name: 'echo', status: HealthStatus.Healthy }] }),
  },
});
// 处理器里回调宿主：await findHost(context)?.invoke(context, 'orders.get', '{"id":"o-1"}')
```

清单 `xihan.manifest.json` 放在插件目录里：

```json
{
  "$schema": "https://spec.xihanfun.com/manifest/v1.json",
  "id": "xihan.samples.echo-ts",
  "version": "0.1.0",
  "primitives": "1",
  "stability": "experimental",
  "kind": "plugin",
  "isolation": "out-of-process",
  "contributes": { "operations": ["echoes.create"], "health": ["echo"] },
  "activation": ["onStartup"],
  "entry": "echo"
}
```

1. 清单与启动前的核对：`kind` 为 `plugin`、`isolation` 为 `out-of-process`，`primitives` 为 `1`，不写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）；操作写进 `contributes.operations`，健康项写进 `contributes.health`，配置节写 `contributes.config.section`。`servePlugin` 在监听之前核对这些，以及订阅与消息目录（每份都要有 `en-US`），不合即在 stderr 记一条 `error` 并抛出 `FrameworkError`，不写握手行。清单按严格的 UTF-8 读；省略 `manifest` 选项而工作目录里没有文件时抛 `KERNEL_CAPABILITY_006` + `kernel.manifest.file_missing`。
2. 启动脚本：`entry` 是相对插件目录的可执行文件。Unix 上是带 shebang、可执行的 sh 脚本，Windows 上是 `.cmd`（开头写 `@echo off`，`entry` 写明 `echo.cmd`，没有扩展名时宿主补的是 `.exe`）。宿主只继承少数环境变量，脚本里写出 node 与入口的绝对路径，不依赖 `NODE_PATH`。
3. 订阅：至少一次投递，处理器必须幂等。`mode` 选订阅方式（`SubscriptionMode`，缺省竞争消费），同一（主题，类型）只能声明一次；处理器的第三个参数给出这次投递的次数与方式。
4. 配置与生命周期：`hooks.configure(configuration, { signal })` 与 `hooks.start({ signal })` 的信号在宿主取消这次调用或运行时开始收尾时中止，只是通知，运行时照常等回调返回。`configuration.section` 是原样的配置节，`configuration.values`（`ConfigurationSection`）按类型读：成员名按 ASCII 不区分大小写，`number` 把字符串叶子按 JSON 数字的文法读成数字，`boolean` 只认 `true` / `false`，读不成时是 `undefined`。钩子与健康回调抛出时，交给宿主的 `reason` 只是异常的类型名（`TypeError`、作者自己的类名），文本只写进 stderr。
5. 回调宿主：`findHost(context)` 取 `Host`，`invoke` 经宿主的完整管道，`publish` 是批量、逐项结果。清单 `permissions` 要声明 `capability:kernel.pipeline`（`Host.Invoke`）或 `capability:messaging.queue`（`Host.Publish`）并由宿主授予；权限、租户与截止时间在发出之前先在本地核对，不合不发请求。回调端点取 `XIHAN_PLUGIN_HOST_ADDRESS` 里第一个 `@grpc/grpc-js` 能连的项：Windows 上是命名管道与回环 TCP，其他系统是 Unix 域套接字与回环 TCP；一个都没有时插件照常服务，回调以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable` 失败。
6. 监听端点：Linux 与 macOS 上是 Unix 域套接字，放在新建的私有目录里（目录 `0700`、套接字 `0600`，临时目录的路径含空白时改放 `/tmp`，仍不行就用回环 TCP）；Windows 上是命名管道 `\\.\pipe\xihan-plugin-<随机>`；`network: 'tcp'` 只绑定 `127.0.0.1`。Node 的 libuv 既不能给命名管道设访问控制表，也不能拒绝远程客户端，管道用 Windows 的缺省安全描述符，隔离与回环 TCP 一样靠会话令牌；只接受本机连接的改用 `network: 'tcp'`。
7. 日志与退出：握手行之后不要向 stdout 写任何东西，日志用 `PluginLogger` 写 stderr。`Stop`、stdin 关闭与 SIGTERM 按规范的时限收尾后以退出码 0 退出；`signal` 选项中止时同样收尾。运行时收尾之后作者的代码仍让进程活着时，1 秒后退出；在同一个进程里起插件的测试用 `exit` 选项接住。

用规范仓库的黑盒运行器验证（需要 Go，在规范子模块里运行）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --config '{"greeting":"hello"}' \
  --sample 'echoes.create={"text":"hi"}' --exclude echoes.crash
```

完整的示例在 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/examples/echo)：四个操作（含故意崩溃、验证时排除的 `echoes.crash`）、一个订阅、健康项、配置与宿主回调。[`test/plugin-conformance.test.ts`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/test/plugin-conformance.test.ts) 把它放进临时目录、生成启动脚本，用运行器跑全部用例。

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

TypeScript 的写法与差异：

- **JSON 读写**：读用 `readJsonText`（共同检查，读出保留文档顺序的 `JsonMembers`），写用 `writeJson`。`JSON.parse` 看不出重复成员，`JSON.stringify` 写不出 `bigint`、把 `2.0` 写成 `2`、把 `NaN` 写成 `null`，线上编码不用它们。
- **`Result` 与 `ErrorInfo`**：成功值不能是 `null` 或 `undefined`，值是 `Maybe` 时用 `Result.parseMaybe` / `Result.fromJSONMaybe`（`null` 即无值）。`ErrorInfo.literal` 与 `withText` 的文案为空或只有空白时抛 `FrameworkError`。`result.toJSON()` 给出 `{ ok, value }` 或 `{ ok, error }` 的普通对象。
- **空白**：按 RFC-0001 写死的 25 个码位逐个比较，不用 JavaScript 的 `\s` 与 `trim`（它们不认 U+0085、却认 U+FEFF）。
- **模板**：参数值为 `null` 写空串、不算缺值，`undefined` 算缺值。`formatMessage`、`tryFormatMessage` 与 `formatValue` 可带区域，`MessageCatalog` 按命中的区域取小数点与分组符。导入 `xihan-framework/plugin` 之后，英文回退也覆盖运行时的 `plugins.*` 键。
- **调用器**：三个内置步骤的名字与归属在 `BuiltInStep`、`KernelOwner`。排在 `ObserveMiddleware` 之前的中间件抛出的异常原样从 `invoke` 抛给调用方，之后的翻译成 `KERNEL_UNEXPECTED_001`。中间件经 `findOperationResponse` 看编码之前的响应；响应里有非有限的浮点或循环引用时调用以未处理失败结束，不抛给调用方。
- **清单**：`readManifest` 返回 `Result<Manifest>`、不跳过 BOM；`xihan-framework/manifest/node` 的 `readManifestFile` 跳过开头的 BOM，`readManifestSource` / `readManifestBytes` 另返回原文，都按严格的 UTF-8 读。读出的 `Manifest` 补齐缺省值，`$schema` 不在模型里；`writeManifest` 写出后按 `readManifest` 再读一遍，读不回来即抛 `FrameworkError`。
- **文法**：规范的正则都按整串匹配。JavaScript 的 `$` 在没有 `m` 标志时只匹配输入的末尾，本 SDK 的正则都不带 `m`。
- **插件运行时**：JavaScript 的时钟精度是毫秒，读 `deadline` 时第三位之后的小数截断，写出时补零到七位。`healthInterval` 选项以毫秒给出健康回调的轮询间隔（缺省 10 秒）。`parseHandshakeLine`、`parseEndpoint`、`parseHostAddresses`、`selectHostEndpoint` 是握手行与端点文法的公开入口；运行时消息键的目录是 `pluginMessages`，只用于本地渲染与日志。

## 语言映射

规范要求各 SDK 写明本语言的值怎样归到值模型、请求类型怎样声明（RFC-0010「模板」、RFC-0011 规则 2）。

- **请求的形状**：`RequestShape` 相当于 .NET 的序列化元数据。`string()` 读成 `string | null`；`boolean()`、`int32()`（`number`）、`int64()`（`bigint`）、`float64()`（`number`）不接受 `null`；`array(item)`、`record(value)`、`object(members)` 与 `json()` 接受 `null`。对象成员缺省是可选的，没有给出时不出现在结果里；`RequestShape.required(shape)` 声明必填，只要求出现，值为 `null` 也算出现。`RequestShape.custom(decode)` 相当于自定义转换器，抛出 `RequestDecodeError(path)` 时取它的路径。`RequestShapeType<typeof S>` 取出形状读出的类型。
- **数字**：安全整数的 `number`（负零除外）与 64 位范围内的 `bigint` 是整数；其余 `number`、超出 64 位的 `bigint` 与 `Float64` 是双精度浮点。`number` 分不出 `2` 与 `2.0`，值恰好是整数的浮点装进 `Float64`（`new Float64(2)`，算术照常可用）。
- **读到的 JSON 数字**：整数写法且在 64 位范围内是整数，安全整数读成 `number`，其余读成 `bigint`；浮点的值恰好是安全整数（`2.0`、`1e2`）时读成 `Float64`，否则读成 `number`。换成数值再写出会与原文不同的（`1e400`、超出 64 位的整数、多于双精度的有效数字）读成保留原文的 `JsonNumber`，任意 JSON 的成员因此写回时 JSON 值相等。
- **写出**：`writeJson` 按 `JSON.stringify` 的规则写（先调用 `toJSON`），只是数字按值模型：整数写十进制，整数值的浮点补 `.0`，`JsonNumber` 照原文；非有限值缺省写成字符串（无类型的值，如错误细节），`{ nonFinite: 'reject' }` 时写不出（类型化的值，如操作的响应）。
- **对象的顺序**：JavaScript 对象把整数样式的键（`"1"`）提到前面，要按文本顺序报错的检查用 `JsonMembers`，`toJsonValue` 转成普通对象。名为 `__proto__` 的成员是普通成员，不改原型。
- **模板里的其他值**：数组、类型化数组、`Set` 与其他可迭代对象是数组；普通对象、`JsonMembers` 与 `Map` 是对象；嵌套的 `undefined` 写 `null`；`Date` 写 `toISOString()`；类实例、symbol 与函数写 `String(value)`。这些文本不跨语言，插件要跨语言格式化时先转成字符串。

## 设计取舍

- 规范里的 `Error` 在这里叫 `ErrorInfo`，免得遮蔽全局的 `Error`；它不是异常，不 `throw`。可恢复失败经 `Result` 返回（含读不出来的清单与不合法的载荷），编程错误抛 `FrameworkError`。
- `Result<T>` 与 `Maybe<T>` 是判别联合：`if (result.ok) result.value`、`if (maybe.hasValue) maybe.value`。没有值的成功是 `Result<void>`（`Result.success()`）。
- 枚举是冻结的常量对象加同名的字面量联合类型（`PipelineStage.Observe === 'observe'`），JSON 里就是名字；需要序号时用 `lifecycleStateOrdinal` 等函数。`TypedId<'order'>` 是品牌类型，运行期就是底层的字符串或数值。
- `InvocationContext` 不可变，`withBaggage` 等方法派生新的上下文；`signal` 用 `AbortSignal.any` 合并调用方的信号与 `AbortSignal.timeout`。查找以 `find` 开头，没有时返回 `undefined`。
- 插件协议的代码由 ts-proto 从 `spec/plugin/plugin.proto` 生成在 `src/plugin/gen`，不是公开入口：`xihan-framework/plugin` 的类型（`Envelope`、`PluginHealth`、`PluginConfiguration` 等）不暴露 protobuf 类型。
- 规范列为宿主能力、语言 SDK 可以不提供的部分本 SDK 不提供：管道的运行期改动、Schema 校验、响应过滤与幂等回放还原响应（RFC-0004、RFC-0011 规则 10）。

## 一致性

- 现状：原语、清单、调用上下文、管道、操作与进程外插件运行时可用，示例插件 echo 通过规范黑盒运行器的全部用例。
- 认领的档（RFC-0024）：`core` 与 `plugin`；档的定义见规范仓库的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。
- 缺口：无。
- 钉的规范版本：SDK 仓库 `spec/` 子模块指向的提交；发版时子模块必须正好在规范标签上。
- 成熟度：Experimental。已有钉在规范 rc 标签上的预发布版，到 Stable 还要钉在规范正式版标签上的发布版。
- 尚未提供：流式操作（操作契约还没有流式形态，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束）、Wasm 载体与远程 Provider 的插件侧（如 `caching.distributed@1`）。
- 怎样验证：[`test/conformance-vectors.test.ts`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/test/conformance-vectors.test.ts) 按档表核对认领的档要求的每个向量要么被测试回放、要么列为缺口；[`test/plugin-conformance.test.ts`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/test/plugin-conformance.test.ts) 用黑盒运行器跑示例插件 echo。CI 在 Windows 与 Linux 上都跑。

## 破坏性改动与迁移

首个对外版本是 `v0.2.0-rc.1`。之后的变化：

- `PluginHooks.configure` 与 `start` 多了参数 `{ signal }` → 自己调用钩子的代码（包装钩子的装饰器）把这个参数转交下去。
- `OperationHandler` 与 `EventHandler` 的返回类型放宽为 `Result` 或 `Promise<Result>` → 自己调用处理器的代码用 `await` 接结果。
- `servePlugin` 按严格的 UTF-8 读清单，坏字节拒绝启动；省略 `manifest` 而文件不存在时抛 `FrameworkError`（`kernel.manifest.file_missing`），原先是原始的 `ENOENT` → 清单存成 UTF-8；按 `ENOENT` 判断的改看错误码。
- `ErrorInfo.create` 收到非字符串的消息键抛 `FrameworkError`，原先是 `TypeError` → 按 `TypeError` 接的改接 `FrameworkError`。
- `result.valueOrThrow()` 抛出的 `FrameworkError` 带上 `messageKey`、`details` 与 `cause` → 不用改；要取原错误的读这几个字段。
- `Maybe#toJSON()` 对 `JsonNumber` 原样交出，`JSON.stringify(Maybe.some(jsonNumber))` 写成对象而不是数字 → 线上编码用 `writeJson`。
- 导入 `xihan-framework/plugin` 之后，`ErrorInfo#message` 对 `plugins.*` 键给出英文文案，原先是「键 (参数)」→ 按文本匹配的地方改为按错误码或消息键。
- `XIHAN_PLUGIN_PROTOCOL_VERSIONS` 里不合文法的项（空项、正负号、小数、指数、十六进制、超过 uint32）现在都记 `warning`；未声明健康项的警告至多记 256 个名字 → 不用改。
- 发布的包不再带 `.js.map` / `.d.ts.map`，`sideEffects` 由 `false` 改为列出 `plugin/messages` → 不用改；调试时跳不到 `.ts` 源码。
