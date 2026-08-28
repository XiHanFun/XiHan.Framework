// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.SearchEngines.Documents;
using XiHan.Framework.SearchEngines.Elasticsearch.Options;
using XiHan.Framework.SearchEngines.Indexing;
using XiHan.Framework.SearchEngines.Querying;
using XiHan.Framework.SearchEngines.Results;
using HttpMethod = Elastic.Transport.HttpMethod;
using SearchRequest = XiHan.Framework.SearchEngines.Querying.SearchRequest;

namespace XiHan.Framework.SearchEngines.Elasticsearch;

/// <summary>
/// Elasticsearch 搜索引擎
/// </summary>
/// <remarks>
/// 连接、认证与重试交给 Elasticsearch 客户端的传输层，请求体与响应解析由本类以 JSON 直接构造。
/// 这样做的原因是客户端的强类型查询 DSL 在大版本间变动频繁，把它焊进实现会让本包的可用性
/// 跟着客户端版本走；而 Elasticsearch 的 REST 请求体格式在同一大版本内是稳定契约。
/// <para>
/// 与契约的两处已知差异：排序字段须为 Keyword/数值/日期/布尔，Text 字段在 Elasticsearch 上
/// 默认不可排序；关键字检索走 <c>multi_match</c>，其相关度模型与进程内实现的计数式打分不同。
/// </para>
/// </remarks>
public sealed class ElasticsearchSearchEngine : ISearchEngine, ISingletonDependency
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ConcurrentDictionary<string, bool> _verifiedIndexes = new(StringComparer.Ordinal);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">配置</param>
    public ElasticsearchSearchEngine(IOptions<ElasticsearchOptions> options)
    {
        _options = options.Value;
        _client = CreateClient(_options);
    }

    /// <summary>
    /// 判断索引是否存在
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.HEAD, ResolveIndex(index), null, cancellationToken);

        return response.ApiCallDetails.HttpStatusCode == 200;
    }

    /// <summary>
    /// 创建索引，已存在时不做任何事
    /// </summary>
    /// <param name="definition">索引定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际创建了索引</returns>
    public async Task<bool> CreateIndexAsync(SearchIndexDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (await IndexExistsAsync(definition.Name, cancellationToken))
        {
            return false;
        }

        var body = BuildCreateIndexBody(definition);
        var response = await SendAsync(HttpMethod.PUT, ResolveIndex(definition.Name), body, cancellationToken);

        // 并发创建时后到者会收到 resource_already_exists_exception，视为未由本次调用创建
        if (response.ApiCallDetails.HttpStatusCode == 400 &&
            response.Body?.Contains("resource_already_exists_exception", StringComparison.Ordinal) == true)
        {
            return false;
        }

        EnsureSuccess(response, $"创建索引 '{definition.Name}' 失败");

        return true;
    }

    /// <summary>
    /// 删除索引，不存在时不做任何事
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际删除了索引</returns>
    public async Task<bool> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.DELETE, ResolveIndex(index), null, cancellationToken);

        _verifiedIndexes.TryRemove(index, out _);

        if (response.ApiCallDetails.HttpStatusCode == 404)
        {
            return false;
        }

        EnsureSuccess(response, $"删除索引 '{index}' 失败");

        return true;
    }

    /// <summary>
    /// 写入单个文档，标识已存在时整体覆盖
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="document">文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task IndexAsync<TDocument>(string index, SearchDocument<TDocument> document, CancellationToken cancellationToken = default)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(document);

        await EnsureIndexExistsAsync(index, cancellationToken);

        var path = $"{ResolveIndex(index)}/_doc/{Uri.EscapeDataString(document.Id)}";
        var body = JsonSerializer.Serialize(document.Document, SerializerOptions);
        var response = await SendAsync(HttpMethod.PUT, path, body, cancellationToken);

        EnsureSuccess(response, $"写入索引 '{index}' 的文档 '{document.Id}' 失败");
    }

    /// <summary>
    /// 批量写入文档，标识已存在时整体覆盖
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="documents">文档集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际写入的文档数</returns>
    public async Task<int> IndexManyAsync<TDocument>(string index, IEnumerable<SearchDocument<TDocument>> documents, CancellationToken cancellationToken = default)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(documents);

        await EnsureIndexExistsAsync(index, cancellationToken);

        var lines = new List<string>();
        var count = 0;

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines.Add(JsonSerializer.Serialize(new { index = new { _id = document.Id } }, SerializerOptions));
            lines.Add(JsonSerializer.Serialize(document.Document, SerializerOptions));
            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        // 批量接口要求 NDJSON，且必须以换行结尾
        var body = string.Join('\n', lines) + '\n';
        var response = await SendAsync(HttpMethod.POST, $"{ResolveIndex(index)}/_bulk", body, cancellationToken, "application/x-ndjson");

        EnsureSuccess(response, $"批量写入索引 '{index}' 失败");
        EnsureBulkItemsSucceeded(response, index);

        return count;
    }

    /// <summary>
    /// 按标识删除文档
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="id">文档标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际删除了文档</returns>
    public async Task<bool> DeleteAsync(string index, string id, CancellationToken cancellationToken = default)
    {
        var path = $"{ResolveIndex(index)}/_doc/{Uri.EscapeDataString(id)}";
        var response = await SendAsync(HttpMethod.DELETE, path, null, cancellationToken);

        if (response.ApiCallDetails.HttpStatusCode == 404)
        {
            return false;
        }

        EnsureSuccess(response, $"删除索引 '{index}' 的文档 '{id}' 失败");

        return true;
    }

    /// <summary>
    /// 按标识获取文档
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="id">文档标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档，不存在时为空</returns>
    public async Task<TDocument?> GetAsync<TDocument>(string index, string id, CancellationToken cancellationToken = default)
        where TDocument : class
    {
        var path = $"{ResolveIndex(index)}/_doc/{Uri.EscapeDataString(id)}";
        var response = await SendAsync(HttpMethod.GET, path, null, cancellationToken);

        if (response.ApiCallDetails.HttpStatusCode == 404)
        {
            return null;
        }

        EnsureSuccess(response, $"读取索引 '{index}' 的文档 '{id}' 失败");

        using var document = JsonDocument.Parse(response.Body!);

        return document.RootElement.TryGetProperty("_source", out var source)
            ? source.Deserialize<TDocument>(SerializerOptions)
            : null;
    }

    /// <summary>
    /// 检索
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="request">检索请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索结果</returns>
    public async Task<SearchResult<TDocument>> SearchAsync<TDocument>(SearchRequest request, CancellationToken cancellationToken = default)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = BuildSearchBody(request);
        var response = await SendAsync(HttpMethod.POST, $"{ResolveIndex(request.Index)}/_search", body, cancellationToken);

        EnsureSuccess(response, $"检索索引 '{request.Index}' 失败");

        return ParseSearchResponse<TDocument>(response.Body!);
    }

    /// <summary>
    /// 使此前的写入立即对检索可见
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RefreshAsync(string index, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.POST, $"{ResolveIndex(index)}/_refresh", null, cancellationToken);

        EnsureSuccess(response, $"刷新索引 '{index}' 失败");
    }

    /// <summary>
    /// 校验索引已存在，不存在时抛出
    /// </summary>
    /// <remarks>
    /// Elasticsearch 默认会在写入时自动创建索引，但自动创建走的是动态映射：
    /// 本应是 keyword 的字段会被推断成 text，其上的精确过滤随即失效且不报错。
    /// 因此这里显式拦截，与进程内实现保持同一契约——索引必须先经 CreateIndexAsync 建立。
    /// 校验结果按索引名缓存，批量写入每次只多一个 HEAD 请求。
    /// </remarks>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task EnsureIndexExistsAsync(string index, CancellationToken cancellationToken)
    {
        if (_verifiedIndexes.ContainsKey(index))
        {
            return;
        }

        if (!await IndexExistsAsync(index, cancellationToken))
        {
            throw new InvalidOperationException($"索引 '{index}' 不存在，请先创建索引。");
        }

        _verifiedIndexes[index] = true;
    }

    /// <summary>
    /// 创建客户端
    /// </summary>
    /// <param name="options">配置</param>
    /// <returns>客户端</returns>
    private static ElasticsearchClient CreateClient(ElasticsearchOptions options)
    {
        var settings = new ElasticsearchClientSettings(new Uri(options.Uri))
            .RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            settings = settings.Authentication(new ApiKey(options.ApiKey));
        }
        else if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            settings = settings.Authentication(new BasicAuthentication(options.UserName, options.Password ?? string.Empty));
        }

        if (options.AllowUntrustedCertificate)
        {
            settings = settings.ServerCertificateValidationCallback((_, _, _, _) => true);
        }

        return new ElasticsearchClient(settings);
    }

    /// <summary>
    /// 发送请求
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    /// <param name="pathAndQuery">路径</param>
    /// <param name="body">请求体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="contentType">内容类型</param>
    /// <returns>响应</returns>
    private async Task<StringResponse> SendAsync(
        HttpMethod method,
        string pathAndQuery,
        string? body,
        CancellationToken cancellationToken,
        string? contentType = null)
    {
        var postData = body is null ? null : PostData.String(body);
        var configuration = contentType is null
            ? null
            : new RequestConfiguration { ContentType = contentType, Accept = "application/json" };

        return await _client.Transport.RequestAsync<StringResponse>(
            new EndpointPath(method, pathAndQuery),
            postData,
            null,
            configuration,
            cancellationToken);
    }

    /// <summary>
    /// 校验响应成功，失败时抛出并带上服务端返回内容
    /// </summary>
    /// <param name="response">响应</param>
    /// <param name="message">错误信息</param>
    private static void EnsureSuccess(StringResponse response, string message)
    {
        if (response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{message}：HTTP {response.ApiCallDetails.HttpStatusCode}，{response.Body}",
            response.ApiCallDetails.OriginalException);
    }

    /// <summary>
    /// 校验批量写入的每一条都成功
    /// </summary>
    /// <remarks>
    /// 批量接口整体返回 200 的同时可能有个别条目失败，只看 HTTP 状态码会把部分失败当成全部成功。
    /// </remarks>
    /// <param name="response">响应</param>
    /// <param name="index">索引名</param>
    private static void EnsureBulkItemsSucceeded(StringResponse response, string index)
    {
        using var document = JsonDocument.Parse(response.Body!);

        if (!document.RootElement.TryGetProperty("errors", out var errors) || !errors.GetBoolean())
        {
            return;
        }

        throw new InvalidOperationException($"批量写入索引 '{index}' 存在失败条目：{response.Body}");
    }

    /// <summary>
    /// 解析索引名，带上配置的前缀
    /// </summary>
    /// <param name="index">索引名</param>
    /// <returns>实际索引名</returns>
    private string ResolveIndex(string index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);

        return string.IsNullOrEmpty(_options.IndexPrefix) ? index : _options.IndexPrefix + index;
    }

    /// <summary>
    /// 构建创建索引的请求体
    /// </summary>
    /// <param name="definition">索引定义</param>
    /// <returns>请求体</returns>
    private string BuildCreateIndexBody(SearchIndexDefinition definition)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var field in definition.Fields)
        {
            properties[field.Name] = field.Type switch
            {
                SearchFieldType.Text => new Dictionary<string, object> { ["type"] = "text" },
                SearchFieldType.Keyword => new Dictionary<string, object> { ["type"] = "keyword" },
                SearchFieldType.Integer => new Dictionary<string, object> { ["type"] = "long" },
                SearchFieldType.Double => new Dictionary<string, object> { ["type"] = "double" },
                SearchFieldType.Boolean => new Dictionary<string, object> { ["type"] = "boolean" },
                SearchFieldType.DateTime => new Dictionary<string, object> { ["type"] = "date" },
                _ => throw new NotSupportedException($"不支持的字段类型：{field.Type}")
            };
        }

        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["number_of_shards"] = _options.NumberOfShards,
                ["number_of_replicas"] = _options.NumberOfReplicas
            },
            ["mappings"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["properties"] = properties
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    /// <summary>
    /// 构建检索请求体
    /// </summary>
    /// <param name="request">检索请求</param>
    /// <returns>请求体</returns>
    private static string BuildSearchBody(SearchRequest request)
    {
        var must = new List<object>();
        var filter = new List<object>();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            must.Add(request.Fields.Count > 0
                ? new Dictionary<string, object>
                {
                    ["multi_match"] = new Dictionary<string, object>
                    {
                        ["query"] = request.Keyword,
                        ["fields"] = request.Fields
                    }
                }
                : new Dictionary<string, object>
                {
                    ["multi_match"] = new Dictionary<string, object> { ["query"] = request.Keyword }
                });
        }

        foreach (var searchFilter in request.Filters)
        {
            filter.Add(BuildFilterClause(searchFilter));
        }

        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["from"] = request.Skip,
            ["size"] = request.Take,
            ["track_total_hits"] = true,
            ["query"] = new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["must"] = must,
                    ["filter"] = filter
                }
            }
        };

        if (request.Sorts.Count > 0)
        {
            payload["sort"] = request.Sorts
                .Select(sort => (object)new Dictionary<string, object>
                {
                    [sort.Field] = new Dictionary<string, object>
                    {
                        ["order"] = sort.Direction == SearchSortDirection.Ascending ? "asc" : "desc"
                    }
                })
                .ToList();
        }

        if (request.HighlightFields.Count > 0)
        {
            payload["highlight"] = new Dictionary<string, object>
            {
                ["pre_tags"] = new[] { "<em>" },
                ["post_tags"] = new[] { "</em>" },
                ["fields"] = request.HighlightFields.ToDictionary(
                    field => field,
                    _ => (object)new Dictionary<string, object>(),
                    StringComparer.Ordinal)
            };
        }

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    /// <summary>
    /// 构建单个过滤子句
    /// </summary>
    /// <param name="filter">过滤条件</param>
    /// <returns>过滤子句</returns>
    private static object BuildFilterClause(SearchFilter filter)
    {
        return filter.Operator switch
        {
            SearchFilterOperator.Equal => Term(filter.Field, filter.Value!),
            SearchFilterOperator.NotEqual => new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object> { ["must_not"] = new[] { Term(filter.Field, filter.Value!) } }
            },
            SearchFilterOperator.In => new Dictionary<string, object>
            {
                ["terms"] = new Dictionary<string, object> { [filter.Field] = filter.Values }
            },
            SearchFilterOperator.GreaterThan => Range(filter.Field, "gt", filter.Value!),
            SearchFilterOperator.GreaterThanOrEqual => Range(filter.Field, "gte", filter.Value!),
            SearchFilterOperator.LessThan => Range(filter.Field, "lt", filter.Value!),
            SearchFilterOperator.LessThanOrEqual => Range(filter.Field, "lte", filter.Value!),
            SearchFilterOperator.Exists => new Dictionary<string, object>
            {
                ["exists"] = new Dictionary<string, object> { ["field"] = filter.Field }
            },
            SearchFilterOperator.StartsWith => new Dictionary<string, object>
            {
                ["prefix"] = new Dictionary<string, object>
                {
                    [filter.Field] = Convert.ToString(filter.Value, CultureInfo.InvariantCulture) ?? string.Empty
                }
            },
            _ => throw new NotSupportedException($"不支持的过滤运算符：{filter.Operator}")
        };
    }

    /// <summary>
    /// 构建精确匹配子句
    /// </summary>
    /// <param name="field">字段名</param>
    /// <param name="value">比较值</param>
    /// <returns>子句</returns>
    private static object Term(string field, object value)
    {
        return new Dictionary<string, object>
        {
            ["term"] = new Dictionary<string, object> { [field] = value }
        };
    }

    /// <summary>
    /// 构建范围子句
    /// </summary>
    /// <param name="field">字段名</param>
    /// <param name="op">范围操作</param>
    /// <param name="value">比较值</param>
    /// <returns>子句</returns>
    private static object Range(string field, string op, object value)
    {
        return new Dictionary<string, object>
        {
            ["range"] = new Dictionary<string, object>
            {
                [field] = new Dictionary<string, object> { [op] = value }
            }
        };
    }

    /// <summary>
    /// 解析检索响应
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="body">响应体</param>
    /// <returns>检索结果</returns>
    private static SearchResult<TDocument> ParseSearchResponse<TDocument>(string body)
        where TDocument : class
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("hits", out var hitsRoot))
        {
            return SearchResult<TDocument>.Empty;
        }

        var totalCount = hitsRoot.TryGetProperty("total", out var total) && total.TryGetProperty("value", out var totalValue)
            ? totalValue.GetInt64()
            : 0;

        var hits = new List<SearchHit<TDocument>>();

        foreach (var hit in hitsRoot.GetProperty("hits").EnumerateArray())
        {
            var id = hit.GetProperty("_id").GetString()!;
            var source = hit.GetProperty("_source").Deserialize<TDocument>(SerializerOptions)!;
            var score = hit.TryGetProperty("_score", out var scoreValue) && scoreValue.ValueKind == JsonValueKind.Number
                ? scoreValue.GetDouble()
                : 0;

            hits.Add(new SearchHit<TDocument>(id, source, score, ParseHighlights(hit)));
        }

        return new SearchResult<TDocument>(hits, totalCount);
    }

    /// <summary>
    /// 解析高亮片段
    /// </summary>
    /// <param name="hit">命中项</param>
    /// <returns>高亮片段</returns>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseHighlights(JsonElement hit)
    {
        if (!hit.TryGetProperty("highlight", out var highlight))
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }

        var highlights = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var field in highlight.EnumerateObject())
        {
            highlights[field.Name] = [.. field.Value.EnumerateArray().Select(fragment => fragment.GetString() ?? string.Empty)];
        }

        return highlights;
    }
}
