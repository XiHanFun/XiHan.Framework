// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 可写的套件操作
/// </summary>
/// <remarks>
/// 动词 <c>run</c>，契约不是只读：Transaction 阶段为它开可写工作单元。处理器执行用例登记的脚本
/// </remarks>
/// <param name="scripts">脚本登记处</param>
[Operation(Id)]
internal sealed class WriteScriptOperation(ConformanceScripts scripts) : IOperation<ConformanceScriptRequest, ConformanceScriptResponse>
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public const string Id = "conformance-scripts.run";

    /// <summary>
    /// 脚本登记处
    /// </summary>
    private readonly ConformanceScripts _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));

    /// <inheritdoc />
    public ValueTask<Result<ConformanceScriptResponse>> HandleAsync(ConformanceScriptRequest request, InvocationContext context) => _scripts.RunAsync(request, context);
}
