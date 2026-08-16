// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 按框架术语表扩展查询词
/// </summary>
/// <remarks>
/// 纯字面匹配无法处理「换句话说」的提问，例如问「怎么避免重复消费」与文档中的「收件箱去重」字面零重叠。
/// 术语表用几十行 JSON 精准补掉这个缺口。扩展词权重折半，避免淹没用户的原始意图。
/// 术语表是增强而非必需：文件缺失或格式损坏时降级为不扩展，服务照常。
/// </remarks>
public sealed class SynonymExpander
{
    /// <summary>
    /// 同义词扩展出的词条权重
    /// </summary>
    private const double ExpandedWeight = 0.5;

    private readonly IReadOnlyList<string[]> _groups;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="groups">等价术语分组</param>
    private SynonymExpander(IReadOnlyList<string[]> groups)
    {
        _groups = groups;
    }

    /// <summary>
    /// 从术语表文件加载，失败时返回一个不做扩展的实例
    /// </summary>
    /// <param name="jsonPath">术语表路径，可为空</param>
    /// <param name="logger">日志记录器，警告写入 stderr</param>
    /// <returns>扩展器实例，永不为空</returns>
    public static SynonymExpander Load(string? jsonPath, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
        {
            logger.LogWarning("未找到术语表 {Path}，同义词扩展已禁用，检索仍可正常工作。", jsonPath);
            return new SynonymExpander([]);
        }

        try
        {
            var groups = JsonSerializer.Deserialize<string[][]>(File.ReadAllText(jsonPath));
            return new SynonymExpander(groups is null ? [] : [.. groups]);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "术语表 {Path} 解析失败，同义词扩展已禁用，检索仍可正常工作。", jsonPath);
            return new SynonymExpander([]);
        }
    }

    /// <summary>
    /// 把查询串扩展为带权词条集合
    /// </summary>
    /// <param name="query">用户查询串</param>
    /// <returns>去重后的带权词条，同一词条保留最高权重</returns>
    public IReadOnlyList<WeightedTerm> Expand(string query)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        var queryTerms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var term in Tokenizer.Tokenize(query))
        {
            weights[term] = 1.0;
            queryTerms.Add(term);
        }

        foreach (var group in _groups)
        {
            var matched = group.Any(member => Mentions(query, queryTerms, member));
            if (!matched)
            {
                continue;
            }

            foreach (var member in group)
            {
                if (Mentions(query, queryTerms, member))
                {
                    continue;
                }

                foreach (var term in Tokenizer.Tokenize(member))
                {
                    if (!weights.ContainsKey(term))
                    {
                        weights[term] = ExpandedWeight;
                    }
                }
            }
        }

        return [.. weights.Select(pair => new WeightedTerm(pair.Key, pair.Value))];
    }

    /// <summary>
    /// 判断查询里是否提到了某个术语
    /// </summary>
    /// <param name="query">原始查询串</param>
    /// <param name="queryTerms">查询串切出的词条集合</param>
    /// <param name="member">术语</param>
    /// <returns>提到时为 true</returns>
    /// <remarks>
    /// 中文没有词边界，只能按子串判断；纯拉丁术语则必须按词条匹配。
    /// 否则「Redis」会因为内含子串「di」而触发依赖注入术语组，
    /// 把「Redis 事件总线怎么配」的结果推向依赖注入文档——黄金查询集正是这样发现这条缺陷的。
    /// </remarks>
    private static bool Mentions(string query, HashSet<string> queryTerms, string member)
    {
        if (member.Any(ch => ch is >= '一' and <= '鿿'))
        {
            return query.Contains(member, StringComparison.OrdinalIgnoreCase);
        }

        var memberTerms = Tokenizer.Tokenize(member);
        return memberTerms.Count > 0 && memberTerms.All(queryTerms.Contains);
    }
}
