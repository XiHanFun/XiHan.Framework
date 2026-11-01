// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 只读调用的标记：放进调用上下文后，Transaction 阶段按契约开只读工作单元。
/// </summary>
/// <remarks>
/// 只用来让套件的调用"像一个 get 操作"，不会被执行：处理器由套件直接给出，编解码与执行都不走到。
/// </remarks>
internal sealed class ReadOnlyProbeOperation : OperationDescriptor
{
    /// <summary>
    /// 操作 ID：动词 <c>get</c>，契约只读。
    /// </summary>
    public const string ProbeId = "conformance-workflows.get";

    /// <summary>
    /// 创建。
    /// </summary>
    public ReadOnlyProbeOperation()
        : base(new OperationContract(ProbeId), "xihan.workflow.conformance", typeof(ReadOnlyProbeOperation), typeof(object), typeof(object), null)
    {
    }

    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => throw new NotSupportedException("The conformance probe operation is never decoded.");

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response) => throw new NotSupportedException("The conformance probe operation is never encoded.");

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request) => throw new NotSupportedException("The conformance probe operation is never executed.");
}
