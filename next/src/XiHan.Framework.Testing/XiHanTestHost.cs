// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Testing;

/// <summary>
/// 测试基座。提供无 HTTP 的管道测试能力。
/// </summary>
public sealed class XiHanTestHost : IAsyncDisposable
{
    private readonly XiHanApp _app;

    internal XiHanTestHost(XiHanApp app) => _app = app;

    /// <summary>
    /// 应用服务提供器。
    /// </summary>
    public IServiceProvider Services => _app.ServiceProvider;

    /// <summary>
    /// 通过管道执行一个测试上下文。
    /// </summary>
    public async Task ExecuteAsync(PipelineContext context)
    {
        if (_app.Pipeline is null)
            throw new InvalidOperationException("No pipeline configured. Call WithPipeline() first.");

        await _app.Pipeline(context);
    }

    /// <summary>
    /// 创建一个测试宿主构建器。
    /// </summary>
    public static TestHostBuilder CreateBuilder() => new();

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>
/// 测试宿主构建器。
/// </summary>
public sealed class TestHostBuilder
{
    private readonly XiHanAppBuilder _builder = new(null);

    /// <summary>
    /// 注册一个测试特性。
    /// </summary>
    public TestHostBuilder UseFeature<TFeature>() where TFeature : IXiHanFeature, new()
    {
        _builder.UseFeature<TFeature>();
        return this;
    }

    /// <summary>
    /// 配置测试管道。
    /// </summary>
    public TestHostBuilder WithPipeline(Action<PipelineBuilder> configure)
    {
        _builder.UsePipeline(configure);
        return this;
    }

    /// <summary>
    /// 构建测试宿主。
    /// </summary>
    public XiHanTestHost Build() => new(_builder.Build());
}
