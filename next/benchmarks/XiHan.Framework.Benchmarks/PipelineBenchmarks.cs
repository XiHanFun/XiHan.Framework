// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 管道调用开销：目标单中间件 &lt; 0.1 ms，5 个中间件 &lt; 0.5 ms。
/// </summary>
[MemoryDiagnoser]
public class PipelineBenchmarks
{
    private ServiceProvider _services = null!;
    private PipelineStep _single = null!;
    private PipelineStep _five = null!;
    private InvocationContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection()
            .AddSingleton<PassThroughA>()
            .AddSingleton<PassThroughB>()
            .AddSingleton<PassThroughC>()
            .AddSingleton<PassThroughD>()
            .AddSingleton<PassThroughE>()
            .BuildServiceProvider();

        static ValueTask Terminal(InvocationContext context)
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        }

        _single = new PipelineBuilder().UseAt<PassThroughA>(PipelineStage.Handle).Build(Terminal);
        _five = new PipelineBuilder()
            .UseAt<PassThroughA>(PipelineStage.Ingress)
            .UseAt<PassThroughB>(PipelineStage.Observe)
            .UseAt<PassThroughC>(PipelineStage.Authorize)
            .UseAt<PassThroughD>(PipelineStage.Transaction)
            .UseAt<PassThroughE>(PipelineStage.Handle)
            .Build(Terminal);
        _context = App.CreateContext(_services, CancellationToken.None);
    }

    [GlobalCleanup]
    public void Cleanup() => _services.Dispose();

    [Benchmark(Baseline = true)]
    public ValueTask SingleMiddleware() => _single(_context);

    [Benchmark]
    public ValueTask FiveMiddleware() => _five(_context);

    public sealed class PassThroughA : IPipelineMiddleware
    {
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    public sealed class PassThroughB : IPipelineMiddleware
    {
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    public sealed class PassThroughC : IPipelineMiddleware
    {
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    public sealed class PassThroughD : IPipelineMiddleware
    {
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    public sealed class PassThroughE : IPipelineMiddleware
    {
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }
}
