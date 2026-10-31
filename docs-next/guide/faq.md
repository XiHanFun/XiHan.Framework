# 常见问题

本页收集使用 5.0 时最常遇到的问题与排查方法，按装配、操作与协议、数据与安全、版本与迁移分组。

## 装配

### 启动时报 KERNEL_CAPABILITY_004，说某个能力有多个候选

同一能力有两个以上显式 Provider，内核无法替应用选择。在配置里指定提供者，或在代码里调用 `UseProvider`：

```json
{ "XiHan": { "Providers": { "caching.distributed": "xihan.caching.redis" } } }
```

清单 ID 写在各包页的信息表里；规则见[能力包与清单](./modularity#选择-provider)。

### 启动时报 KERNEL_CAPABILITY_001，说依赖没有提供者

某个特性的 `Requires` 里有一个必需能力，但应用没有加入任何提供它的特性。错误信息给出需求方与能力 ID，加入对应的默认实现或 Provider（`UseFeature<...>()`）即可；不需要它时把依赖标为可选。

### 引用了 Provider 包，但运行时仍在用内存实现

只引用包不会生效，还要在 `AddXiHan` 里 `UseFeature<...>()` 加入它的特性。加入后看启动日志里的能力图谱：被顶替的默认实现不再出现在该能力下，完全没有被选中的节点不装配。

### 中间件第一次调用时报「没有注册服务」

用 `AppBuilder.ConfigurePipeline` 或 `Pipeline.UseAt<T>` 登记的中间件，内核不替应用注册类型，需要自己 `builder.Services.AddSingleton<T>()`。在特性里用 `context.AddMiddleware<T>()` 登记则由内核一并注册。

### 中间件里取不到主体或租户

主体在 Authenticate 阶段、租户在 Tenant 阶段之后才写入上下文。中间件要放在这些阶段之后；同一阶段内依赖另一个包中间件的产出时，必须用 `After<TMiddleware, TOther>()` 声明次序，不能依赖归属字典序。见[执行管道](./pipeline#排序规则)。

### 怎样查看最终的能力图谱与管道顺序

启动日志在 Information 级别打印两者。接了命令行宿主时运行 `graph` 与 `pipeline` 命令；代码里用 `App.DescribeGraph()` 与 `App.DescribePipeline()`。

## 操作与协议

### 编译报 XHG002

操作的请求或响应类型没有登记到任何 `JsonSerializerContext`。在项目的序列化上下文上加 `[JsonSerializable(typeof(...))]`，见[操作契约](./operations#登记序列化类型与-schema)。

### 操作没有出现在 HTTP 路由或 OpenAPI 里

逐项检查：

1. 特性的 `Configure` 调用了 `context.AddGeneratedOperations()`，项目引用了 `Kernel.Generators`（以分析器方式）。
2. 操作的 `Expose` 包含 `Expose.Http`；没写 `Expose` 时看程序集级 `OperationDefaults`。
3. 操作的 `Stability` 不是 `Internal`。
4. 应用加入了 `HttpProtocolFeature`，并在 `app.UseXiHan()` 之后调用了 `app.MapXiHanOperations()`。

### 两个操作启动时报 PROTOCOLS_CONFLICT_002

两个操作推导出了同一条 HTTP 路由或同一个 gRPC 方法名，典型情况是请求都不含 `id` 的 `get` 与 `list`。按操作覆盖其中一条路由，见 [HTTP 与 OpenAPI](./http)。

### 调用返回 503 与 KERNEL_UNAVAILABLE_001

应用还没有就绪或正在停止。看 `/health/startup` 与 `/health/ready` 的响应，找出状态不是 `Ready` 的特性；启动失败的原因记录在启动日志里。

### 返回 503 与 KERNEL_UNAVAILABLE_002、003 或 004

分别是限流、并发上限与熔断拒绝，来自 `XiHan:Kernel:Resilience` 的配置。响应的 `details.retryAfter` 给出建议的重试秒数，见[弹性与限流](./resilience)。

### Problem Details 的 detail 总是英文

宿主按请求头 `Accept-Language` 选择区域，没有指定时用默认区域 `en-US`。应用自己的消息键要在对应语言的 `Localization/<区域>.json` 里有译文，并用 `AddMessageCatalog` 登记目录，见[国际化](./localization)。

### 什么时候是 401，什么时候是 403

`SECURITY_DENIED_001`（未认证）与 `SECURITY_DENIED_003`（凭据无效、过期或签名不符）是 401，并带 `WWW-Authenticate` 质询；已认证但缺少权限（`SECURITY_DENIED_004`）或被策略拒绝（`SECURITY_DENIED_002`）是 403。

## 数据与安全

### 在后台任务里使用仓储报 PERSISTENCE_UNAVAILABLE_001

仓储只能在活动的工作单元内使用。操作调用由 Transaction 阶段自动开启工作单元；管道之外（后台循环、`IHostedFeature.StartAsync`、单例服务）要自己创建作用域并开启工作单元，写法见[工作单元与事务](./uow)。

### 重启后内存里的数据都没了

`Persistence.InMemory` 是开发与测试用的默认实现，数据只在进程内且容量有界。生产环境加入 `Persistence.EntityFrameworkCore` 或 `Persistence.SqlSugar`。

### 新增多租户实体时报 PERSISTENCE_VALIDATION_001

实体实现了 `IMultiTenant`，但当前调用没有租户。检查认证主体是否带租户声明、调用方是否在 Baggage 里给出租户，或把配置 `XiHan:MultiTenancy:RequireTenant` 打开，让没有租户的调用在 Tenant 阶段就被拒绝，见[多租户](./multi-tenancy)。

## 版本与迁移

### 5.0 能用于生产吗

5.0 当前是 `5.0.0-preview.1`，尚未发布到 nuget.org，公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整。评估与新项目试点可以使用；需要稳定版本的生产系统继续使用 4.x。

### 4.x 与 5.0 能在一个应用里混用吗

不能。两条版本线互不引用，部分包 ID 相同而主版本不同，同一个应用只能引用其中一条。

### 4.x 的某个模块在 5.0 里找不到

对照表与不再提供的能力见[从 4.x 迁移](./upgrade)。

### 有没有 4.x 到 5.0 的自动迁移工具

没有。`xihan migrate` 只保留通用机制，当前没有登记任何规则集。
