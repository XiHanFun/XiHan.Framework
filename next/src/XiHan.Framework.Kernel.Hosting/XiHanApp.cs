// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// XiHan 应用实例。持有服务提供器、管道和特性集合。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public sealed class XiHanApp : IAsyncDisposable
{
    private bool _disposed;

    internal XiHanApp(
        IServiceProvider serviceProvider,
        PipelineBuilder? pipelineBuilder,
        FeatureCollection features)
    {
        ServiceProvider = serviceProvider;
        Features = features;

        if (pipelineBuilder is not null)
        {
            Pipeline = pipelineBuilder.Build();
        }
    }

    /// <summary>
    /// 应用服务提供器。
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 已注册的特性集合。
    /// </summary>
    public FeatureCollection Features { get; }

    /// <summary>
    /// 管道执行入口。为 null 表示未配置管道。
    /// </summary>
    public PipelineDelegate? Pipeline { get; }

    /// <summary>
    /// 创建一个应用构建器。
    /// </summary>
    public static XiHanAppBuilder CreateBuilder(string[]? args = null) => new(args);

    /// <summary>
    /// 通过管道执行一个上下文。
    /// </summary>
    public async Task ExecuteAsync(PipelineContext context)
    {
        if (Pipeline is null)
            throw new InvalidOperationException("No pipeline has been configured.");

        await Pipeline(context);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (ServiceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (ServiceProvider is IDisposable disposable)
            disposable.Dispose();
    }
}
