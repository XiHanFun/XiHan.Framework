// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 请求是任意 JSON 的操作
/// </summary>
/// <remarks>
/// 只用来经解码观察共同的 JSON 读取检查，不会被执行
/// </remarks>
internal sealed class VectorJsonOperation : IOperation<JsonElement, VectorOrder>
{
    /// <summary>
    /// 运行期契约
    /// </summary>
    public static OperationContract Contract { get; } = new("vector-json.check");

    /// <inheritdoc />
    public ValueTask<Result<VectorOrder>> HandleAsync(JsonElement request, InvocationContext context)
        => throw new NotSupportedException("The vector operation only decodes payloads.");
}
