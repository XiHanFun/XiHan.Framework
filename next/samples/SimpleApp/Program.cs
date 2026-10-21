// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using SimpleApp;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, SampleJsonContext.Default));

// Standard 组合：五个子系统的内存默认实现；再加事务性发件箱与两个业务能力包。
// 依赖写在清单里按能力 ID 解析；换 Provider 只需再 UseFeature 一个显式实现。
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutboxFeature>()
    .UseFeature<GreetingFeature>()
    .UseFeature<OrdersFeature>());

var app = builder.Build();

// 其余端点在内核管道的 Handle 阶段内执行；探针直接映射应用状态机
app.UseXiHan();
app.MapXiHanHealth();

app.MapGet("/", () => $"{FrameworkInfo.Name} {FrameworkInfo.Version}");
app.MapGet("/greet", (string? name, GreetingCounter counter) => new Greeting($"Hello, {name ?? "XiHan"}!", counter.Count));
app.MapGet("/missing", (HttpContext http) =>
{
    // 把失败结果写进调用上下文，Egress 由宿主翻译为 RFC 9457 Problem Details
    http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("SAMPLES_NOTFOUND_001", "示例资源不存在。"));
    return Results.Empty;
});

// 一个带租户、鉴权、事务的操作：ApiKey 认证 → 主体声明解析租户 → 权限判定 → 工作单元内写订单并经发件箱发消息
app.MapPost("/orders", async (PlaceOrderRequest request, HttpContext http, IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, IMessagePublisher publisher, TimeProvider clock) =>
{
    var context = http.GetXiHanContext().ValueOrThrow();
    var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };
    order.Place(clock.GetUtcNow());

    var added = await orders.AddAsync(order, context.CancellationToken);
    if (added.IsFailure)
    {
        context.SetOutcome(added);
        return Results.Empty;
    }

    var response = new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    var envelope = new MessageEnvelope
    {
        Id = order.Id.ToString("N"),
        Source = "/orders",
        Type = "orders.placed",
        Subject = $"order/{order.Id:N}",
        Time = order.CreatedAt,
        Data = JsonSerializer.SerializeToUtf8Bytes(response, SampleJsonContext.Default.OrderResponse),
    }.WithExtension(EnvelopeExtensions.Tenant, order.TenantId);

    var published = await publisher.PublishAsync(OrdersFeature.Topic, envelope, context.CancellationToken);
    if (published.IsFailure)
    {
        context.SetOutcome(published);
        return Results.Empty;
    }

    return Results.Created($"/orders/{order.Id:N}", response);
}).RequireXiHanPermission("orders.write");

app.MapGet("/orders", async (HttpContext http, IRepository<Order, Guid> orders) =>
{
    var context = http.GetXiHanContext().ValueOrThrow();
    var list = await orders.ListAsync(EntityQuery.All<Order>().OrderBy(static o => o.CreatedAt), context.CancellationToken);
    return list.Select(static o => new OrderResponse(o.Id, o.Name, o.Amount, o.CreatedAt)).ToList();
}).RequireXiHanPermission("orders.read");

app.MapGet("/orders/stats", (OrderEventCounter counter) => new OrderStatistics(counter.Handled)).RequireXiHanAuthentication();

app.MapGet("/whoami", (HttpContext http) =>
{
    var security = http.GetXiHanContext().ValueOrThrow().GetSecurity();
    return new WhoAmI(security.Principal.Id, security.Principal.IsAuthenticated, security.Tenant?.Id);
});

app.Run();
