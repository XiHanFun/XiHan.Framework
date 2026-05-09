// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// XiHan 应用实例。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public sealed class XiHanApp : IAsyncDisposable
{
    private readonly bool _ownsProvider;
    private bool _disposed;

    internal XiHanApp(IServiceProvider serviceProvider, PipelineBuilder? pipelineBuilder,
        FeatureCollection features, bool ownsProvider)
    {
        ServiceProvider = serviceProvider;
        Features = features;
        _ownsProvider = ownsProvider;

        if (pipelineBuilder is not null)
            Pipeline = pipelineBuilder.Build();
    }

    /// <summary>
    /// 应用服务提供器。外部容器模式（UseXiHan/AddXiHan）下为 null，通过宿主获取服务。
    /// </summary>
    public IServiceProvider? ServiceProvider { get; }

    /// <summary>
    /// 已注册的特性集合。
    /// </summary>
    public FeatureCollection Features { get; }

    /// <summary>
    /// 管道执行入口。
    /// </summary>
    public PipelineHandler? Pipeline { get; }

    /// <summary>
    /// 独立创建应用构建器。
    /// </summary>
    public static XiHanAppBuilder CreateBuilder(string[]? args = null) => new(args);

    /// <summary>
    /// 通过管道执行上下文。
    /// </summary>
    public async Task ExecuteAsync(PipelineContext context)
    {
        if (Pipeline is null)
            throw new InvalidOperationException("No pipeline configured.");
        await Pipeline(context);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsProvider)
        {
            if (ServiceProvider is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
