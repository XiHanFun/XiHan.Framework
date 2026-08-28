// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Documents;
using XiHan.Framework.SearchEngines.Abstractions.Indexing;
using XiHan.Framework.SearchEngines.Abstractions.Querying;
using XiHan.Framework.SearchEngines.Abstractions.Results;

namespace XiHan.Framework.SearchEngines.Abstractions;

/// <summary>
/// 搜索引擎
/// </summary>
/// <remarks>
/// 契约按「Elasticsearch 与 PostgreSQL 全文检索都能落地」的交集设计，签名中不出现任一后端的类型。
/// <para>
/// 投递语义：索引与删除均为幂等的按标识覆盖/移除；写入对检索可见的延迟由实现决定，
/// 需要立即可见时调用 <see cref="RefreshAsync"/>。
/// </para>
/// </remarks>
public interface ISearchEngine
{
    /// <summary>
    /// 判断索引是否存在
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> IndexExistsAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建索引，已存在时不做任何事
    /// </summary>
    /// <param name="definition">索引定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际创建了索引</returns>
    Task<bool> CreateIndexAsync(SearchIndexDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除索引，不存在时不做任何事
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际删除了索引</returns>
    Task<bool> DeleteIndexAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入单个文档，标识已存在时整体覆盖
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="document">文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task IndexAsync<TDocument>(string index, SearchDocument<TDocument> document, CancellationToken cancellationToken = default)
        where TDocument : class;

    /// <summary>
    /// 批量写入文档，标识已存在时整体覆盖
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="documents">文档集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际写入的文档数</returns>
    Task<int> IndexManyAsync<TDocument>(string index, IEnumerable<SearchDocument<TDocument>> documents, CancellationToken cancellationToken = default)
        where TDocument : class;

    /// <summary>
    /// 按标识删除文档
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="id">文档标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际删除了文档</returns>
    Task<bool> DeleteAsync(string index, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标识获取文档
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="id">文档标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档，不存在时为空</returns>
    Task<TDocument?> GetAsync<TDocument>(string index, string id, CancellationToken cancellationToken = default)
        where TDocument : class;

    /// <summary>
    /// 检索
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="request">检索请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索结果</returns>
    Task<SearchResult<TDocument>> SearchAsync<TDocument>(SearchRequest request, CancellationToken cancellationToken = default)
        where TDocument : class;

    /// <summary>
    /// 使此前的写入立即对检索可见
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task RefreshAsync(string index, CancellationToken cancellationToken = default);
}
