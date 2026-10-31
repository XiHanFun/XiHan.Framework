// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 操作响应过滤器。
/// </summary>
/// <remarks>
/// XGI 与类型化、管道内外四个入口都过滤；按登记顺序串联、作用域服务可用；失败的调用不过滤；返回类型不对是意外错误（ADR-0061）。
/// </remarks>
public sealed class OperationResponseFilterTests
{
    [Fact]
    public async Task Filters_RunInOrder_ForEveryEntryPoint()
    {
        var (provider, app) = await StartAsync(static services =>
        {
            services.AddScoped<FilterLog>();
            services.AddScoped<IOperationResponseFilter, UpperFilter>();
            services.AddSingleton<IOperationResponseFilter, ExclaimFilter>();
        });
        await using var _ = provider;
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        // XGI：编码出去的载荷与上下文里的响应对象都是过滤后的；作用域服务在调用的作用域里解析
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = Hosts.Context(scope.ServiceProvider);
            var response = await invoker.InvokeAsync(context, new OperationRequest(EchoOperation.Id, Encoding.UTF8.GetBytes("""{"text":"hi","repeat":2}""")));
            using var document = JsonDocument.Parse(response.Payload);
            Assert.Equal("HIHI!", document.RootElement.GetProperty("echo").GetString());
            Assert.Equal("HIHI!", ((EchoResponse)context.GetOperationResponse().ValueOrThrow()).Echo);
            Assert.Equal([EchoOperation.Id], scope.ServiceProvider.GetRequiredService<FilterLog>().Seen);
        }

        // 类型化调用，以及已在管道里的两种执行
        await using (var scope = provider.CreateAsyncScope())
        {
            Assert.Equal("XX!", (await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("x", 2))).ValueOrThrow().Echo);
            OperationResponse? inner = null;
            Result<EchoResponse> typed = default;
            var outcome = await app.InvokeAsync(Hosts.Context(scope.ServiceProvider), async ctx =>
            {
                inner = await invoker.ExecuteAsync(ctx, new OperationRequest(EchoOperation.Id, Encoding.UTF8.GetBytes("""{"text":"in"}""")));
                typed = await invoker.ExecuteAsync<EchoRequest, EchoResponse>(ctx, EchoOperation.Id, new EchoRequest("side"));
            });
            Assert.True(outcome.IsSuccess);
            using var document = JsonDocument.Parse(inner!.Payload);
            Assert.Equal("IN!", document.RootElement.GetProperty("echo").GetString());
            Assert.Equal("SIDE!", typed.ValueOrThrow().Echo);
            Assert.Equal(3, scope.ServiceProvider.GetRequiredService<FilterLog>().Seen.Count);
        }

        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailedCalls_AreNotFiltered()
    {
        var (provider, app) = await StartAsync(static services =>
        {
            services.AddScoped<FilterLog>();
            services.AddScoped<IOperationResponseFilter, UpperFilter>();
        });
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();

        var failed = await provider.GetRequiredService<IOperationInvoker>().InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest(string.Empty));
        Assert.Equal("ECHOES_VALIDATION_001", failed.Error?.Code);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<FilterLog>().Seen);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FilterReturningAnotherType_IsAnUnexpectedError()
    {
        var (provider, app) = await StartAsync(static services => services.AddSingleton<IOperationResponseFilter, WrongTypeFilter>());
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();

        var response = await provider.GetRequiredService<IOperationInvoker>().InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Encoding.UTF8.GetBytes("""{"text":"hi"}""")));
        Assert.Equal(KernelErrors.Unexpected, response.Outcome.Error?.Code);
        Assert.True(response.Payload.IsEmpty);
        await app.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 启动带回显操作与给定服务的应用。
    /// </summary>
    private static async Task<(ServiceProvider Provider, App App)> StartAsync(Action<IServiceCollection> services)
    {
        var (provider, app) = Hosts.BuildApp(b =>
        {
            b.UseFeature(new EchoFeature());
            services(b.Services);
        });
        await app.StartAsync(CancellationToken.None);
        return (provider, app);
    }

    /// <summary>
    /// 作用域内被过滤的操作。
    /// </summary>
    private sealed class FilterLog
    {
        /// <summary>
        /// 依次过滤过的操作 ID。
        /// </summary>
        public List<string> Seen { get; } = [];
    }

    /// <summary>
    /// 作用域过滤器：转大写，记下操作。
    /// </summary>
    private sealed class UpperFilter(FilterLog log) : IOperationResponseFilter
    {
        /// <inheritdoc />
        public ValueTask<object> FilterAsync(InvocationContext context, OperationDescriptor operation, object response)
        {
            log.Seen.Add(operation.Id);
            var echo = (EchoResponse)response;
            return ValueTask.FromResult<object>(echo with { Echo = echo.Echo.ToUpperInvariant() });
        }
    }

    /// <summary>
    /// 单例过滤器：在前一个的结果后加感叹号。
    /// </summary>
    private sealed class ExclaimFilter : IOperationResponseFilter
    {
        /// <inheritdoc />
        public ValueTask<object> FilterAsync(InvocationContext context, OperationDescriptor operation, object response)
            => ValueTask.FromResult<object>((EchoResponse)response with { Echo = ((EchoResponse)response).Echo + "!" });
    }

    /// <summary>
    /// 返回别的类型的过滤器。
    /// </summary>
    private sealed class WrongTypeFilter : IOperationResponseFilter
    {
        /// <inheritdoc />
        public ValueTask<object> FilterAsync(InvocationContext context, OperationDescriptor operation, object response) => ValueTask.FromResult<object>("not a response");
    }
}
