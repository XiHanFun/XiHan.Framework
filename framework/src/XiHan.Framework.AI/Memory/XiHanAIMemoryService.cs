#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIMemoryService
// Guid:437df3b7-3e45-417f-b206-a26d4a34845e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using System.Collections.Concurrent;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Memory;

/// <summary>
/// 记忆服务实现
/// </summary>
public class XiHanAIMemoryService : IXiHanAIMemoryService
{
    private readonly IOptions<KernelMemoryOptions> _options;
    private readonly ILogger<XiHanAIMemoryService> _logger;
    private readonly IKernelMemory _memory;
    private readonly ConcurrentDictionary<string, DateTime> _memoryTimestamps = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">记忆选项</param>
    /// <param name="logger">日志记录器</param>
    public XiHanAIMemoryService(IOptions<KernelMemoryOptions> options, ILogger<XiHanAIMemoryService> logger)
    {
        _options = options;
        _logger = logger;

        // 创建内核记忆实例
        var memoryBuilder = new KernelMemoryBuilder();

        // 使用OpenAI配置
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        memoryBuilder.WithOpenAIDefaults(apiKey);

        // 尝试配置Qdrant(如有)
        if (_options.Value.StorageType.Equals("qdrant", StringComparison.InvariantCultureIgnoreCase) &&
            !string.IsNullOrEmpty(_options.Value.ConnectionString))
        {
            memoryBuilder.WithQdrantMemoryDb(_options.Value.ConnectionString);
        }

        _memory = memoryBuilder.Build<MemoryServerless>();
    }

    /// <summary>
    /// 添加记忆
    /// </summary>
    public async Task<string> AddAsync(string collection, string text, IDictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // 生成唯一Id
            var id = Guid.NewGuid().ToString();

            // 元数据作为标签字典
            var tags = new TagCollection();
            if (metadata != null)
            {
                foreach (var (key, value) in metadata)
                {
                    tags.Add(key, value?.ToString() ?? string.Empty);
                }
            }

            // 添加时间戳
            tags.Add("timestamp", DateTime.UtcNow.ToString("o"));

            // 导入文本
            await _memory.ImportTextAsync(
                text: text,
                documentId: id,
                index: collection,
                tags: tags,
                cancellationToken: cancellationToken);

            // 记录时间戳
            _memoryTimestamps[id] = DateTime.UtcNow;

            _logger.LogInformation("已添加记忆: {Collection}/{Id}", collection, id);

            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加记忆时出错: {Collection}", collection);
            throw;
        }
    }

    /// <summary>
    /// 检索记忆
    /// </summary>
    public async Task<IEnumerable<XiHanMemoryResult>> SearchAsync(string collection, string query, int limit = 5, double minRelevance = 0.7, CancellationToken cancellationToken = default)
    {
        try
        {
            // 调用内核记忆搜索
            var searchResults = await _memory.SearchAsync(
                index: collection,
                query: query,
                limit: limit,
                minRelevance: minRelevance,
                cancellationToken: cancellationToken);

            // 转换结果
            var results = new List<XiHanMemoryResult>();

            // 手动转换，安全访问属性
            foreach (var result in searchResults.Results)
            {
                // 从结果中获取分区的第一个元素(如果有)
                var partition = result.Partitions.Count > 0
                    ? result.Partitions[0]
                    : null;

                var memoryResult = new XiHanMemoryResult
                {
                    Id = result.SourceName ?? string.Empty,
                    // 从分区获取文本和相关性得分
                    Text = partition != null ? partition.Text ?? string.Empty : string.Empty,
                    Relevance = partition?.Relevance ?? 0.0,
                    Metadata = []
                };

                // 安全访问分区标签
                if (partition != null)
                {
                    foreach (var key in partition.Tags.Keys)
                    {
                        memoryResult.Metadata[key] = partition.Tags[key];
                    }
                }

                results.Add(memoryResult);
            }

            _logger.LogInformation("检索记忆: {Collection}, 查询: {Query}, 找到: {Count}条结果", collection, query, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索记忆时出错: {Collection}, 查询: {Query}", collection, query);
            return [];
        }
    }

    /// <summary>
    /// 删除记忆
    /// </summary>
    public async Task<bool> DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        try
        {
            // 删除内核记忆
            await _memory.DeleteDocumentAsync(
                documentId: id,
                index: collection,
                cancellationToken: cancellationToken);

            // 移除时间戳
            _memoryTimestamps.TryRemove(id, out _);

            _logger.LogInformation("已删除记忆: {Collection}/{Id}", collection, id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除记忆时出错: {Collection}/{Id}", collection, id);
            return false;
        }
    }
}
