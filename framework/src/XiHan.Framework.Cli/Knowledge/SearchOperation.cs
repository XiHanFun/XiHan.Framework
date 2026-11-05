// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 操作 <c>cli.search</c>：检索文档，或取一页全文
/// </summary>
/// <remarks>
/// 发布为 MCP tool <c>xihan_framework_search</c>，见 <see cref="KnowledgeQueries.Search"/>
/// </remarks>
/// <param name="knowledge">离线知识库</param>
internal sealed class SearchOperation(FrameworkKnowledge knowledge) : IOperation<SearchRequest, SearchResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<SearchResponse>> HandleAsync(SearchRequest request, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(KnowledgeQueries.Search(knowledge, request.Query, request.Page, request.Limit));
    }
}
