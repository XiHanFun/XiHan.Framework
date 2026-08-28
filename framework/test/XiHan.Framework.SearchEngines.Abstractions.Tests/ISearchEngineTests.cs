// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.SearchEngines.Abstractions.Documents;
using XiHan.Framework.SearchEngines.Abstractions.Indexing;
using XiHan.Framework.SearchEngines.Abstractions.Querying;
using XiHan.Framework.SearchEngines.Abstractions.Results;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests;

/// <summary>
/// 搜索引擎契约签名的测试
/// </summary>
/// <remarks>
/// 行为一致性（幂等覆盖、过滤与关系、分页总数等）由 XiHan.Framework.SearchEngines.Tests
/// 中的实现契约用例覆盖，本类不重复。这里只锁抽象包自身能保证的东西：
/// 方法集合不漂移、全异步、取消令牌一律可省略且排在末位、泛型文档参数约束为引用类型，
/// 以及「未命中返回空、空结果可复用」这两处可空性约定确实能被实现方满足。
/// </remarks>
public class ISearchEngineTests
{
    /// <summary>
    /// 契约方法集合不漂移
    /// </summary>
    /// <remarks>
    /// 新增成员会同时打断所有已有实现，属破坏性变更，必须显式认账。
    /// </remarks>
    [Fact]
    public void Members_AreExactlyTheDeclaredSurface()
    {
        var names = typeof(ISearchEngine).GetMethods()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "CreateIndexAsync",
                "DeleteAsync",
                "DeleteIndexAsync",
                "GetAsync",
                "IndexAsync",
                "IndexExistsAsync",
                "IndexManyAsync",
                "RefreshAsync",
                "SearchAsync"
            },
            names);
    }

    /// <summary>
    /// 所有契约方法都返回 Task 或 Task 泛型
    /// </summary>
    /// <remarks>
    /// 后端一律是网络调用，契约不留同步口子，避免实现方用 .Result 兜底。
    /// </remarks>
    [Fact]
    public void Members_AllReturnTask()
    {
        Assert.All(typeof(ISearchEngine).GetMethods(), method =>
        {
            var returnType = method.ReturnType;
            var isAwaitable = returnType == typeof(Task)
                || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));

            Assert.True(isAwaitable, $"{method.Name} 未返回 Task 或 Task<T>");
        });
    }

    /// <summary>
    /// 所有契约方法的末位参数都是可省略的取消令牌
    /// </summary>
    [Fact]
    public void Members_AllTakeOptionalCancellationTokenLast()
    {
        Assert.All(typeof(ISearchEngine).GetMethods(), method =>
        {
            var parameters = method.GetParameters();

            Assert.NotEmpty(parameters);

            var last = parameters[^1];

            Assert.Equal(typeof(CancellationToken), last.ParameterType);
            Assert.True(last.HasDefaultValue, $"{method.Name} 的取消令牌不可省略");
        });
    }

    /// <summary>
    /// 泛型方法的文档类型参数一律约束为引用类型
    /// </summary>
    /// <remarks>
    /// 约束放宽会让值类型文档进来，取回未命中时无法用 null 表达。
    /// </remarks>
    [Fact]
    public void GenericMembers_ConstrainDocumentToReferenceType()
    {
        var generics = typeof(ISearchEngine).GetMethods()
            .Where(method => method.IsGenericMethodDefinition)
            .ToArray();

        Assert.Equal(4, generics.Length);
        Assert.All(generics, method =>
        {
            var arguments = method.GetGenericArguments();

            Assert.Single(arguments);
            Assert.Equal("TDocument", arguments[0].Name);
            Assert.True(
                arguments[0].GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint),
                $"{method.Name} 的文档类型参数未约束为引用类型");
        });
    }

    /// <summary>
    /// 实现方可省略取消令牌、以空表达未命中并复用空结果
    /// </summary>
    [Fact]
    public async Task Implementation_OmitsCancellationToken_AndExpressesMissAsNull()
    {
        ISearchEngine engine = new NoopSearchEngine();

        var written = await engine.IndexManyAsync<SearchTestDocument>("articles", [new SearchDocument<SearchTestDocument>("1", new SearchTestDocument())]);
        var missing = await engine.GetAsync<SearchTestDocument>("articles", "404");
        var result = await engine.SearchAsync<SearchTestDocument>(new SearchRequest("articles"));

        Assert.Equal(1, written);
        Assert.Null(missing);
        Assert.Same(SearchResult<SearchTestDocument>.Empty, result);
    }

    /// <summary>
    /// 最小实现，只用于验证契约可被实现且签名可省略取消令牌
    /// </summary>
    private sealed class NoopSearchEngine : ISearchEngine
    {
        /// <summary>
        /// 判断索引是否存在
        /// </summary>
        /// <param name="index">索引名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public Task<bool> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// 创建索引
        /// </summary>
        /// <param name="definition">索引定义</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否实际创建</returns>
        public Task<bool> CreateIndexAsync(SearchIndexDefinition definition, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// 删除索引
        /// </summary>
        /// <param name="index">索引名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否实际删除</returns>
        public Task<bool> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// 写入单个文档
        /// </summary>
        /// <typeparam name="TDocument">文档类型</typeparam>
        /// <param name="index">索引名</param>
        /// <param name="document">文档</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task IndexAsync<TDocument>(string index, SearchDocument<TDocument> document, CancellationToken cancellationToken = default)
            where TDocument : class
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 批量写入文档
        /// </summary>
        /// <typeparam name="TDocument">文档类型</typeparam>
        /// <param name="index">索引名</param>
        /// <param name="documents">文档集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际写入的文档数</returns>
        public Task<int> IndexManyAsync<TDocument>(string index, IEnumerable<SearchDocument<TDocument>> documents, CancellationToken cancellationToken = default)
            where TDocument : class
        {
            return Task.FromResult(documents.Count());
        }

        /// <summary>
        /// 按标识删除文档
        /// </summary>
        /// <param name="index">索引名</param>
        /// <param name="id">文档标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否实际删除</returns>
        public Task<bool> DeleteAsync(string index, string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// 按标识获取文档
        /// </summary>
        /// <typeparam name="TDocument">文档类型</typeparam>
        /// <param name="index">索引名</param>
        /// <param name="id">文档标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文档，不存在时为空</returns>
        public Task<TDocument?> GetAsync<TDocument>(string index, string id, CancellationToken cancellationToken = default)
            where TDocument : class
        {
            return Task.FromResult<TDocument?>(null);
        }

        /// <summary>
        /// 检索
        /// </summary>
        /// <typeparam name="TDocument">文档类型</typeparam>
        /// <param name="request">检索请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>检索结果</returns>
        public Task<SearchResult<TDocument>> SearchAsync<TDocument>(SearchRequest request, CancellationToken cancellationToken = default)
            where TDocument : class
        {
            return Task.FromResult(SearchResult<TDocument>.Empty);
        }

        /// <summary>
        /// 使此前的写入立即对检索可见
        /// </summary>
        /// <param name="index">索引名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task RefreshAsync(string index, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
