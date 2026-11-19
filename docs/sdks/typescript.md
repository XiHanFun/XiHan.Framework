# TypeScript SDK

TypeScript SDK 是 [XiHan Spec](https://github.com/XiHanFun/framework-spec) 的 TypeScript 实现：原语层、清单、调用上下文、管道、操作契约与进程外插件运行时。仓库是 [XiHanFun/framework-sdk-typescript](https://github.com/XiHanFun/framework-sdk-typescript)，规范以 submodule 放在仓库的 `spec/`。参考实现是 .NET（[XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework)）：规范写死的行为与 .NET 宿主相同，一致性由规范仓库的向量与黑盒运行器验证。ADR-0004 把 TypeScript 定为 Go 之后的第二个 SDK。

这一页讲安装、模块、最小示例、怎样写一个插件、与宿主共用的行为、TypeScript 的语言映射与设计取舍，以及一致性的现状。规范怎样组织、各语言 SDK 的对照见[规范与多语言](/appendix/spec)；宿主一侧怎样加载插件见[进程外插件](/plugins/out-of-process)。

## 安装

```bash
pnpm add xihan-framework
pnpm add @grpc/grpc-js @bufbuild/protobuf   # 只在写插件时需要
```

还没有发布到 npm，发布前在本仓库里使用。

## 模块

整个 SDK 是一个 npm 包 `xihan-framework`（与 crates.io、PyPI 上的包同名），各模块按子路径导入（`xihan-framework/operation`），没有根入口。全部是 ESM。核心五块运行时零第三方依赖，只用语言与平台内建的能力（`AbortSignal`、Web Crypto、`TextDecoder`、`Intl`），除 `xihan-framework/manifest/node` 外不引用 Node 专有模块，浏览器里也能用。进程外插件运行时 `xihan-framework/plugin` 只用于 Node，依赖 `@grpc/grpc-js` 与 `@bufbuild/protobuf`：它们是可选的 peerDependencies，用到插件时自己安装，其余子路径不加载它们。

| 子路径 | 内容 | 规范 |
| --- | --- | --- |
| `xihan-framework/primitives` | `ErrorInfo` / 错误码、`Result<T>`、`Maybe<T>`、`TypedId<Tag, V>`、`LifecycleState`、`PipelineStage`、`Stability`、Baggage 键、消息键与模板、`MessageCatalog`、`FrameworkError` | [RFC-0001](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0001-primitives.md)、[RFC-0010](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0010-localization.md)、[`conventions/`](https://github.com/XiHanFun/framework-spec/blob/main/conventions) |
| `xihan-framework/manifest` | `xihan.manifest.json` 的模型、按 Schema 读取、规范化输出与校验，能力声明与依赖文本；读取文件在 `xihan-framework/manifest/node` | [RFC-0002](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0002-manifest.md)、[`manifest/manifest.schema.json`](https://github.com/XiHanFun/framework-spec/blob/main/manifest/manifest.schema.json) |
| `xihan-framework/invocation` | `InvocationContext`：取消与截止时间（`AbortSignal`）、W3C 追踪、已登记的 Baggage、类型化对象袋；超时与取消翻译为 `KERNEL_TIMEOUT_001` / `KERNEL_CANCELLED_001` | [RFC-0003](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0003-invocation-context.md) |
| `xihan-framework/pipeline` | 十一阶段的 `PipelineBuilder`：`use` / `useAt` / `useNamedInstance`、`before` / `after`、`describe` / `dump`、`build`；排序规则与错误码 `KERNEL_PIPELINE_00x` | [RFC-0004](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0004-pipeline-stages.md) |
| `xihan-framework/operation` | `defineContract`、`OperationRegistry`、经完整管道执行的 `OperationInvoker`（XGI：JSON 请求与响应）与类型化入口 `invokeTyped` | [RFC-0011](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0011-operations.md) |
| `xihan-framework/plugin` | 进程外插件运行时 `servePlugin`（协议 `xihan.plugin.v1`）：握手、配置（类型化读取 `ConfigurationSection`）、启停、健康与健康推送、`Invoke` 与事件投递，宿主回调客户端 `findHost`（`Host.Invoke`、`Host.Publish`），结构化的 stderr 日志 `PluginLogger`，消息目录 `loadCatalog`；只用于 Node | [RFC-0017](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0017-plugin-protocol.md)、[`plugin/`](https://github.com/XiHanFun/framework-spec/blob/main/plugin) |

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

`Result<T>`、`ErrorInfo`、`Maybe<T>` 的 JSON 编码与 .NET 参考实现一致（`{"ok":true,"value":…}` / `{"ok":false,"error":{…}}`，没有值的成功是 `{"ok":true}`）。写出用 `writeJson`，`Result.parse` / `Result.parseMaybe` / `ErrorInfo.parse` 从文本还原（`fromJSON` 系列收已解析的值，看不出重复成员，也分不出 `2` 与 `2.0`）；JSON 文本的读取检查、线上编码、模板格式化、内核错误、清单的读写、载荷的解码、管道排序、生命周期迁移表、错误码、Baggage 键与值、`traceparent`、能力依赖的文法都由 `spec/conformance/vectors` 验证。

## 写一个插件

插件是宿主启动的子进程：清单 `xihan.manifest.json`、启动入口与插件代码放在插件目录里，宿主以插件目录为工作目录启动清单的 `entry`。`servePlugin` 读工作目录里的清单与宿主下发的环境变量、监听本机端点、向 stdout 写握手行，然后服务到 `Stop`、stdin 关闭或 SIGTERM。宿主一侧怎样发现、启动、调用插件以及失败时怎样降级见[进程外插件](/plugins/out-of-process)。

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
  subscriptions: [{ topic: 'echo.events', handler: async (context, envelope) => Result.success() }],
  catalogs: [{ name: 'echots', locales: { 'en-US': { 'echots.text.missing': 'The text of {operation} is required.' } } }],
  hooks: {
    // configuration.section 是清单 contributes.config.section 节；values.number('retries') 等按 IConfiguration 的习惯读
    configure: (configuration) => Result.success(),
    health: () => ({ state: LifecycleState.Ready, items: [{ name: 'echo', status: HealthStatus.Healthy }] }),
  },
});
// 处理器里回调宿主：await findHost(context)?.invoke(context, 'orders.get', '{"id":"o-1"}')
```

清单的要点：

- `kind` 为 `plugin`、`isolation` 为 `out-of-process`，`primitives` 为 `1`，不能写 `contributes.pipeline`（只有 Wasm 插件能贡献中间件）。声明与清单不符时 `servePlugin` 在监听之前在 stderr 记一条 `error` 日志并抛出，不写握手行。
- `entry` 是相对插件目录的可执行文件，Windows 上没有扩展名时宿主补 `.exe`。Node 插件用启动脚本：Unix 上是带 shebang、可执行的 sh 脚本，Windows 上是 `.cmd`（开头写 `@echo off`，命令不回显到 stdout）。宿主只继承少数环境变量（handshake.md §1），脚本里写出 node 与入口的绝对路径，不依赖 `NODE_PATH` 之类。
- 操作写进 `contributes.operations`，健康项写进 `contributes.health`，配置节写 `contributes.config.section`；回调宿主要在 `permissions` 里声明 `capability:kernel.pipeline`（`Host.Invoke`）或 `capability:messaging.queue`（`Host.Publish`）。
- 握手行之后不要向 stdout 写任何东西，日志用 `PluginLogger` 写 stderr。

插件自己的端点按平台：Linux 与 macOS 上是 Unix 域套接字，放在本进程新建的私有目录里（`mkdtemp`，目录 `0700`、套接字 `0600`，停止后删除；临时目录的路径含空白时改放 `/tmp`，仍不行就用回环 TCP，因为握手行的地址不能含空白）；Windows 上是命名管道 `\\.\pipe\xihan-plugin-<随机>`（Node 在 Windows 上没有 Unix 域套接字）；`network: 'tcp'` 时只绑定 `127.0.0.1`，握手行写 `127.0.0.1:<端口>`。

命名管道的访问控制（handshake.md §4）：Node 的 libuv 两样都给不了，既不能给管道设访问控制表，也不能拒绝远程客户端（不设 `PIPE_REJECT_REMOTE_CLIENTS`）。管道因此用 Windows 的缺省安全描述符：当前用户、管理员与 SYSTEM 可读写，其他用户只能只读打开、发不出请求，隔离与回环 TCP 一样靠会话令牌。需要只接受本机连接的改用回环 TCP（`network: 'tcp'`）。

回调宿主的端点（handshake.md §4）：`XIHAN_PLUGIN_HOST_ADDRESS` 是宿主按偏好排序、以 `;` 分隔的一个或多个端点，最后一项总是回环 TCP。本 SDK 取第一个 `@grpc/grpc-js` 能连的项，之后不换：实测 Windows 上能连命名管道与回环 TCP、连不上 Unix 域套接字，其他系统上能连 Unix 域套接字与回环 TCP。所以 Windows 上宿主给出命名管道时经命名管道回调，排在前面的是本 SDK 连不上的项时经回环 TCP 回调。变量缺失、不合法或没有能连的项时插件照常服务，启动时在 stderr 记一条 `warning`，每次回调都以 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `no supported callback endpoint`）失败、不发请求；`findHost` 在插件运行时交给处理器的上下文里总能取到客户端。

流式操作的契约还没有定义（RFC-0017「非目标」），插件只声明一元操作，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束。收尾的时限：`Stop` 按宿主给的宽限（为 0 时 5 秒）等在途调用；stdin 关闭与 SIGTERM 立即取消在途调用，最多 3 秒后以退出码 0 退出（lifecycle.md §7.5、§7.7）。

用规范仓库的黑盒运行器验证（需要 Go，在规范子模块里运行）：

```bash
cd spec/conformance
go run ./cmd/xihan-conformance plugin --dir <插件目录> --config '{"greeting":"hello"}' \
  --sample 'echoes.create={"text":"hi"}' --exclude echoes.crash
```

完整的示例在 [`examples/echo`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/examples/echo)：四个操作（含故意崩溃、验证时排除的 `echoes.crash`）、一个订阅、健康项、配置与宿主回调。[`test/plugin-conformance.test.ts`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/test/plugin-conformance.test.ts) 把它放进临时目录、生成启动脚本，用运行器跑全部用例。

## 与宿主共用的行为

以下行为按规范（ADR-0233、ADR-0235、ADR-0236、ADR-0237）定死，各语言 SDK 与 .NET 宿主相同。

::: v-pre
- **JSON 文本**（RFC-0001「JSON 文本」）：Result 与 Error 的线上编码、操作载荷与清单都先过 `readJsonText` 的共同检查。语法按 RFC 8259（没有注释、尾逗号、`NaN`、前导零、正号、多个根值，空文本与只有空白也不是 JSON，开头的 BOM 是语法错误），嵌套不超过 64 层，字符串必须是合法的 Unicode（严格的 UTF-8，没有原始控制字符、非法转义与孤立的代理项），任何一层都不能有重复的成员名（按转义还原后比较，`"a"` 与 `"a"` 重复）。语法错误不论在哪里都优先，位置只写 `$`；重复成员写第一处重复的路径（`$.a[0].b`）。
- **Result 与 Error 的读写**（RFC-0001「Result」）：无值成功只写 `{"ok":true}`，读取时带了 `value` 即拒绝，`{}` 与 `null` 也一样；有值成功的 `value` 必填、不能是 `null`（值类型是 Maybe 时用 `Result.fromJSONMaybe`，`null` 即无值）。Error 的 `message` 必填，至少含一个空白之外的字符，为空或只有空白都不合法；空白是 RFC-0001「空白字符」写死的 25 个码位，不用 JavaScript 的 `\s` 与 `trim`（它们不认 U+0085、却认 U+FEFF）。`ErrorInfo.literal` 与 `withText` 的文案为空或只有空白时同样抛出 `FrameworkError`。`messageKey`、`details`、`inner` 写了就必须合法，写成 `null` 不合法；未知成员忽略。数字按值模型往返：64 位范围内的整数精确（9007199254740993 不会变成 9007199254740992），整数值的浮点写成 `2.0`、`-0.0`、`1E+17`，非有限的浮点写成字符串 `"NaN"`、`"Infinity"`、`"-Infinity"`。
- **模板**（RFC-0010「模板」）：`{{` / `}}` 转义；参数值为 `null` 写空串、不算缺值，`undefined` 算缺值。值先归到值模型（空、布尔、字符串、64 位整数、双精度浮点、数组、对象）：数组与对象写成紧凑的 JSON，对象成员按名字的 UTF-16 码元序，嵌套的数字不随区域变化，超过 64 层的容器写 `null`。说明符只认 `^[DdFfNnXx][0-9]{0,2}$`（`D`、`F`、`N` 不区分大小写，`X` / `x` 定十六进制的大小写，精度至多两位、可有前导零），`E2`、`G`、`0.00`、`yyyy`、`D100`、带空格的一律按无格式；`D` 与 `X` 只用于整数，`X` 只用于非负整数，`F` 与 `N` 用于整数与浮点，不适用时按无格式。`F` 与 `N` 按值的精确十进制展开舍入、正好一半时取偶（0.125 写 `F2` 得 `0.12`，2.675 得 `2.67`），写出全部整数位，负数舍入成零与负零都保留负号。浮点在 1e-4 ≤ |x| < 1e17 内写最短往返的十进制，范围外写 `1E+17`、`9.9E-05`、`5E-324`，负零写 `-0`，另有 `NaN`、`Infinity`、`-Infinity`。区域只改小数点与分组符，取规范的固定表（de-DE、es-ES、pt-BR 是 `,` 与 `.`，fr-FR 是 `,` 与 U+202F，ru-RU 是 `,` 与 U+00A0，其余与不给区域按不变文化），区域名不区分大小写；小数点用在无格式的浮点（含指数的尾数）与 `F`、`N` 上，分组符只用在 `N` 上。`formatMessage`、`tryFormatMessage` 与 `formatValue` 带区域，`MessageCatalog` 按命中的区域取符号。`Describe` 的参数按名字的序数顺序排列，与区域无关。消息键最长 128 个字符。
- **内核错误**（RFC-0011「错误码」）：码、消息键、参数与 en-US 模板照 `kernel-messages.json`，SDK 自带这组英文模板（`builtInMessages`，另有清单读取的 `kernel.manifest.*` 与 `primitives.manifest.*`），`ErrorInfo.message` 在进程默认目录解析不出时用它；宿主收到错误后按键用自己的目录翻译。`kernel.operation.type_mismatch` 只带 `id`。配置期的重复登记与登记表已满以 `FrameworkError` 抛出，同样带 `messageKey` 与 `details`。
- **调用器**（RFC-0011 规则 2、5、9、10）：进入管道前只核对未登记（`KERNEL_NOTFOUND_001`），类型化调用再核对契约（`KERNEL_VALIDATION_002`）。管道里有三个内置步骤，与 .NET 内核的内置中间件同名、同位置，都按归属 `xihan.kernel.core` 放在所在阶段的 `Beginning`（`BuiltInStep`、`KernelOwner`），与同位置的其他中间件按归属的序数序排序：Observe 阶段的 `ObserveMiddleware` 把排在它之后抛出的异常翻译为 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`（调用已中止时抛出的中止按取消归因），排在它之前的（Ingress 阶段，或 Observe 阶段 `Beginning` 上归属排在它之前的，如缺省的 `app`）抛出的异常原样从 `invoke` 抛给调用方；Resilience 阶段的 `DeadlineMiddleware` 进入时截止时间已到即 `KERNEL_TIMEOUT_001` + `kernel.invocation.deadline_before_start`，进入时不核对取消，途中照 .NET 归因：下游抛出的中止成为超时或取消的结果，截止信号已触发时写成结果的 `KERNEL_CANCELLED_001` 按超时报告；Validate 阶段的 `ValidationMiddleware` 解码原始调用的载荷，类型化调用跳过。管道以成功结束而没有响应时返回 `KERNEL_UNEXPECTED_001` + `kernel.operation.no_response`。XGI 入口在管道结束之后才把响应编码成载荷，中间件经 `findOperationResponse` 看到编码之前的对象；数字按值模型写，非有限的浮点（类型化的值不写成 `null` 或字符串）与循环引用写不出时以 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception` 结束，FrameworkError 放进 `inner`，不抛给调用方，没有载荷。取消与截止时间只经 `signal` 通知（RFC-0003 第 1 条）：调用器不与信号赛跑、不丢弃处理器的 Promise，信号触发之后处理器照常返回结果的，调用以那个结果结束。
- **载荷的解码**（RFC-0011 规则 2）：长度为零的载荷等价于 `{}`，只有空白不是 JSON。先过共同检查（语法错误的 `reason` 是 `$`，重复成员是它的路径），再按请求的形状在文本顺序里报第一处问题：成员名区分大小写，未知成员忽略，数字不从字符串读，整数不接受 `1.0` 与 `1e2`、超出范围即不符，浮点接受整数写法、上溢为无穷；字符串、数组、对象与任意 JSON 接受 `null`，整数、浮点、布尔不接受；必填成员缺少时在所在对象结束时报该对象的路径（`$`、`$.inner`），值为 `null` 也算出现。载荷的文本按 JSON 值比较，空白、转义与数字的写法不是契约；任意 JSON 类型的成员原样保留读到的值，写回时 JSON 值相等（`1e400` 不变成 `null` 或无穷，超出双精度的整数精确，`2.0` 与 `2` 的区别保留）。
- **调用上下文**（RFC-0003、conventions/baggage-keys.md）：已登记 Baggage 键的值为空、超过 256 个 UTF-16 码元或含控制字符时，由一组 Baggage 构造上下文（外部输入）丢弃该成员，`withBaggage` 逐项派生则抛出参数错误，幂等键两种情形都例外；`traceparent` 只认版本 `00` 与小写十六进制、全零的标识不合法，不合法的当作没给，采样只看标志的最低位，写出时只写 `00` 或 `01`，没有上游的根调用写 `01`。
- **管道**（RFC-0004 排序规则第 7、8 条）：`before` / `after` 的两端是中间件名：按名字登记的实例名，或没有名字、只登记一次的中间件的类型名（按名字登记的实例不能按类型名指到）；重复登记、未知阶段、约束主体未登记或有歧义在登记时立即报，约束目标未登记、有歧义、跨阶段与成环在排序时报。错误消息是与 .NET 逐字相同的英文原文，按名字登记的实例写成 `类型名[名字]`，成环时按基准次序列出全部仍被卡住的中间件。
- **清单**（RFC-0002 解析规则 2 与「写出」）：`readManifest` 返回 `Result<Manifest>`，错误码都是 `KERNEL_CAPABILITY_006`，按 JSON 文本（`kernel.manifest.invalid_json`，`reason` 是 `$` 或第一处重复的路径）→ 字面量 `null`（`kernel.manifest.empty`）→ 按 Schema 逐项核对（`kernel.manifest.invalid`，`reason` 是第一处问题的路径：成员按文本顺序，每个成员依次查 Schema 之外的成员、`null`、类型、取值，对象的成员查完后按 `required` 的顺序报缺少的成员）→ 跨字段约束（`primitives.manifest.*`，参数只有 `id`）的顺序报第一处。接口版本不带前导零、不超过 2147483647，只有 `requires[].version` 可带一个 `^`；中间件名至少一个字符，`contributes.health` 符合 `^[a-z][a-z0-9.-]*$`，贡献的权限码重复时报第二次出现的项。`readManifestFile` 跳过开头的 UTF-8 BOM，文件不存在为 `kernel.manifest.file_missing`（参数 `path`）；`readManifest` 收的文本不跳过 BOM。`writeManifest` 的排版逐字照规范：两个空格缩进、LF、末尾不换行、列表总写、缺省值写出、字符串只转义必须转义的字符。只有 Wasm 插件能贡献中间件由宿主在发现时核对，读清单时不核对。
- **文法的匹配**（RFC-0001「文法的匹配」）：消息键、错误码、Baggage 键、清单与能力 ID、操作 ID、`traceparent` 等文法都按整串匹配，结尾的换行不例外。JavaScript 的 `$` 在没有 `m` 标志时只匹配输入的末尾，本 SDK 的正则都不带 `m`，所以天然是整串匹配。
:::

### 插件协议

插件一侧的行为按 RFC-0017、规范的 `plugin/` 与 ADR-0237：

- **会话令牌**：每个 RPC（含 Handshake 与流式调用）先按常数时间核对元数据 `xihan-plugin-session`，没有、不匹配或不止一个值（即使其中一个对）都以 `Unauthenticated` 结束、不执行；令牌先于一切处理，含流式调用读第一帧与调用次序的核对（handshake.md §4，ADR-0202）。
- **启动**：监听与写握手行之前核对清单与声明（handshake.md §1）：`kind` 为 `plugin`、`isolation` 为 `out-of-process`、没有 `contributes.pipeline`、`primitives` 等于本 SDK 的原语层大版本 `1`、操作都在 `contributes.operations` 里（登记表本身不收重复的 ID）、订阅的主题合文法、方式为空、`competing` 或 `broadcast` 且（主题，类型）不重复、目录有 `en-US`。不合即在 stderr 记一条 `error` 日志并以 `FrameworkError` 抛出，不监听、不写握手行。健康项由健康回调在运行期给出，清单 `contributes.health` 之外的在上报时丢掉（见下）。
- **握手行与端点**（handshake.md §2）：地址的空白是 RFC-0001「空白字符」，即 .NET 正则 `\s` 的集合（U+0009–U+000D、U+0020、U+0085、U+00A0、U+1680、U+2000–U+200A、U+2028、U+2029、U+202F、U+205F、U+3000；JavaScript 的 `\s` 不含 U+0085、却含 U+FEFF，所以逐个码元比较），其余字符（含中文路径）都可以出现在地址里；`tcp` 只认 `127.0.0.0/8` 里四段、不带前导零的点分十进制或 `[::1]`，端口 1–65535、不带正负号与前导零。本 SDK 自己生成的地址不含空白，`tcp` 只写 `127.0.0.1:<端口>`。`parseHandshakeLine`、`parseEndpoint`、`parseHostAddresses`、`selectHostEndpoint` 回放 `handshake-lines.json` 与 `host-addresses.json`。
- **握手**（handshake.md §3）：选宿主列表里最高的共同版本，没有即 `FailedPrecondition`；`primitives` 写清单的值，`manifest` 是清单原文去掉开头的 BOM，只声明一元操作（带 Schema），订阅按声明顺序、`mode` 总写 `competing` 或 `broadcast`，`catalogs` 只有作者自己的目录：运行时的 `plugins.*` 由宿主提供，不随握手发送。
- **调用次序**（lifecycle.md §7.1）：`Handshake` 只在 Declared、`Configure` 只在 Resolved、`Start` 只在 Configured 接受，`Stop` 在 Declared、Draining、Stopped 与已经接受过一次 `Stop` 时拒绝；不在表里的以 `FailedPrecondition` 拒绝、什么都不做、状态不变。`Health`、`WatchHealth`、`Invoke`、`InvokeStream`、`HandleEvent` 任何状态都接受。
- **配置**（§7.2）：`configuration` 为空等同 `{}`；通不过共同的 JSON 读取检查或不是对象时不调配置回调，以 `PLUGINS_UNEXPECTED_001` + `plugins.process.failed`（`phase` 为 `configure`，`reason` 为 `the configuration is not a JSON object`）失败、进入 Failed。`PluginConfiguration.section` 是原样的配置节，`values` 是类型化读取 `ConfigurationSection`：成员名按 ASCII 不区分大小写匹配，`number` 把字符串叶子按 JSON 数字的文法读成数字，`boolean` 只认 `true`、`false`（ASCII 不区分大小写），读不成时为 `undefined`。配置回调返回失败带那个错误进 Failed。
- **启动与就绪**（§7.3）：启动回调成功后进入 Started，有健康回调时立即求一次、按结果进入 Ready、Degraded 或 Failed（报其他状态按 Ready，抛出按 Degraded），没有健康回调即 Ready，然后才应答。只有 Ready 与 Degraded 可服务，其余状态下的调用与投递为 `PLUGINS_UNAVAILABLE_001` + `plugins.process.unavailable`（`state` 是状态名，如 `Configured`、`Draining`、`Failed`）。
- **健康**（§7.4）：`Start` 之前报本地状态（Declared、Resolved、Configured）、没有健康项、不调回调；可服务期间每次 `Health` 求一次健康回调，只有它报的 Ready、Degraded、Failed 改变状态（Failed 不再回来，之后不再调回调）；清单之外的健康项丢掉，同一个名字只记一条 `warning`；回调抛出时这一次是 Degraded，`error` 为 `plugins.process.failed`（`phase` 为 `health`），没有健康项，状态也迁到 Degraded，之后的结果照常决定状态。`WatchHealth` 先推当前状态，状态、健康项或错误变化时推，可服务期间每 10 秒（`healthInterval`）重新求一次回调，进入 Draining 推一次，应答 `Stop` 之后结束流。
- **停止**（§7.5）：启动回调没成功过的（Resolved、Configured，或在那之前进入 Failed）不调停止回调，应答 `ok`；Started、Ready、Degraded 进入 Draining、拒绝新调用，等在途调用到宽限期（`grace_seconds` 为 0 按 5 秒），到时触发它们的取消信号、不再等，然后调停止回调（`reason` 原样），应答，进入 Stopped；启动成功过后进入 Failed 的不迁移状态，照样调停止回调。停止回调在进程里最多调一次，返回失败或抛出时写进 `StopResponse.status`，都照常以退出码 0 退出。
- **收尾**（§7.7）：stdin 读到结尾（或出错）与 SIGTERM 都是收尾请求：可服务的进入 Draining，立即触发全部在途调用的取消信号并等它们，启动回调成功过、停止回调还没调过的调一次（`reason` 为 `shutdown`）；从收到起最多 3 秒，到时不论完成与否以退出码 0 退出，正在处理的 `Stop` 同样受这 3 秒限制。运行时收尾之后不留下自己的句柄，作者的代码仍让进程活着时 1 秒后以退出码 0 退出；在同一个进程里运行插件的测试用 `exit` 选项接住。
- **回调抛出的 `reason`**（§7.6）：生命周期与健康回调抛出时错误不带异常文本（文本只写进 stderr 日志），`reason` 是异常的简单类型名：构造函数的名字（`TypeError`、作者自己的异常类名），抛出原始值时是包装类型名（`String`、`Number`），`null` 与 `undefined` 写它们自己。
- **`Invoke`**（invocation.md §1）：会话令牌之后依次是操作不在握手声明里 gRPC `Unimplemented`、不可服务 `plugins.process.unavailable`、按 §2 还原上下文、经调用器与完整管道执行，空载荷等同 `{}`。`InvokeStream` 读第一帧：流在第一帧之前结束或第一帧不是 `start` 为 `InvalidArgument`，是 `start` 为 `Unimplemented`，不论操作是否声明、插件是否就绪；本 SDK 不提供流式操作的 API。
- **调用上下文**（invocation.md §2）：`trace_parent` 按 RFC-0003 的文法取，不合即没有父 Span；`trace_state` 不进上下文，回调时写空；Baggage 只保留已登记的键，`xihan.principal` 原样交给作者；`deadline` 按严格的文法读（年 0001–9999、月日按公历、不认闰秒，换算成 UTC 之后在范围内），读不出当作没有，生效的截止时间取它与 gRPC 截止时间中较早的。JavaScript 的时钟精度是毫秒，第三位之后的小数截断、不进位。回调写 `deadline` 用 UTC、七位小数、`+00:00`（毫秒之后补零），gRPC 调用的截止时间与它相同。回放 `deadlines.json`。
- **Error 的写出与读入**（invocation.md §3）：写出时带消息键的错误 `text` 留空，由宿主按调用方的语言翻译，作者用 `withText` 填的文案也不写；只有自由文本错误（`ErrorInfo.literal`）写 `text`；`details` 是按值模型写出的 JSON 对象文本，没有为空；`inner` 照写。读入宿主写来的错误：码不合文法时整个换成 `plugins.process.failed`（`phase` 为 `invoke`，`reason` 为 `invalid error code '<code>'`）；键合文法按键还原、不留宿主的文案，否则是自由文本（`text`，为空或只有空白时取码）；码不合文法时 `phase` 一律为 `invoke`（`Host.Publish` 的结果也一样），原来的 `inner` 丢掉；`details` 为空、只有空白、是空对象 `{}`、通不过共同的 JSON 读取检查、不是对象，或任何一层有读成双精度会溢出（±∞）的数时当作没有细节，下溢的数读成 0、超出 64 位的整数读成双精度（写回时仍是浮点），`inner` 同此；`ok` 为假却没有 `error`、或整个 `status` 缺失，为 `plugins.process.failed`（`phase` 为操作 ID 或 `publish`，`reason` 为 `the host reported a failure without an error`）。回放 `error-reading.json`。
- **`HandleEvent`**（invocation.md §4）：会话令牌 → 没有信封或方式不是空、`competing`、`broadcast` 为 gRPC `InvalidArgument` → 按（主题，方式，类型）找订阅，一个也没有为 `KERNEL_NOTFOUND_001` + `plugins.event.unsubscribed`（`id`、`topic`、`type`、`mode`，空方式写 `competing`），不改交给另一种方式的处理器 → 不可服务为 `plugins.process.unavailable` → 还原上下文，匹配的订阅按声明顺序依次执行，第一个失败即终止并作为结果；处理器抛出为 `KERNEL_UNEXPECTED_001` + `kernel.invocation.unhandled_exception`（调用已中止时抛出的中止按取消归因，FrameworkError 的错误放进 `inner`）。
- **回调**（invocation.md §5）：`Host.Invoke` 与 `Host.Publish` 发出之前按宿主的顺序在本地核对，不合即不发请求：所需权限（`capability:kernel.pipeline`、`capability:messaging.queue`）既在清单 `permissions` 里、也在 `Configure` 的 `granted_permissions` 里，否则 `PLUGINS_DENIED_002` + `plugins.callback.denied`；上下文的 `xihan.tenant` 必须是插件此刻正在处理的某个调用或投递的租户（含调用结束后在后台回调），否则 `plugins.callback.tenant_denied`（ADR-0195）；最后，回调上下文的截止时间已过或已被取消时也不发请求，结果与映射表相应的一行相同（截止时间已过按 `DeadlineExceeded`，已被取消按「`Cancelled`，取消信号已触发」）。有没有可用的端点排在这几项之前。`Host.Publish` 是批量：这两项、没有可用端点与 RPC 失败都是整批的结果（每项同一个错误），信封的 `xihantenant` 不在本地核对、交给宿主逐项判定；结果数与信封数不同时每项都是 `plugins.process.failed`（`phase` 为 `publish`）。元数据取自所在的调用上下文（上下文按 RFC-0003 写出的 traceparent，与其他载体同一取法；截止时间、已登记的 Baggage、空的 `trace_state`），带会话令牌。gRPC 状态只按 §5 的表映射、不另按时钟或令牌改写结果（回放 `callback-status.json`）：上下文到了截止时间交给 gRPC 自己的截止时间、报 `DeadlineExceeded`，只有调用方取消时才取消回调；`Unauthenticated` 为 `plugins.callback.session_invalid`，`PermissionDenied` 为 `plugins.callback.denied`，`DeadlineExceeded` 为 `KERNEL_TIMEOUT_001`，调用方已取消的 `Cancelled` 为 `KERNEL_CANCELLED_001`，没有取消的 `Cancelled`、`Unavailable` 与连接失败为 `PLUGINS_UNAVAILABLE_001` + `plugins.host.unavailable`（`reason` 为 `<状态名>: <说明>`），`Host.Invoke` 的 `Unimplemented` 与 `InvalidArgument` 为 `kernel.operation.unknown` 与 `kernel.operation.invalid_payload`，其余为 `plugins.process.failed`（`phase` 为操作 ID 或 `publish`）。
- **日志与消息键**：日志写 stderr，一行一个 JSON 对象：`level`（`trace` / `debug` / `information` / `warning` / `error` / `critical`）、`message`，其余是作用域属性（invocation.md §6）。运行时只用规范的七个消息键（invocation.md §8：`plugins.process.unavailable`、`plugins.process.failed`、`plugins.event.unsubscribed`、`plugins.host.unavailable`、`plugins.callback.denied`、`plugins.callback.tenant_denied`、`plugins.callback.session_invalid`），`details` 就是模板参数、不另加成员；目录 `pluginMessages` 带参考实现的 10 种区域，只用于插件这一侧的本地渲染与日志，回放 `plugin-messages.json`。

## 语言映射

规范要求各 SDK 写明本语言的值怎样归到值模型、请求类型怎样声明（RFC-0010「模板」、RFC-0011 规则 2）。

- **数字**：安全整数的 `number`（负零除外）与 64 位范围内的 `bigint` 是整数；其余 `number`（小数、负零、`NaN`、无穷、超出安全整数的值）、超出 64 位的 `bigint` 与 `Float64` 是双精度浮点。`number` 分不出 `2` 与 `2.0`，所以值恰好是整数的浮点装进 `Float64`（`new Float64(2)`，`Number(v)` 与算术照常可用）。
- **读到的 JSON 数字**：整数写法（没有小数点与指数）且在 64 位范围内是整数，安全整数读成 `number`，其余读成 `bigint`，`-0` 读成整数 `0`；其余是浮点，值恰好是安全整数（`2.0`、`1e2`、`0.0`）时读成 `Float64`，否则读成 `number`（`-0.0` 是负零）。换成数值再写出时十进制值与原文不同的（`1e400`、`1e-400`、超出 64 位的整数、多于双精度的有效数字）读成保留原文的 `JsonNumber`：值模型里是浮点（`Number(v)` 上溢为无穷、下溢为 0），写出时照原文，所以任意 JSON 的成员写回时 JSON 值相等。
- **写出**：`writeJson` 按 `JSON.stringify` 的规则写（先调用 `toJSON`，写不出时同样抛 `TypeError`），只是数字按值模型：整数写十进制，浮点写文本形式并给整数值补 `.0`，`JsonNumber` 照原文（缺省模式下上溢的按值模型写成 `"Infinity"`）；非有限值缺省写成字符串（无类型的值，如错误细节），`{ nonFinite: 'reject' }` 时写不出（类型化的值，如操作的响应）。`JSON.stringify` 写不出 `bigint`，也会把 `2.0` 写成 `2`、把 `NaN` 写成 `null`，线上编码不要用它。
- **对象的顺序**：JavaScript 对象把整数样式的键（`"1"`）提到前面。要按文本顺序报错的检查（载荷、清单）用 `readJsonText` 读出的 `JsonMembers`，它保留文档顺序；`toJsonValue` 转成普通对象。名为 `__proto__` 的成员是普通成员，不改原型。
- **模板里的其他值**：字符串原样；数组、类型化数组、`Set` 与其他可迭代对象是数组；普通对象、`JsonMembers` 与 `Map` 是对象（`Map` 的键按文本形式写成名字）；嵌套的 `undefined` 写 `null`；`Date` 写 `toISOString()`；类实例、symbol 与函数写 `String(value)`。这些语言自己的文本不跨语言，插件要跨语言格式化时先转成字符串。
- **请求的形状**：`RequestShape` 相当于 .NET 的序列化元数据。`string()` 读成 `string | null`（接受 `null`）；`boolean()`、`int32()`（`number`）、`int64()`（`bigint`）、`float64()`（`number`）不接受 `null`；`array(item)`、`record(value)`、`object(members)` 与 `json()` 接受 `null`。对象成员缺省是可选的，没有给出时不出现在结果里；`RequestShape.required(shape)` 声明必填，只要求出现，值为 `null` 也算出现。`RequestShape.custom(decode)` 相当于自定义转换器：形状核对跳过它，构建时转换，抛出 `RequestDecodeError(path)` 时取它的路径（用 `JsonPath` 从给出的位置往下拼）。`RequestShapeType<typeof S>` 取出形状读出的类型。
- **清单的模型**：`readManifest` 读出的 `Manifest` 补齐缺省值（`role`、`activation`、各列表、`requires[].optional`、`bindings[].required`），与 .NET 的 `Manifest` 一致；`$schema` 不在模型里，写出时总写 `manifestSchemaUri`。程序里构造的清单可以不写可选成员，`writeManifest` 写出后按 `readManifest` 再读一遍核对，读不回来即抛出带同样码、键与参数的 `FrameworkError`。

## 设计取舍

- 规范里的 `Error` 在这里叫 `ErrorInfo`，免得遮蔽全局的 `Error`；它不是异常，不 `throw`。可恢复失败经 `Result` 返回（含读不出来的清单与不合法的载荷），参数非法、程序里构造的清单写不出、管道登记冲突这类编程错误抛出 `FrameworkError`（带 `code`，消息是英文句子）。
- `Result<T>` 与 `Maybe<T>` 是判别联合：`if (result.ok) result.value`、`if (maybe.hasValue) maybe.value`。成功值与 `Some` 的值不能为 `null` 或 `undefined`，没有值的成功是 `Result<void>`（`Result.success()`）。
- 枚举是冻结的常量对象加同名的字面量联合类型（`PipelineStage.Observe === 'observe'`），JSON 里就是名字；需要序号时用 `lifecycleStateOrdinal` 等函数。
- `TypedId<'order'>` 是品牌类型，运行期就是底层的字符串或数值，标签不上线。
- `InvocationContext` 不可变，`withBaggage` 等方法派生新的上下文；取消与截止时间是 `signal`（`AbortSignal.any` 合并调用方的信号与 `AbortSignal.timeout`），可以直接传给 `fetch` 等接口。截止信号已触发即报超时，调用方同时取消也一样（RFC-0003 第 1 条）。
- 中间件、步、处理器与调用器都返回 `Promise`。查找以 `find` 开头，没有时返回 `undefined`。
- 插件协议的代码由 ts-proto 从 `spec/plugin/plugin.proto` 生成在 `src/plugin/gen`：消息的编解码用 `@bufbuild/protobuf` 的 wire 运行时，服务定义直接给 `@grpc/grpc-js`，枚举生成冻结的常量对象，Node 照样直接剥离类型运行。生成代码不是公开入口，`xihan-framework/plugin` 的类型（`Envelope`、`PluginHealth`、`PluginConfiguration` 等）不暴露 protobuf 类型。生命周期回调与事件处理器返回 `Result`，抛出的异常只把文本写进日志、不写上线：生命周期与健康回调按 `plugins.process.failed`（`reason` 是简单类型名）返回，事件处理器按未处理异常返回。

## 一致性

一致性档按 [RFC-0024](https://github.com/XiHanFun/framework-spec/blob/main/rfcs/0024-versions-and-profiles.md) 划分，档表是规范的 [`conformance/profiles.json`](https://github.com/XiHanFun/framework-spec/blob/main/conformance/profiles.json)。

| 项 | 现状 |
| --- | --- |
| 认领的档 | `core` 与 `plugin` |
| 缺口 | 无 |
| 钉的规范版本 | 仓库 `spec/` 子模块指向的提交；发预发布或正式版时子模块必须正好在规范标签上 |
| 成熟度 | Experimental：在有钉在规范标签上的发布版之前都是 |

原语、清单、调用上下文、管道与操作五个模块可用；进程外插件运行时 `plugin`（`xihan.plugin.v1`，基于 `@grpc/grpc-js`）可用，示例插件 echo 通过规范仓库黑盒运行器 `xihan-conformance plugin` 的全部用例（Windows 与 Linux）。

怎样验证：[`test/conformance-vectors.test.ts`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/test/conformance-vectors.test.ts) 读 `spec/conformance/profiles.json`，认领的档要求的每个向量要么被测试回放、要么列为缺口并写理由，规范在认领的档里新增向量时它失败；[`test/plugin-conformance.test.ts`](https://github.com/XiHanFun/framework-sdk-typescript/blob/main/test/plugin-conformance.test.ts) 用黑盒运行器跑示例插件 echo，CI 在 Windows 与 Linux 上都跑。

不提供的部分：

- 流式操作的契约还没有定义（RFC-0017「非目标」），本 SDK 不实现流式调用，`InvokeStream` 读第一帧后以 `InvalidArgument` 或 `Unimplemented` 结束。
- 远程 Provider 操作集（如 `caching.distributed@1`）仍未提供。
- 规范列为宿主能力、语言 SDK 可以不提供的部分本 SDK 不提供：管道的运行期改动（`UseOwned` / `RemoveOwnedBy`）、Schema 校验、响应过滤与幂等回放还原响应（RFC-0004、RFC-0011 规则 10）。

## 破坏性改动与迁移

本 SDK 还没有发布到 npm，目前没有需要迁移的破坏性改动。之后公开 API 或可观察行为有破坏性改动时，在这一节逐条写明旧行为、新行为与调用方怎么改。
