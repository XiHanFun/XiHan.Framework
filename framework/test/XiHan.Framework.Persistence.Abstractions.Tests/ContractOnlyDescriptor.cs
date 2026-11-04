// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 只带契约的操作描述
/// </summary>
/// <remarks>
/// 放进调用上下文，验证 Transaction 阶段按契约的只读标志开工作单元；编解码与执行都不会被调用
/// </remarks>
/// <param name="contract">契约</param>
internal sealed class ContractOnlyDescriptor(OperationContract contract)
    : OperationDescriptor(contract, "xihan.test.orders", typeof(ContractOnlyDescriptor), typeof(object), typeof(object), null)
{
    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => throw new NotSupportedException();

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response) => throw new NotSupportedException();

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request) => throw new NotSupportedException();
}
