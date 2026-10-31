# 快速上手

本页从取得 5.0 预览包开始，建一个 ASP.NET Core 应用，写一个操作契约并经 HTTP 调用它，再换成 Standard 组合观察能力图谱与管道。适合第一次使用 5.0 的开发者，全程约 15 分钟。

## 环境准备

| 项 | 要求 |
| --- | --- |
| .NET SDK | .NET 10 SDK；从源码构建框架时用仓库 `global.json` 锁定的 10.0.1xx 功能带 |
| Git | 克隆 `XiHan.Framework` 仓库并切到 `next` 分支 |
| 编辑器 | Visual Studio 2026、Rider 或 VS Code（C# Dev Kit） |

## 获取预览包

5.0 尚未发布到 nuget.org。框架源码在 [XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) 仓库的 `next` 分支，包的版本是 `5.0.0-preview.1`。两种引用方式任选其一。

### 方式一：源码引用

适合同时调试框架源码的场景。

1. 克隆仓库并切到 `next` 分支：

   ```bash
   git clone https://github.com/XiHanFun/XiHan.Framework.git
   cd XiHan.Framework
   git switch next
   ```

2. 需要构建整个解决方案时初始化规范子模块（`Plugins.Protocol` 从 `next/spec/plugin/plugin.proto` 生成代码）：

   ```bash
   git submodule update --init next/spec
   ```

3. 应用项目以 `ProjectReference` 引用 `next/src` 下的项目，源生成器按分析器引用：

   ```xml
   <ItemGroup>
     <ProjectReference Include="../XiHan.Framework/next/src/XiHan.Framework.Profiles.Minimal/XiHan.Framework.Profiles.Minimal.csproj" />
     <ProjectReference Include="../XiHan.Framework/next/src/XiHan.Framework.Protocols.Http/XiHan.Framework.Protocols.Http.csproj" />
     <ProjectReference Include="../XiHan.Framework/next/src/XiHan.Framework.Kernel.Generators/XiHan.Framework.Kernel.Generators.csproj"
                       OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
   </ItemGroup>
   ```

### 方式二：本地包源

适合只使用框架、不改框架源码的场景。

1. 在仓库根还原、构建并打包（打包前先按方式一的第 2 步初始化子模块）：

   ```bash
   dotnet restore next/XiHan.Framework.Next.slnx
   dotnet build next/XiHan.Framework.Next.slnx -c Release --no-restore
   dotnet pack next/XiHan.Framework.Next.slnx -c Release --no-build
   ```

   包写到 `next/artifacts/packages/`，文件名形如 `XiHan.Framework.Kernel.5.0.0-preview.1.nupkg`。

2. 在应用的解决方案目录放一个 `nuget.config`，把这个目录登记为包源：

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="xihan-next-local" value="../XiHan.Framework/next/artifacts/packages" />
     </packageSources>
   </configuration>
   ```

3. 应用项目以 `PackageReference` 引用，版本写 `5.0.0-preview.1`：

   ```xml
   <ItemGroup>
     <PackageReference Include="XiHan.Framework.Profiles.Minimal" Version="5.0.0-preview.1" />
     <PackageReference Include="XiHan.Framework.Protocols.Http" Version="5.0.0-preview.1" />
     <PackageReference Include="XiHan.Framework.Kernel.Generators" Version="5.0.0-preview.1" PrivateAssets="all" />
   </ItemGroup>
   ```

`Kernel.Generators` 是开发期依赖，只在编译时运行，不进入应用的运行时依赖。

## 第一步：创建项目

```bash
dotnet new web -n HelloXiHan
cd HelloXiHan
```

按上一节任一方式加入三个引用：`Profiles.Minimal`（内核、ASP.NET Core 宿主与内存本地化）、`Protocols.Http`（HTTP 协议适配器）、`Kernel.Generators`（操作源生成器）。项目文件里确认 `RootNamespace` 为 `HelloXiHan`，生成的登记代码放在这个命名空间下。

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

`HandleAsync` 返回 `Result<GreetResponse>`：可恢复的失败返回 `Error`，不抛异常。

## 第三步：登记序列化类型

内核不经反射序列化。新建 `AppJsonContext.cs`，把请求与响应类型登记到源生成的序列化上下文；`Kernel.Generators` 从这里取类型元数据，类型没有登记时报 `XHG002`：

```csharp
using System.Text.Json.Serialization;

namespace HelloXiHan;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GreetRequest))]
[JsonSerializable(typeof(GreetResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

## 第四步：写特性

新建 `GreetingsFeature.cs`。清单声明这个特性只依赖内核的宿主能力，`Configure` 调用源生成器产出的 `AddGeneratedOperations` 登记操作与它的 JSON Schema：

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

## 第六步：运行

```bash
dotnet run --urls http://localhost:5000
```

启动日志先打印已解析的能力图谱，再打印管道顺序：

```text
Capability graph:
xihan.kernel.core 5.0.0-preview.1
  provides kernel.hosting@1
  ...
xihan.localization.inmemory 5.0.0-preview.1 (default)
  provides localization.resources@1
  requires kernel.hosting@1 -> xihan.kernel.core
xihan.protocols.http 5.0.0-preview.1
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

请求不满足契约时，Validate 阶段按生成的 JSON Schema 拒绝，宿主按 `Accept-Language` 翻译成 RFC 9457 Problem Details：

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

`/health/ready` 返回应用状态与每个特性的状态，可直接作为就绪探针。

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

`Greet` 没有声明 `Permission`，Authorize 阶段直接放行，调用结果与前面相同。给操作加上 `Permission = "greetings.write"` 后，还要在特性里登记这个权限码，并在 `XiHan:Security:InMemory` 下配置主体与授予，做法见[授权](./guide/authorization)。

## 完整示例

仓库的 `next/samples` 下有几个零外部设施即可运行的示例：

| 示例 | 内容 |
| --- | --- |
| `SimpleApp` | Standard 组合：订单的两个操作同时经 HTTP、gRPC 与 MCP 暴露，入站 CloudEvents 交给消息处理器，带租户、ApiKey 认证、权限、事务、领域事件与发件箱；Native AOT 冒烟项目 |
| `SimpleCli` | Minimal 组合：同一份契约作为命令行命令与 MCP stdio tool |
| `KubernetesApp` | Full 组合：探针、停机宽限、ConfigMap 热更新、Lease 领导选举与 `xihan pack` 产物 |
| `SamplePlugin`、`EchoPlugin`、`EchoWasmPlugin`、`EchoGoPlugin`、`CacheGoPlugin` | 进程内、进程外（.NET、Go）与 Wasm 插件 |

```bash
dotnet run --project next/samples/SimpleApp
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-alice-key" -H "Content-Type: application/json" -d '{"name":"widget","amount":3}'
```

## 下一步

1. [能力包与清单](./guide/modularity)：特性、清单与能力图谱
2. [操作契约](./guide/operations)：操作 ID、暴露协议、授权要求与源生成器
3. [执行管道](./guide/pipeline)：十一个阶段与中间件
4. [包总览](./packages/)：按需选择 Provider
