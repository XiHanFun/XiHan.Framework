// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory.Tests.Fixtures;

/// <summary>
/// 成功时交回空载荷的操作，照插件代理的写法
/// </summary>
/// <remarks>
/// 插件交回的空载荷表示没有响应体（<c>spec/plugin/invocation.md</c> §1）：响应对象是原样搬运的字节，编码与回放的解码都不经 JSON。类型化的操作写不出空载荷，用例只能手写描述；请求不解码，执行总是成功并计数
/// </remarks>
/// <param name="id">操作 ID</param>
internal sealed class EmptyResponseOperation(string id)
    : OperationDescriptor(new OperationContract(id), "xihan.test.blanks", typeof(EmptyResponseOperation), typeof(object), typeof(object), null)
{
    /// <summary>
    /// 解码出的请求对象
    /// </summary>
    private static readonly object _request = new();

    /// <summary>
    /// 执行的次数
    /// </summary>
    private int _executions;

    /// <summary>
    /// 执行的次数
    /// </summary>
    public int Executions => Volatile.Read(ref _executions);

    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => Result.Success(_request);

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response)
        => response is byte[] bytes ? bytes : throw new ArgumentException($"The response of operation '{Id}' must be the raw bytes.", nameof(response));

    /// <inheritdoc />
    /// <remarks>
    /// 字节原样包回，空载荷也是（与插件代理相同）
    /// </remarks>
    public override Result<object> DecodeResponse(ReadOnlyMemory<byte> payload) => Result.Success<object>(payload.ToArray());

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request)
    {
        Interlocked.Increment(ref _executions);
        return ValueTask.FromResult(Result.Success<object>(Array.Empty<byte>()));
    }
}
