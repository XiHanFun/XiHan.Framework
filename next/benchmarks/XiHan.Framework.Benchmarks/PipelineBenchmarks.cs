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
    /// <summary>
    /// 解析中间件实例的容器。
    /// </summary>
    /// <remarks>
    /// 在 <see cref="Setup"/> 构建、<see cref="Cleanup"/> 释放。
    /// </remarks>
    private ServiceProvider _services = null!;

    /// <summary>
    /// 只含一个中间件的管道。
    /// </summary>
    private PipelineStep _single = null!;

    /// <summary>
    /// 五个阶段各一个中间件的管道。
    /// </summary>
    private PipelineStep _five = null!;

    /// <summary>
    /// 复用的调用上下文。
    /// </summary>
    /// <remarks>
    /// 基准只测管道本身，不计上下文创建。
    /// </remarks>
    private InvocationContext _context = null!;

    /// <summary>
    /// 构建容器、两条管道与共享上下文。
    /// </summary>
    /// <remarks>
    /// 每次基准运行前执行一次。
    /// </remarks>
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

        // 终端步骤：直接把结果置为成功，不做任何业务工作
        static ValueTask Terminal(InvocationContext context)
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        }

        // 五个中间件分布在五个不同阶段，覆盖跨阶段排序的开销
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

    /// <summary>
    /// 释放容器。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => _services.Dispose();

    /// <summary>
    /// 基线：单中间件管道的一次调用。
    /// </summary>
    [Benchmark(Baseline = true)]
    public ValueTask SingleMiddleware() => _single(_context);

    /// <summary>
    /// 五中间件管道的一次调用。
    /// </summary>
    [Benchmark]
    public ValueTask FiveMiddleware() => _five(_context);

    /// <summary>
    /// 直通中间件。
    /// </summary>
    /// <remarks>
    /// 不做任何事，直接调用下一步。五个类型只为占据五个不同的注册位。
    /// </remarks>
    public sealed class PassThroughA : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    /// <summary>
    /// 直通中间件，见 <see cref="PassThroughA"/>。
    /// </summary>
    public sealed class PassThroughB : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    /// <summary>
    /// 直通中间件，见 <see cref="PassThroughA"/>。
    /// </summary>
    public sealed class PassThroughC : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    /// <summary>
    /// 直通中间件，见 <see cref="PassThroughA"/>。
    /// </summary>
    public sealed class PassThroughD : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }

    /// <summary>
    /// 直通中间件，见 <see cref="PassThroughA"/>。
    /// </summary>
    public sealed class PassThroughE : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }
}
