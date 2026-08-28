// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.VectorData;
using System.Net.Sockets;
using XiHan.Framework.AI.Rag;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.AI.Tests;

/// <summary>
/// 向量库异常翻译测试。
/// </summary>
/// <remarks>
/// 覆盖三条契约：连接类故障翻译成依赖不可用、请求类故障原样抛出、流式结果在枚举时才翻译。
/// </remarks>
public sealed class VectorStoreOperationTests
{
    /// <summary>
    /// 连接被拒必须翻译成依赖不可用，并保留原始异常供日志。
    /// </summary>
    /// <remarks>这正是 Qdrant 未启动时的形态：VectorStoreException 包着 SocketException。</remarks>
    [Fact]
    public async Task ExecuteAsync_ConnectionRefusedShouldTranslate()
    {
        var inner = BuildUnavailable();

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => VectorStoreOperation.ExecuteAsync(() => Task.FromException<bool>(inner)));

        Assert.Contains("向量库不可达", exception.Message, StringComparison.Ordinal);
        Assert.Same(inner, exception.InnerException);
        // 文案不得泄露主机、端口等拓扑信息。
        Assert.DoesNotContain("localhost", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("6334", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 无返回值重载同样翻译。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_VoidOverloadShouldTranslate()
    {
        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => VectorStoreOperation.ExecuteAsync(() => Task.FromException(BuildUnavailable())));

        Assert.Contains("向量库不可达", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 请求本身的问题必须原样抛出，不能被报成依赖不可用。
    /// </summary>
    /// <remarks>
    /// 维度不匹配、过滤表达式非法这类故障重试不会好，翻译成 503 会掩盖真正的缺陷，
    /// 也会让告警把代码问题误判成基础设施问题。
    /// </remarks>
    [Fact]
    public async Task ExecuteAsync_NonConnectivityFailureShouldPassThrough()
    {
        var original = new VectorStoreException("向量维度不匹配", new InvalidOperationException("dimension mismatch"));

        var thrown = await Assert.ThrowsAsync<VectorStoreException>(
            () => VectorStoreOperation.ExecuteAsync(() => Task.FromException<bool>(original)));

        Assert.Same(original, thrown);
    }

    /// <summary>
    /// 没有内部异常的向量库异常也不应被误判为连接故障。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithoutInnerExceptionShouldPassThrough()
    {
        var original = new VectorStoreException("未知失败");

        _ = await Assert.ThrowsAsync<VectorStoreException>(
            () => VectorStoreOperation.ExecuteAsync(() => Task.FromException<bool>(original)));
    }

    /// <summary>
    /// 流式结果的异常在枚举时才抛出，包装必须覆盖枚举过程而不是构造调用。
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldTranslateDuringEnumeration()
    {
        // 构造包装器本身不得抛出——异常只应在真正推进序列时出现。
        var stream = VectorStoreOperation.ExecuteStreamAsync(FailingStream());

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(async () =>
        {
            await foreach (var _ in stream)
            {
                // 故意不做任何事：第一次推进就应当失败。
            }
        });

        Assert.Contains("向量库不可达", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 正常流式结果必须逐项原样转发。
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldForwardItems()
    {
        var items = new List<int>();

        await foreach (var item in VectorStoreOperation.ExecuteStreamAsync(SuccessfulStream()))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
    }

    /// <summary>
    /// 构造 Qdrant 未启动时的真实异常形态。
    /// </summary>
    private static VectorStoreException BuildUnavailable()
    {
        // 连接器把传输层异常包进 VectorStoreException；这里用 SocketException 复现「目标计算机积极拒绝」。
        return new VectorStoreException(
            "Call to vector store failed.",
            new SocketException((int)SocketError.ConnectionRefused));
    }

    private static async IAsyncEnumerable<int> FailingStream()
    {
        await Task.Yield();
        throw BuildUnavailable();
#pragma warning disable CS0162 // 编译器要求迭代器有 yield，实际不可达
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<int> SuccessfulStream()
    {
        await Task.Yield();
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
