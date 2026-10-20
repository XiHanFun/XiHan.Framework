// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SimpleApp;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, SampleJsonContext.Default));

// 一行一个能力包；依赖写在清单里按能力 ID 解析，不扫描、不推断
builder.AddXiHan(xihan => xihan.UseFeature<GreetingFeature>());

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

app.Run();
