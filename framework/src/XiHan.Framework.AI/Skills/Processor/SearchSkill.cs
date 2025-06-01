#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SearchSkill
// Guid:3b1010e0-19f2-4968-96ff-f6b721102426
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Skills.Processor;

/// <summary>
/// 搜索技能
/// </summary>
public class SearchSkill : IXiHanSkill
{
    private readonly ILogger<SearchSkill> _logger;
    private readonly HttpClient _httpClient;
    private readonly Regex _searchPattern = new(@"(搜索|查询|查找)\s+(.+)");

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="httpClientFactory">HTTP客户端工厂</param>
    public SearchSkill(ILogger<SearchSkill> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("SearchSkill");
        _httpClient.BaseAddress = new Uri("https://api.example.com/");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// 技能名称
    /// </summary>
    public string Name => "SearchSkill";

    /// <summary>
    /// 技能描述
    /// </summary>
    public string Description => "提供简单的网络搜索功能，支持关键词查询。例如：搜索 曦寒框架";

    /// <summary>
    /// 判断技能是否可处理指定输入
    /// </summary>
    public bool CanHandle(string input, SkillContext context)
    {
        return _searchPattern.IsMatch(input);
    }

    /// <summary>
    /// 执行技能
    /// </summary>
    public async Task<XiHanSkillResult> ExecuteAsync(string input, SkillContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 提取搜索关键字
            var match = _searchPattern.Match(input);
            if (!match.Success || match.Groups.Count < 3)
            {
                return XiHanSkillResult.Failure("无法识别搜索关键字");
            }

            var keyword = match.Groups[2].Value.Trim();

            // 执行搜索(模拟实现)
            var searchResults = await SearchAsync(keyword, cancellationToken);

            if (string.IsNullOrEmpty(searchResults))
            {
                return XiHanSkillResult.Failure($"未找到关于{keyword}的相关信息");
            }

            _logger.LogInformation("搜索关键字: {Keyword}, 找到结果", keyword);

            return XiHanSkillResult.Success(searchResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索时出错");
            return XiHanSkillResult.Failure($"搜索出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    /// <param name="keyword">关键字</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果</returns>
    private static async Task<string> SearchAsync(string keyword, CancellationToken cancellationToken)
    {
        // 模拟搜索结果(实际项目中应调用真实搜索API)
        await Task.Delay(500, cancellationToken); // 模拟网络延迟

        // 简单返回一些模拟结果
        return $"关于{keyword}的搜索结果:\n\n" +
               $"1. {keyword}的基本介绍与用法\n" +
               $"2. {keyword}相关的最新资讯\n" +
               $"3. {keyword}的常见问题与解答\n\n" +
               "由于是模拟搜索结果，实际项目中请替换为真实搜索API。";
    }
}
