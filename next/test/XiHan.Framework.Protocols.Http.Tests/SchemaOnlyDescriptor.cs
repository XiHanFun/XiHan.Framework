// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 没有类型化序列化元数据、只带（可选）Schema 的描述：模拟插件代理操作，验证路由推导回落到 Schema 判定 <c>id</c>。
/// </summary>
internal sealed class SchemaOnlyDescriptor(string id, OperationSchema? schema)
    : OperationDescriptor(new OperationContract(id), "xihan.test.orders", typeof(SchemaOnlyDescriptor), typeof(object), typeof(object), schema)
{
    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => throw new NotSupportedException();

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response) => throw new NotSupportedException();

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request) => throw new NotSupportedException();
}
