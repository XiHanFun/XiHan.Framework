// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 操作 <c>cli.config</c>：查配置节的键、类型、缺省值与取值范围
/// </summary>
/// <remarks>
/// 发布为 MCP tool <c>xihan_framework_config</c>，见 <see cref="KnowledgeQueries.Config"/>
/// </remarks>
/// <param name="knowledge">离线知识库</param>
internal sealed class ConfigOperation(FrameworkKnowledge knowledge) : IOperation<ConfigRequest, ConfigResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<ConfigResponse>> HandleAsync(ConfigRequest request, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(KnowledgeQueries.Config(knowledge, request.Query));
    }
}
