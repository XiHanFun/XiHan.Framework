// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 宿主侧的操作：应答写成给定的原始字节
/// </summary>
/// <remarks>
/// 类型化的操作总是写出合法的 JSON；用例要一份不合法的应答经插件回调转回宿主，只能绕过序列化直接给字节。请求不解码，执行总是成功
/// </remarks>
/// <param name="id">操作 ID</param>
/// <param name="bytes">写出的应答字节</param>
internal sealed class RawResponseOperation(string id, byte[] bytes)
    : OperationDescriptor(new OperationContract(id), "xihan.test.raw", typeof(RawResponseOperation), typeof(object), typeof(object), null)
{
    /// <summary>
    /// 执行成功时的响应对象
    /// </summary>
    private static readonly object _response = new();

    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => Result.Success<object>(_response);

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response) => bytes;

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request) => ValueTask.FromResult(Result.Success<object>(_response));
}
