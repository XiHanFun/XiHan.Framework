// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 不带载体的假运行时：一份清单、一次握手声明，投递由用例接管
/// </summary>
/// <remarks>
/// 名册只经 <see cref="IPluginRuntime"/> 看运行时，用它就能单独核对名册的登记、撤回、投递与健康条目
/// </remarks>
internal sealed class StubPluginRuntime : IPluginRuntime
{
    /// <summary>
    /// 收到的投递数
    /// </summary>
    private int _deliveries;

    /// <summary>
    /// 创建假运行时
    /// </summary>
    /// <param name="manifestId">清单 ID</param>
    /// <param name="operation">握手声明的一个操作</param>
    /// <param name="topics">以竞争方式订阅的主题，不限类型</param>
    public StubPluginRuntime(string manifestId, string operation, params string[] topics)
    {
        Manifest = TestManifests.Create(manifestId, kind: ManifestKind.Plugin);
        Handshake = new HandshakeInfo(
            1,
            "{}",
            [new ProxyOperation(new OperationContract(operation), null, false)],
            topics.Select(static topic => new PluginSubscription(topic, null, SubscriptionMode.Competing)).ToList(),
            [],
            "stub");
    }

    /// <inheritdoc />
    public Manifest Manifest { get; }

    /// <inheritdoc />
    public HandshakeInfo? Handshake { get; }

    /// <inheritdoc />
    public LifecycleState State { get; set; } = LifecycleState.Ready;

    /// <inheritdoc />
    public Error? Error { get; set; }

    /// <inheritdoc />
    public PluginStatus Status => new(Manifest, State, Error);

    /// <summary>
    /// 一次投递怎样结束
    /// </summary>
    /// <remarks>
    /// 缺省立即成功
    /// </remarks>
    public Func<Task<Result>> OnDeliver { get; set; } = static () => Task.FromResult(Result.Success());

    /// <summary>
    /// 收到的投递数
    /// </summary>
    public int Deliveries => Volatile.Read(ref _deliveries);

    /// <inheritdoc />
    public async ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
    {
        Interlocked.Increment(ref _deliveries);
        return await OnDeliver().ConfigureAwait(false);
    }

    /// <summary>
    /// 一次调用怎样结束
    /// </summary>
    /// <remarks>
    /// 缺省立即成功、返回操作 ID
    /// </remarks>
    public Func<string, InvocationContext, ValueTask<Result<object>>> OnInvoke { get; set; } = static (operation, _) => ValueTask.FromResult(Result.Success<object>(operation));

    /// <inheritdoc />
    public ValueTask<Result<object>> InvokeAsync(string operation, ReadOnlyMemory<byte> payload, InvocationContext context)
        => OnInvoke(operation, context);
}
