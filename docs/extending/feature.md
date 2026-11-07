# 27.2 能力包与中间件

这一页讲框架的三条扩展轴各写什么，然后动手写一个应用自己的能力包（清单、权限码、操作登记）和一个管道中间件（`IPipelineMiddleware`、阶段与位置、相对次序），最后讲包 ID、清单 ID、能力 ID、错误码与健康项的命名规则。读完你能把业务代码按能力包组织，并在管道的任一阶段插入自己的逻辑。前置知识是[特性与清单](/startup/features)、[执行管道](/startup/pipeline)与[写一个 Provider](/extending/provider)。

## 为什么需要

业务代码如果只是一堆 `AddXxx` 扩展方法和散落的端点，依赖什么、贡献什么、在哪个阶段生效都看不出来，`xihan graph` 也画不出它。框架把能力包作为扩展的主轴：业务代码与 Provider 一样是实现 `IFeature` 的特性，清单写依赖的能力与贡献点，`Configure` 里显式登记服务、权限、中间件、操作与消息处理，内核按能力图谱装配，不靠扫描、标记接口或拦截器。管道固定十一个阶段，中间件挂在某个阶段、按声明的次序排列，所以横切逻辑有确定的位置，也能被工具展示和被测试宿主验证。

## 三条扩展轴

| 轴 | 写什么 | 怎样装配 | 适用 |
| --- | --- | --- | --- |
| 能力包 | 实现 `IFeature` 的特性与它的清单：子系统的 Provider、应用自己的业务能力包（操作、中间件、权限、消息处理） | 随应用编译，`UseFeature<T>()`；内核按清单的能力 ID 求解能力图谱后显式装配 | 绝大多数扩展 |
| 插件 | `kind: plugin` 的独立制品：进程内程序集、Wasm 模块或任意语言的子进程 | 运行期按目录发现、宿主授予权限、隔离运行（[隔离级别与清单](/plugins/overview)） | 第三方代码、不可信代码、跨语言实现、远程 Provider |
| 协议 | 协议适配器 `Protocols.*`：把操作登记表里的契约发布到一种线协议上 | 作为能力包装配，在宿主上映射端点 | 需要 HTTP、gRPC、MCP、CloudEvents 之外的线协议 |

能力包是主轴：Provider 与业务代码都是特性，差别只在清单。插件不进入能力图谱，靠握手声明的操作、订阅与（Wasm 的）中间件接进宿主。协议适配器读 `OperationRegistry` 里的 `OperationDescriptor`（契约与 JSON Schema），按 `expose` 过滤，把请求解码为 `OperationRequest`，经 `IOperationInvoker.InvokeAsync` 走完整管道，再把 `OperationResponse` 编码回线协议；`Expose` 的协议标志是固定枚举（`Http`、`Grpc`、`Mcp`、`CommandLine`、`Ai`），没有为第三方协议预留标志位，第三方协议适配器自己决定发布范围。

## 最小示例：写一个应用能力包

一个「便签」能力包：依赖持久化与授权，定义一个权限码，贡献一个操作与一个中间件。先写操作与请求、响应类型，`[Operation]` 由 `Kernel.Generators` 在编译期生成登记代码与 JSON Schema：

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace Contoso.Notes;

public sealed record CreateNoteRequest([Required, StringLength(80)] string Title);

public sealed record NoteResponse(Guid Id, string Title);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateNoteRequest))]
[JsonSerializable(typeof(NoteResponse))]
internal sealed partial class NotesJsonContext : JsonSerializerContext;

// 契约：操作 ID、稳定性、暴露到哪些协议、调用方要持有的权限码
[Operation("notes.create", Stability = Stability.Preview, Expose = Expose.Http | Expose.Mcp, Permission = "notes.write")]
public sealed class CreateNote : IOperation<CreateNoteRequest, NoteResponse>
{
    public ValueTask<Result<NoteResponse>> HandleAsync(CreateNoteRequest request, InvocationContext context)
    {
        Result<NoteResponse> result = new NoteResponse(Guid.CreateVersion7(), request.Title);
        return ValueTask.FromResult(result);
    }
}
```

再写特性：清单写依赖的能力与贡献点，`Configure` 里登记权限码、中间件与生成的操作：

```csharp
using Contoso.Correlation;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace Contoso.Notes;

public sealed class NotesFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.contoso.notes",
        Package = "Contoso.Notes",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
            new CapabilityRequirement(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(RequireCorrelationMiddleware).FullName!, PipelineStage.Validate)],
            Permissions = ["notes.write"],
        },
        Entry = typeof(NotesFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 权限码先登记，操作契约里的 Permission 才能引用；引用了没登记的码，装配完成时启动失败（KERNEL_VALIDATION_005）
        context.AddPermission(new PermissionDefinition("notes.write", "Write notes"));
        context.AddMiddleware<RequireCorrelationMiddleware>(PipelineStage.Validate);
        // Kernel.Generators 从 [Operation] 生成的登记代码与 JSON Schema
        context.AddGeneratedOperations();
    }
}
```

应用里 `builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<NotesFeature>())`，再 `app.MapXiHanOperations()`：`notes.create` 按路由约定成为 `POST /notes`，同一份契约也出现在 MCP 的 tool 列表里；`xihan graph` 能看到 `xihan.contoso.notes` 依赖的三个能力分别由谁提供；没有 `notes.write` 权限的主体在 Authorize 阶段被拒；`title` 超过 80 个字符在 Validate 阶段按 Schema 拒绝（[数据校验](/validation/)）。完整的业务能力包示例是 `samples/SimpleApp/Orders/OrdersFeature.cs`：依赖四个能力，登记生成的仓储、领域事件到消息的映射、消息处理器、三个权限码与两个操作。应用能力包通常不写 `Provides`；要让别的能力包按能力 ID 依赖它时再声明，能力 ID 的文法见下文。

## 写一个管道中间件

中间件实现 `IPipelineMiddleware.InvokeAsync(InvocationContext context, PipelineStep nextStep)`：调用 `nextStep(context)` 进入下游，不调用即短路；短路时用 `context.SetOutcome(...)` 写入失败结果，预期内的失败不抛异常。管道固定十一个阶段 `Ingress → Observe → Resilience → Authenticate → Tenant → Authorize → Validate → Idempotency → Transaction → Handle → Egress`，中间件挂在 Handle 之外的某个阶段。

上面能力包用到的中间件要求调用方带关联 ID：

```csharp
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace Contoso.Correlation;

public static class CorrelationMessages
{
    public const string Missing = "contoso.correlation.missing";
}

public sealed class RequireCorrelationMiddleware : IPipelineMiddleware
{
    public const string ErrorCode = "CONTOSO_VALIDATION_001";

    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        if (!context.TryGetBaggage(BaggageKeys.Correlation, out _))
        {
            // 短路：写入失败结果，不调用下游
            context.SetOutcome(Error.Validation(ErrorCode, CorrelationMessages.Missing));
            return ValueTask.CompletedTask;
        }

        return nextStep(context);
    }
}
```

四步接进管道：

1. 写中间件类。它以单例注册，构造函数只注入单例；要用 Scoped 服务时在 `InvokeAsync` 里从 `context.Services` 解析。
2. 在特性的 `Configure` 里 `context.AddMiddleware<T>(stage, placement)`：注册为单例并加入指定阶段，归属为特性的清单 ID。`Placement.End`（缺省）与 `Placement.Beginning` 决定在阶段内的基准位置；同阶段内与别的中间件的相对顺序用 `context.Pipeline.Before<TMiddleware, TOther>()` / `After<TMiddleware, TOther>()` 约束，也可以按名字 `Before(middleware, other)` 约束只登记一次的宿主中间件类型全名或插件中间件 `<清单 ID>:<名字>`。跨阶段的约束是 `KERNEL_PIPELINE_003`，成环是 `KERNEL_PIPELINE_001`，都在构建管道时报出。
3. 在清单 `Contributes.Pipeline` 里声明 `new PipelineContribution(typeof(T).FullName!, stage)`，与第 2 步的阶段一致；`xihan pipeline` 据此展示。
4. 用测试宿主分别验证放行与短路（[单元与集成测试](/testing/basics)）。

`Error.Validation(code, messageKey, details)` 只携带键与参数，宿主在 Egress 按调用方区域翻译；应用的 `CorrelationMessages.Missing` 要在应用自己的消息目录里有译文（[全球化和本地化](/localization/)）。阶段的选择按职责：补充 Baggage 或观测放 Observe，与身份有关的放 Authenticate 之后，与请求形状有关的放 Validate；不要把业务逻辑写进中间件，它看得到上下文与结果，但不该解析载荷，需要载荷的逻辑写成操作。Wasm 插件也能贡献中间件，语义是 before / after 两次钩子、看不到载荷、失败即关闭（[Wasm 插件](/plugins/wasm)）。

## 命名规则

向框架仓库提交的包由规范 `conventions/package-ids.md` 的文法与术语表推出，架构测试 `PackageIds_FollowGrammar` 执行：

```text
PackageId = "XiHan.Framework." Segment
Segment   = "Primitives" | "Kernel" [ "." KernelTool ] | "Domain" | "Testing" | "Cli"
          | Subsystem "." Role
          | "Hosting." Host | "Protocols." Protocol | "Plugins." ( Isolation | "Protocol" | "Conformance" ) | "Profiles." Profile
Role      = "Abstractions" | "Conformance" | "Generators" | Mechanism | Technology
```

| 对象 | 规则 | 示例 |
| --- | --- | --- |
| 包 ID | 领域在前、角色在后；实现包按机制（`InMemory`、`FileSystem`、`Database`）或技术（官方拼写，如 `EntityFrameworkCore`、`RabbitMQ`）命名；角色段禁用 `Default`、`Core`、`Common`、`Shared`、`Base`、`Impl`、`Util`、`Utils`、`Helper`、`Manager`、`Bus`、`Engine`；版本不进包名 | `XiHan.Framework.Caching.Redis`、`XiHan.Framework.Persistence.Conformance` |
| 默认实现 | 「默认」是清单的 `role: default`，不进包名，也不进类型名 | `Caching.InMemory` 的 `InMemoryCachingFeature` |
| 命名空间 | 等于包 ID，子命名空间最多一层、按功能区命名、与目录同名 | `XiHan.Framework.Caching.Abstractions.Stores` |
| 类型名 | 不带 `XiHan` 前缀（XH0002），不用 `Default` 前缀与 `Manager`、`Helper`、`Util(s)`、`Common`、`Shared`、`Impl`、`Base` 后缀（XH0004），公开类型标 `[ApiLevel]`（XH0001） | `RedisCacheStore` |
| 清单 ID | `^xihan\.[a-z][a-z0-9]*\.[a-z][a-z0-9-]*$` | `xihan.caching.redis` |
| 能力 ID | `<领域>.<能力>`，引用时带接口版本 `@1`，可选依赖加 `?`；接口变化只加新版本并存 | `persistence.relational@1`、`multitenancy.context@^1?` |
| 操作 ID | `<资源>.<动作>`，小写、点分，资源用复数 | `orders.create`、`notes.create` |
| 权限码 | 小写、点分、至少两段，不重复 | `notes.write` |
| 错误码 | `<领域大写>_<KIND>_<三位序号>`，KIND 为 `VALIDATION`、`NOTFOUND`、`CONFLICT`、`DENIED`、`UNAVAILABLE`、`TIMEOUT`、`CANCELLED`、`UNEXPECTED` 或领域自定义；序号一经发布不复用 | `CACHING_UNAVAILABLE_001` |
| 健康项 | `^[a-z][a-z0-9.-]*$`，约定 `<领域>.<机制>.<对象>` | `caching.redis.connection` |
| 配置节 | `XiHan:<领域>`，Provider 在其下用自己的子键 | `XiHan:Caching:Redis` |
| 消息键 | 小写、点分，`<领域>.<对象>.<情形>`，一种语言一个文件 `Localization/<区域>.json` | `contoso.correlation.missing` |

框架外的扩展包不受 `XiHan.Framework.*` 包名文法约束，但清单 ID、能力 ID、操作 ID、错误码与健康项的文法由内核校验，照样适用；想让编译器执行同样的类型命名规则，按 [Kernel.Analyzers](/packages/kernel-analyzers) 启用分析器。

## 常见问题

### 业务代码一定要写成能力包吗

不一定，最小 API 端点、普通服务照常可用（[端点里使用框架](/web/endpoints)）。但只有能力包里登记的操作才经完整管道、被协议适配器统一发布、进入 `xihan graph` 与 OpenAPI；只有清单声明的依赖才被内核在启动时核对。业务按能力包组织，端点只做协议层的事。

### 一个应用要拆几个能力包

按业务边界拆，每个包一份清单、各自声明依赖；共享的实体与契约放在它们都依赖的包里。拆得太细没有收益，一个包也完全可以，`samples/SimpleApp` 的订单就是一个能力包。

### 中间件能拿到请求体吗

能力包里的 .NET 中间件在 Handle 之前运行，`InvocationContext` 里有元数据、Baggage 与操作 ID，载荷在 Handle 阶段才解码成请求类型。需要按请求内容判断的逻辑写在操作里或请求类型的自校验里（[数据校验](/validation/)），不要在中间件里解析 JSON。

### 中间件的次序约束指向的目标不存在

`Before` / `After` 的目标必须在同一阶段且只登记一次，否则构建管道时报 `KERNEL_PIPELINE_003`。用 `xihan pipeline` 看实际的阶段与名字，宿主中间件按类型全名引用，插件中间件按 `<清单 ID>:<名字>` 引用。

### 自定义协议怎么接

写一个作为能力包装配的 `Protocols.*` 适配器：从 `OperationRegistry` 读契约与 Schema，把请求解码为 `OperationRequest`，经 `IOperationInvoker` 调用，再编码响应。`Expose` 没有为第三方协议预留标志位，发布范围由适配器自己的配置决定；现成的四种协议见 [gRPC](/protocols/grpc)、[MCP](/protocols/mcp)、[CloudEvents](/protocols/cloudevents) 与[命令行宿主](/protocols/command-line)。

## 相关包与下一步

- 包参考：[Kernel](/packages/kernel)、[Kernel.Generators](/packages/kernel-generators)、[Kernel.Analyzers](/packages/kernel-analyzers)、[Testing](/packages/testing)
- 相关章节：[特性与清单](/startup/features)、[执行管道](/startup/pipeline)、[操作契约](/web/operations)、[授权与权限](/security/authorization)、[全球化和本地化](/localization/)、[写一个 Provider](/extending/provider)
- 规范：RFC-0002《清单》、RFC-0004《管道阶段与排序》、RFC-0010《本地化》
- 下一步：[For Agents](/agents/agents)讲怎样让编码助手按版本查证框架、用 MCP 与 `xihan` 工具开发
