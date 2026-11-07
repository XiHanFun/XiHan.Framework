# 2.2 第一个应用

这一页从一个空的 ASP.NET Core 项目开始，写一个操作契约并经 HTTP 调用它，再换成 Standard 组合观察能力图谱与管道。全程约十五分钟，不需要任何数据库或中间件。前置条件是[环境与安装](/getting-started/installation)里的 .NET 10 SDK。

## 为什么从一个操作开始

在 XiHan Framework 里，业务的最小单位是操作：一个类，一个请求类型，一个响应类型，一个 ID。框架从这一个类推出 HTTP 路由、OpenAPI 文档、gRPC 方法、MCP tool 与命令行命令。所以第一个应用只需要三样东西：一个操作、一个把操作交给内核的特性、一个入口。

## 第一步：创建项目

```bash
dotnet new web -n HelloXiHan
cd HelloXiHan
dotnet add package XiHan.Framework.Profiles.Minimal --prerelease
dotnet add package XiHan.Framework.Protocols.Http --prerelease
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

三个包分别是：组合包（内核、ASP.NET Core 宿主与内存本地化）、HTTP 协议适配器、操作源生成器。确认项目文件里 `RootNamespace` 是 `HelloXiHan`，生成的登记代码会放在这个命名空间下。

## 第二步：写操作契约

新建 `Greet.cs`。操作 ID `greetings.create` 的资源是 `greetings`、动词是 `create`，HTTP 适配器据此推导出 `POST /greetings`：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace HelloXiHan;

[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http)]
public sealed class Greet : IOperation<GreetRequest, GreetResponse>
{
    public const string Id = "greetings.create";

    public ValueTask<Result<GreetResponse>> HandleAsync(GreetRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new GreetResponse($"Hello, {request.Name}!", context.TraceId)));
}

public sealed record GreetRequest(string Name);

public sealed record GreetResponse(string Message, string TraceId);
```

三件事值得注意：

- `HandleAsync` 返回 `Result<GreetResponse>`。可恢复的失败返回 `Error`，不抛异常，见[结果与错误处理](/errors/)。
- `InvocationContext` 是这次调用的上下文：取消令牌、截止时间、Trace、租户与主体都从这里取，见[调用上下文](/startup/invocation-context)。
- `Expose = Expose.Http` 声明这个操作只经 HTTP 暴露。加上 `Expose.Grpc`、`Expose.Mcp` 就能同时成为 gRPC 方法与 MCP tool，见[操作契约](/web/operations)。

## 第三步：登记序列化类型

内核不经反射序列化。新建 `AppJsonContext.cs`，把请求与响应类型登记到源生成的序列化上下文；`Kernel.Generators` 从这里取类型元数据，类型没有登记时编译报 `XHG002`：

```csharp
using System.Text.Json.Serialization;

namespace HelloXiHan;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(GreetResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

## 第四步：写特性

新建 `GreetingsFeature.cs`。特性是内核装配的最小单元：清单声明它只依赖内核的宿主能力，`Configure` 调用源生成器产出的 `AddGeneratedOperations` 登记操作与它的 JSON Schema：

```csharp
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace HelloXiHan;

public sealed class GreetingsFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.hello.greetings",
        Package = "HelloXiHan",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(GreetingsFeature).FullName,
    };

    public void Configure(FeatureContext context) => context.AddGeneratedOperations();
}
```

清单 ID 的形式固定是 `xihan.<领域>.<名称>` 三段。清单的全部字段见[特性与清单](/startup/features)。

## 第五步：改写 Program.cs

```csharp
using HelloXiHan;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

// 把特性交给内核：求解能力图谱，按拓扑顺序调用各特性的 Configure
builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

var app = builder.Build();

app.UseXiHan();            // HTTP 请求进入内核管道
app.MapXiHanHealth();      // /health/startup、/health/ready、/health/live
app.MapXiHanOperations();  // 按操作登记表映射路由

app.Run();
```

`AddXiHan` 在构建期求解能力图谱并调用各特性的 `Configure`；`UseXiHan` 让每个 HTTP 请求进入内核管道；`MapXiHanOperations` 把登记的操作映射为路由。

## 第六步：运行

```bash
dotnet run --urls http://localhost:5000
```

启动日志先打印已解析的能力图谱，再打印管道顺序：

```text
Capability graph:
xihan.kernel.core <版本>
  provides kernel.hosting@1
  ...
xihan.localization.inmemory <版本> (default)
  provides localization.resources@1
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.protocols.http <版本>
  provides protocols.http@1
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.hello.greetings 1.0.0
  requires kernel.hosting@1 -> xihan.kernel.core

Pipeline:
observe      XiHan.Framework.Kernel.Diagnostics.ObserveMiddleware
resilience   XiHan.Framework.Kernel.Pipeline.DeadlineMiddleware
resilience   XiHan.Framework.Kernel.Resilience.ResilienceMiddleware
validate     XiHan.Framework.Kernel.Validation.ValidationMiddleware
```

调用操作：

```bash
curl -i -X POST http://localhost:5000/greetings -H "Content-Type: application/json" -d '{"name":"Ada"}'
```

```text
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8

{"message":"Hello, Ada!","traceId":"c4a453046dcf9b7fac181cb8d5475014"}
```

`create` 动词返回 201。请求不满足契约时，Validate 阶段按生成的 JSON Schema 拒绝，宿主按 `Accept-Language` 翻译成 RFC 9457 Problem Details：

```bash
curl -i -X POST http://localhost:5000/greetings -H "Content-Type: application/json" -H "Accept-Language: zh-CN" -d '{}'
```

```text
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json
Content-Language: zh-CN

{"type":"urn:xihan:error:KERNEL_VALIDATION_003","title":"VALIDATION","status":400,
 "detail":"操作 'greetings.create' 的请求不符合契约：$.name: required property 'name' is missing",
 "instance":"/greetings","code":"KERNEL_VALIDATION_003","traceId":"…",
 "errors":{"name":["缺少必填属性 'name'"]},
 "details":{"id":"greetings.create","reason":"$.name: required property 'name' is missing"}}
```

`/health/ready` 返回应用状态与每个特性的状态，可以直接作为就绪探针。

## 换成 Standard 组合

把 `Profiles.Minimal` 换成 `Profiles.Standard`，`UseMinimalProfile()` 换成 `UseStandardProfile()`，应用多了持久化、缓存、多租户、安全与消息五个子系统的内存默认实现：

```csharp
using HelloXiHan;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GreetingsFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();
app.Run();
```

管道里出现了各子系统贡献的中间件，顺序由阶段决定，与 `UseFeature` 的书写顺序无关：

```text
observe      XiHan.Framework.Kernel.Diagnostics.ObserveMiddleware
resilience   XiHan.Framework.Kernel.Pipeline.DeadlineMiddleware
resilience   XiHan.Framework.Kernel.Resilience.ResilienceMiddleware
authenticate XiHan.Framework.Security.Abstractions.Authentication.AuthenticationMiddleware
tenant       XiHan.Framework.MultiTenancy.Abstractions.Resolution.TenantResolutionMiddleware
authorize    XiHan.Framework.Security.Abstractions.Authorization.AuthorizationMiddleware
validate     XiHan.Framework.Kernel.Validation.ValidationMiddleware
idempotency  XiHan.Framework.Caching.Abstractions.Idempotency.IdempotencyMiddleware
transaction  XiHan.Framework.Persistence.Abstractions.UnitOfWork.UnitOfWorkMiddleware
```

`Greet` 没有声明 `Permission`，Authorize 阶段直接放行，调用结果与前面相同。给操作加上 `Permission = "greetings.write"` 后，还要在特性里登记这个权限码，并在 `XiHan:Security:InMemory` 下配置主体与授予，做法见[授权与权限](/security/authorization)。

## 常见问题

### 编译报 XHG002

操作的请求或响应类型没有登记到 `JsonSerializerContext`。回到第三步，确认两个 `[JsonSerializable]` 都在。

### 启动后访问 `/greetings` 返回 404

逐项检查：特性的 `Configure` 调用了 `AddGeneratedOperations()`；项目引用了 `Kernel.Generators`；操作的 `Expose` 含 `Http`；`Program.cs` 里加入了 `HttpProtocolFeature`，并且 `MapXiHanOperations()` 在 `UseXiHan()` 之后。

### 想看能力图谱与管道但不想翻日志

接上命令行宿主后运行 `graph` 与 `pipeline` 命令，或在代码里调用 `App.DescribeGraph()` 与 `App.DescribePipeline()`，见[命令行宿主](/protocols/command-line)。

## 下一步

- [核心概念](/getting-started/concepts)：把刚才用到的每个概念讲透
- [示例说明](/appendix/samples)：仓库里完整的订单示例
- [特性与清单](/startup/features)：清单的全部字段
