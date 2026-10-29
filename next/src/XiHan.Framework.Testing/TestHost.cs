// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 内存中的测试宿主。
/// </summary>
/// <remarks>
/// 自建服务集合与配置，装配并启动 <see cref="App"/>，调用直接走管道，不经过任何协议。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class TestHost : IAsyncDisposable
{
    /// <summary>
    /// 根服务提供器。
    /// </summary>
    /// <remarks>
    /// 宿主释放时一并释放。
    /// </remarks>
    private readonly ServiceProvider _provider;

    /// <summary>
    /// 是否已释放。
    /// </summary>
    /// <remarks>
    /// 释放后拒绝再发起调用。
    /// </remarks>
    private bool _disposed;

    /// <summary>
    /// 以已启动的应用、其服务提供器与配置创建宿主。
    /// </summary>
    /// <remarks>
    /// 只由 <see cref="StartAsync"/> 调用。
    /// </remarks>
    private TestHost(ServiceProvider provider, App app, IConfiguration configuration)
    {
        _provider = provider;
        App = app;
        Configuration = configuration;
    }

    /// <summary>
    /// 应用实例。
    /// </summary>
    public App App { get; }

    /// <summary>
    /// 根服务提供器。
    /// </summary>
    public IServiceProvider Services => _provider;

    /// <summary>
    /// 配置。
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 装配并启动应用。
    /// </summary>
    /// <param name="configure">应用配置。</param>
    /// <param name="configuration">内存配置键值。</param>
    /// <param name="services">附加服务注册。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task<TestHost> StartAsync(
        Action<AppBuilder> configure,
        IEnumerable<KeyValuePair<string, string?>>? configuration = null,
        Action<IServiceCollection>? services = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var config = new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build();
        var collection = new ServiceCollection();
        services?.Invoke(collection);

        var builder = new AppBuilder(collection, config);
        configure(builder);
        builder.Build();

        // ValidateScopes：在根容器解析 Scoped 服务直接报错，让错误的生存期注册在测试里暴露
        var provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var app = provider.GetRequiredService<App>();
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 启动失败时没有宿主实例可供调用方释放，这里先释放容器再抛出，避免已创建的单例泄漏
            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new TestHost(provider, app, config);
    }

    /// <summary>
    /// 在新的服务作用域内创建调用上下文并走完整管道。
    /// </summary>
    public async ValueTask<Result> InvokeAsync(
        PipelineStep handler,
        DateTimeOffset? deadline = null,
        IReadOnlyDictionary<string, string>? baggage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 每次调用一个独立作用域，与真实宿主里一请求一作用域的语义一致
        await using var scope = _provider.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, deadline: deadline, baggage: baggage);
        return await App.InvokeAsync(context, handler).ConfigureAwait(false);
    }

    /// <summary>
    /// 以 XGI 消息调用操作，经完整管道。
    /// </summary>
    /// <remarks>
    /// 每次调用一个独立作用域。
    /// </remarks>
    public async ValueTask<OperationResponse> InvokeOperationRawAsync(
        OperationRequest request,
        IReadOnlyDictionary<string, string>? baggage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await using var scope = _provider.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, baggage: baggage);
        return await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, request).ConfigureAwait(false);
    }

    /// <summary>
    /// 类型化调用操作，经完整管道。
    /// </summary>
    /// <remarks>
    /// 每次调用一个独立作用域。
    /// </remarks>
    public async ValueTask<Result<TResponse>> InvokeOperationAsync<TRequest, TResponse>(
        string operationId,
        TRequest request,
        IReadOnlyDictionary<string, string>? baggage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await using var scope = _provider.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, baggage: baggage);
        return await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync<TRequest, TResponse>(context, operationId, request).ConfigureAwait(false);
    }

    /// <summary>
    /// 停止应用并释放容器。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // 幂等：重复释放直接返回
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await App.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _provider.DisposeAsync().ConfigureAwait(false);
    }
}
