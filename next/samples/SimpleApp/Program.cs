// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SimpleApp;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;

var builder = WebApplication.CreateBuilder(args);

// 示例保持 AOT 友好：把源生成的序列化上下文放在解析链最前面，端点返回的类型不走反射序列化
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, SampleJsonContext.Default));

// Standard 组合：五个子系统的内存默认实现；再加 HTTP 协议适配器、事务性发件箱与两个业务能力包。
// 依赖写在清单里按能力 ID 解析；换 Provider 只需再 UseFeature 一个显式实现。
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OutboxFeature>()
    .UseFeature<GreetingFeature>()
    .UseFeature<OrdersFeature>());

// 应用自己的消息模板（SampleMessages）与译文（嵌入的 Localization/<区域>.json）；Problem Details 的 detail 按 Accept-Language 翻译，默认英文
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(SampleMessages)));

var app = builder.Build();

// 其余端点在内核管道的 Handle 阶段内执行；探针直接映射应用状态机
app.UseXiHan();
app.MapXiHanHealth();

// 操作契约经 HTTP 暴露：orders.create → POST /orders（201），orders.list → GET /orders；授权按契约里的权限码判定
app.MapXiHanOperations();

app.MapGet("/", () => $"{FrameworkInfo.Name} {FrameworkInfo.Version}");
app.MapGet("/greet", (string? name, GreetingCounter counter) => new Greeting($"Hello, {name ?? "XiHan"}!", counter.Count));
app.MapGet("/missing", (HttpContext http) =>
{
    // 把失败结果写进调用上下文，Egress 由宿主翻译为 RFC 9457 Problem Details
    http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("SAMPLES_NOTFOUND_001", SampleMessages.ResourceMissing));
    return Results.Empty;
});

app.MapGet("/orders/stats", (OrderEventCounter counter) => new OrderStatistics(counter.Handled)).RequireXiHanAuthentication();

app.MapGet("/whoami", (HttpContext http) =>
{
    var security = http.GetXiHanContext().ValueOrThrow().GetSecurity();
    return new WhoAmI(security.Principal.Id, security.Principal.IsAuthenticated, security.Tenant?.Id);
});

app.Run();
