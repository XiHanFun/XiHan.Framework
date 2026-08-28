// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.SearchEngines.Abstractions;
using XiHan.Framework.SearchEngines.Abstractions.Documents;
using XiHan.Framework.SearchEngines.Abstractions.Indexing;
using XiHan.Framework.SearchEngines.Abstractions.Querying;
using XiHan.Framework.SearchEngines.Abstractions.Results;

namespace XiHan.Framework.SearchEngines.InMemory;

/// <summary>
/// 进程内搜索引擎
/// </summary>
/// <remarks>
/// 面向单机开发与自动化测试：数据只存在于当前进程，重启即丢，不做分词与相关度模型。
/// 生产环境请引用具体搜索引擎的实现包。
/// <para>
/// 存在的另一个目的是让 <see cref="ISearchEngine"/> 始终有第二个实现来校验契约——
/// 只有一个实现的抽象无法证明自己没有泄漏该实现的概念。
/// </para>
/// </remarks>
public sealed class InMemorySearchEngine : ISearchEngine, ISingletonDependency
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, SearchIndexState> _indexes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 判断索引是否存在
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public Task<bool> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_indexes.ContainsKey(index));
    }

    /// <summary>
    /// 创建索引，已存在时不做任何事
    /// </summary>
    /// <param name="definition">索引定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际创建了索引</returns>
    public Task<bool> CreateIndexAsync(SearchIndexDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var created = _indexes.TryAdd(definition.Name, new SearchIndexState(definition));

        return Task.FromResult(created);
    }

    /// <summary>
    /// 删除索引，不存在时不做任何事
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际删除了索引</returns>
    public Task<bool> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_indexes.TryRemove(index, out _));
    }

    /// <summary>
    /// 写入单个文档，标识已存在时整体覆盖
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="document">文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task IndexAsync<TDocument>(string index, SearchDocument<TDocument> document, CancellationToken cancellationToken = default)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(document);

        var state = GetIndexOrThrow(index);
        state.Documents[document.Id] = JsonSerializer.SerializeToElement(document.Document, SerializerOptions);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量写入文档，标识已存在时整体覆盖
    /// </summary>
    /// <typeparam name="TDocument">文档类型</typeparam>
    /// <param name="index">索引名</param>
    /// <param name="documents">文档集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际写入的文档数</returns>
    public Task<int> IndexManyAsync<TDocument>(string index, IEnumerable<SearchDocument<TDocument>> documents, CancellationToken cancellationToken = default)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(documents);

        var state = GetIndexOrThrow(index);
        var count = 0;

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.Documents[document.Id] = JsonSerializer.SerializeToElement(document.Document, SerializerOptions);
            count++;
        }

        return Task.FromResult(count);
    }

    /// <summary>
    /// 按标识删除文档
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="id">文档标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次调用是否实际删除了文档</returns>
    public Task<bool> DeleteAsync(string index, string id, CancellationToken cancellationToken = default)
    {
        var state = GetIndexOrThrow(index);

        return Task.FromResult(state.Documents.TryRemove(id, out _));
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
        var state = GetIndexOrThrow(index);

        return Task.FromResult(state.Documents.TryGetValue(id, out var element)
            ? element.Deserialize<TDocument>(SerializerOptions)
            : null);
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
        ArgumentNullException.ThrowIfNull(request);

        var state = GetIndexOrThrow(request.Index);
        var searchFields = ResolveSearchFields(state, request);

        var matched = new List<(string Id, JsonElement Element, double Score)>();

        foreach (var (id, element) in state.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MatchesFilters(element, request.Filters))
            {
                continue;
            }

            var score = ScoreKeyword(element, searchFields, request.Keyword);
            if (score < 0)
            {
                continue;
            }

            matched.Add((id, element, score));
        }

        var ordered = ApplySorts(matched, request.Sorts);
        var page = ordered.Skip(request.Skip).Take(request.Take).ToList();

        var hits = page
            .Select(item => new SearchHit<TDocument>(
                item.Id,
                item.Element.Deserialize<TDocument>(SerializerOptions)!,
                item.Score,
                BuildHighlights(item.Element, request)))
            .ToList();

        return Task.FromResult(new SearchResult<TDocument>(hits, matched.Count));
    }

    /// <summary>
    /// 使此前的写入立即对检索可见
    /// </summary>
    /// <param name="index">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RefreshAsync(string index, CancellationToken cancellationToken = default)
    {
        // 进程内实现的写入立即可见，无需刷新
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取索引状态，索引不存在时抛出
    /// </summary>
    /// <param name="index">索引名</param>
    /// <returns>索引状态</returns>
    private SearchIndexState GetIndexOrThrow(string index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);

        return _indexes.TryGetValue(index, out var state)
            ? state
            : throw new InvalidOperationException($"索引 '{index}' 不存在，请先创建索引。");
    }

    /// <summary>
    /// 解析参与关键字检索的字段
    /// </summary>
    /// <param name="state">索引状态</param>
    /// <param name="request">检索请求</param>
    /// <returns>字段名集合</returns>
    private static IReadOnlyList<string> ResolveSearchFields(SearchIndexState state, SearchRequest request)
    {
        return request.Fields.Count > 0
            ? request.Fields
            : [.. state.Definition.Fields.Where(field => field.Searchable).Select(field => field.Name)];
    }

    /// <summary>
    /// 计算关键字得分，未命中时返回负数
    /// </summary>
    /// <param name="element">文档</param>
    /// <param name="fields">参与检索的字段</param>
    /// <param name="keyword">关键字</param>
    /// <returns>得分，未命中为 -1</returns>
    private static double ScoreKeyword(JsonElement element, IReadOnlyList<string> fields, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return 0;
        }

        var score = 0d;

        foreach (var field in fields)
        {
            var text = ReadString(element, field);
            if (text is not null && text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        return score > 0 ? score : -1;
    }

    /// <summary>
    /// 判断文档是否满足全部过滤条件
    /// </summary>
    /// <param name="element">文档</param>
    /// <param name="filters">过滤条件</param>
    /// <returns>是否满足</returns>
    private static bool MatchesFilters(JsonElement element, IReadOnlyList<SearchFilter> filters)
    {
        return filters.All(filter => MatchesFilter(element, filter));
    }

    /// <summary>
    /// 判断文档是否满足单个过滤条件
    /// </summary>
    /// <param name="element">文档</param>
    /// <param name="filter">过滤条件</param>
    /// <returns>是否满足</returns>
    private static bool MatchesFilter(JsonElement element, SearchFilter filter)
    {
        var hasValue = TryReadProperty(element, filter.Field, out var property);

        if (filter.Operator == SearchFilterOperator.Exists)
        {
            return hasValue && property.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
        }

        if (!hasValue)
        {
            return false;
        }

        return filter.Operator switch
        {
            SearchFilterOperator.Equal => CompareTo(property, filter.Value) == 0,
            SearchFilterOperator.NotEqual => CompareTo(property, filter.Value) != 0,
            SearchFilterOperator.In => filter.Values.Any(value => CompareTo(property, value) == 0),
            SearchFilterOperator.GreaterThan => CompareTo(property, filter.Value) > 0,
            SearchFilterOperator.GreaterThanOrEqual => CompareTo(property, filter.Value) >= 0,
            SearchFilterOperator.LessThan => CompareTo(property, filter.Value) < 0,
            SearchFilterOperator.LessThanOrEqual => CompareTo(property, filter.Value) <= 0,
            SearchFilterOperator.StartsWith => ToComparableString(property)
                .StartsWith(Convert.ToString(filter.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    /// <summary>
    /// 比较文档字段与给定值
    /// </summary>
    /// <param name="property">文档字段</param>
    /// <param name="value">比较值</param>
    /// <returns>小于返回负数，等于返回 0，大于返回正数；不可比时返回非 0</returns>
    private static int CompareTo(JsonElement property, object? value)
    {
        if (value is null)
        {
            return property.ValueKind == JsonValueKind.Null ? 0 : 1;
        }

        if (property.ValueKind is JsonValueKind.Number && IsNumeric(value))
        {
            return property.GetDouble().CompareTo(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False && value is bool boolean)
        {
            return property.GetBoolean().CompareTo(boolean);
        }

        if (property.ValueKind == JsonValueKind.String && value is DateTime dateTime
            && property.TryGetDateTime(out var propertyDateTime))
        {
            return propertyDateTime.CompareTo(dateTime);
        }

        return string.Compare(
            ToComparableString(property),
            Convert.ToString(value, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断是否为数值类型
    /// </summary>
    /// <param name="value">值</param>
    /// <returns>是否为数值</returns>
    private static bool IsNumeric(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    /// <summary>
    /// 按排序项对命中集合排序
    /// </summary>
    /// <param name="matched">命中集合</param>
    /// <param name="sorts">排序项</param>
    /// <returns>排序后的集合</returns>
    private static IEnumerable<(string Id, JsonElement Element, double Score)> ApplySorts(
        List<(string Id, JsonElement Element, double Score)> matched,
        IReadOnlyList<SearchSort> sorts)
    {
        if (sorts.Count == 0)
        {
            return matched.OrderByDescending(item => item.Score).ThenBy(item => item.Id, StringComparer.Ordinal);
        }

        IOrderedEnumerable<(string Id, JsonElement Element, double Score)>? ordered = null;

        foreach (var sort in sorts)
        {
            var keySelector = BuildSortKeySelector(sort);

            ordered = ordered is null
                ? sort.Direction == SearchSortDirection.Ascending
                    ? matched.OrderBy(keySelector)
                    : matched.OrderByDescending(keySelector)
                : sort.Direction == SearchSortDirection.Ascending
                    ? ordered.ThenBy(keySelector)
                    : ordered.ThenByDescending(keySelector);
        }

        return ordered!.ThenBy(item => item.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// 构建排序键选择器
    /// </summary>
    /// <param name="sort">排序项</param>
    /// <returns>排序键选择器</returns>
    private static Func<(string Id, JsonElement Element, double Score), IComparable> BuildSortKeySelector(SearchSort sort)
    {
        if (sort.Field == SearchSort.ScoreField)
        {
            return item => item.Score;
        }

        return item =>
        {
            if (!TryReadProperty(item.Element, sort.Field, out var property))
            {
                return string.Empty;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number => property.GetDouble(),
                JsonValueKind.True => 1d,
                JsonValueKind.False => 0d,
                _ => ToComparableString(property)
            };
        };
    }

    /// <summary>
    /// 构建高亮片段
    /// </summary>
    /// <param name="element">文档</param>
    /// <param name="request">检索请求</param>
    /// <returns>高亮片段</returns>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildHighlights(JsonElement element, SearchRequest request)
    {
        if (request.HighlightFields.Count == 0 || string.IsNullOrWhiteSpace(request.Keyword))
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }

        var highlights = new Dictionary<string, IReadOnlyList<string>>();

        foreach (var field in request.HighlightFields)
        {
            var text = ReadString(element, field);
            if (text is null || !text.Contains(request.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            highlights[field] = [text.Replace(request.Keyword, $"<em>{request.Keyword}</em>", StringComparison.OrdinalIgnoreCase)];
        }

        return highlights;
    }

    /// <summary>
    /// 按字段名读取属性，支持以点分隔的嵌套路径
    /// </summary>
    /// <param name="element">文档</param>
    /// <param name="field">字段名</param>
    /// <param name="property">属性</param>
    /// <returns>是否存在</returns>
    private static bool TryReadProperty(JsonElement element, string field, out JsonElement property)
    {
        property = element;

        foreach (var segment in field.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (property.ValueKind != JsonValueKind.Object || !property.TryGetProperty(segment, out property))
            {
                property = default;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 读取字段的字符串值
    /// </summary>
    /// <param name="element">文档</param>
    /// <param name="field">字段名</param>
    /// <returns>字符串值，字段不存在时为空</returns>
    private static string? ReadString(JsonElement element, string field)
    {
        return TryReadProperty(element, field, out var property) ? ToComparableString(property) : null;
    }

    /// <summary>
    /// 取属性的可比较字符串表示
    /// </summary>
    /// <param name="property">属性</param>
    /// <returns>字符串表示</returns>
    private static string ToComparableString(JsonElement property)
    {
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => property.GetRawText()
        };
    }

    /// <summary>
    /// 索引状态
    /// </summary>
    /// <param name="definition">索引定义</param>
    private sealed class SearchIndexState(SearchIndexDefinition definition)
    {
        /// <summary>
        /// 索引定义
        /// </summary>
        public SearchIndexDefinition Definition { get; } = definition;

        /// <summary>
        /// 文档集合
        /// </summary>
        public ConcurrentDictionary<string, JsonElement> Documents { get; } = new(StringComparer.Ordinal);
    }
}
