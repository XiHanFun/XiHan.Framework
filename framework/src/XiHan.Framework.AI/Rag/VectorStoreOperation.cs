// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.VectorData;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 向量库调用的统一异常翻译。
/// </summary>
/// <remarks>
/// <para>
/// 向量库连接器把底层传输异常（gRPC <c>RpcException</c>、<see cref="SocketException"/> 等）统一包成
/// <see cref="VectorStoreException"/>。直接冒泡的话，调用方拿到的是一坨驱动堆栈：
/// 接口返回 500 且不含任何可操作信息，文档摄取则把裸堆栈截断后写进错误字段。
/// </para>
/// <para>
/// 这里把「连不上」翻译成 <see cref="ServiceUnavailableException"/>（映射 503），原始异常保留在
/// <c>InnerException</c> 供日志排查。翻译不是吞异常：流程照样中断，只是失败可读、可分类、可告警。
/// </para>
/// <para>
/// 只翻译连接类故障。查询语法错误、维度不匹配等<b>请求本身的问题</b>原样抛出——
/// 那类问题重试不会好，把它们也报成 503 会掩盖真正的缺陷。
/// </para>
/// <para>
/// 替换默认检索器或摄取器的自定义实现应同样经由本类调用向量库，以获得一致的失败语义。
/// </para>
/// </remarks>
public static class VectorStoreOperation
{
    /// <summary>
    /// 面向运维的统一文案；刻意不含主机、端口与凭据，拓扑信息只进日志不进响应。
    /// </summary>
    private const string UnavailableMessage = "向量库不可达，请检查向量库服务是否运行以及 RAG 连接配置。";

    /// <summary>
    /// 执行一次有返回值的向量库调用。
    /// </summary>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="operation">向量库调用。</param>
    /// <returns>调用结果。</returns>
    /// <exception cref="ServiceUnavailableException">向量库不可达。</exception>
    public static async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (VectorStoreException exception) when (IsUnavailable(exception))
        {
            throw new ServiceUnavailableException(UnavailableMessage, innerException: exception);
        }
    }

    /// <summary>
    /// 执行一次无返回值的向量库调用。
    /// </summary>
    /// <param name="operation">向量库调用。</param>
    /// <exception cref="ServiceUnavailableException">向量库不可达。</exception>
    public static async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (VectorStoreException exception) when (IsUnavailable(exception))
        {
            throw new ServiceUnavailableException(UnavailableMessage, innerException: exception);
        }
    }

    /// <summary>
    /// 包装向量库的流式结果。
    /// </summary>
    /// <typeparam name="TItem">元素类型。</typeparam>
    /// <param name="source">向量库返回的异步序列。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>逐项转发的异步序列。</returns>
    /// <remarks>
    /// 异步序列的异常在<b>枚举时</b>才抛出，包住构造调用没有意义，必须包住枚举过程本身。
    /// </remarks>
    /// <exception cref="ServiceUnavailableException">向量库不可达。</exception>
    public static async IAsyncEnumerable<TItem> ExecuteStreamAsync<TItem>(
        IAsyncEnumerable<TItem> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (VectorStoreException exception) when (IsUnavailable(exception))
                {
                    throw new ServiceUnavailableException(UnavailableMessage, innerException: exception);
                }

                if (!moved)
                {
                    yield break;
                }

                // yield 不能放在 try/catch 内，因此推进与产出分开写。
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// 判断异常链上是否存在连接类故障。
    /// </summary>
    /// <param name="exception">向量库异常。</param>
    /// <returns>属于「服务不可达」时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 按异常类型逐层判定，不匹配消息文本：驱动版本与系统语言都会改变文案，字符串匹配不可靠。
    /// <see cref="SocketException"/> 覆盖连接被拒与主机不可达；
    /// gRPC 的 <c>RpcException</c> 按类型名判定，避免为此引入 Grpc.Core 依赖。
    /// </remarks>
    private static bool IsUnavailable(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException or HttpRequestException)
            {
                return true;
            }

            if (current.GetType().FullName is "Grpc.Core.RpcException")
            {
                return true;
            }
        }

        return false;
    }
}
