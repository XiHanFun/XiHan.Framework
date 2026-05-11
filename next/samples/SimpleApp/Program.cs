// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan =>
{
    xihan.UseFeature<GreetingFeature>();
    xihan.UsePipeline(pipeline =>
    {
        pipeline.Use<RequestTimingMiddleware>();
    });
});

var app = builder.Build();

app.MapGet("/", () => XiHanMetadata.GetSummary());
app.MapGet("/greet", (string? name) => new { message = $"Hello, {name ?? "XiHan"}!" });

app.UseXiHan();
app.Run();

// --- Features ---

internal sealed class GreetingFeature : IXiHanFeature
{
    public string Name => "Greeting";

    public void Configure(IFeatureConfigurationContext context)
    {
        // 注册此特性需要的服务
    }
}

// --- Middleware ---

internal sealed class RequestTimingMiddleware : IPipelineMiddleware
{
    public async Task InvokeAsync(PipelineContext context, PipelineHandler nextHandler)
    {
        var sw = Stopwatch.StartNew();
        await nextHandler(context);
        sw.Stop();
        Console.WriteLine($"[{context.TraceId}] Request took {sw.ElapsedMilliseconds}ms");
    }
}
