// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 只带契约的操作描述
/// </summary>
/// <remarks>
/// 审计中间件只看当前操作的 ID、权限码与策略名。直接调用中间件的回放不登记也不执行操作，编解码与执行入口不会被用到，调用即抛出
/// </remarks>
/// <param name="contract">操作契约</param>
internal sealed class ContractOnlyOperationDescriptor(OperationContract contract)
    : OperationDescriptor(contract, VectorsOwner, typeof(ContractOnlyOperationDescriptor), typeof(object), typeof(object), schema: null)
{
    /// <summary>
    /// 归属的特性清单 ID
    /// </summary>
    private const string VectorsOwner = "auditing.vectors";

    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => throw Unused();

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response) => throw Unused();

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request) => throw Unused();

    /// <summary>
    /// 编解码或执行入口被调用时的异常
    /// </summary>
    private static NotSupportedException Unused()
        => new("The descriptor only carries a contract; it is never decoded, encoded or executed.");
}
