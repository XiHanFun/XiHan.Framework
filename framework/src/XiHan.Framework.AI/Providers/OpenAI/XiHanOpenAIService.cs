#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOpenAIService
// Guid:b6589a67-d560-427c-9590-c4a71eb59762
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 5:57:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers.OpenAI;

/// <summary>
/// 基于远程 OpenAI 的曦寒 AI 服务
/// </summary>
public class XiHanOpenAiService : IXiHanAiService
{
    private readonly Kernel _kernel;
    private readonly OpenAiOptions _options;
    private readonly IChatCompletionService _openAiChatService;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _openAiTextEmbeddingService;
    private readonly ILogger<XiHanOpenAiService> _logger;

    private string _currentModel;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="kernel">Semantic Kernel实例</param>
    /// <param name="options">OpenAI配置选项</param>
    /// <param name="logger">日志记录器</param>
    public XiHanOpenAiService(
        Kernel kernel,
        IOptions<OpenAiOptions> options,
        ILogger<XiHanOpenAiService> logger)
    {
        _kernel = kernel;
        _options = options.Value;
        _currentModel = _options.ModelName;
        _openAiChatService = _kernel.GetRequiredService<IChatCompletionService>(_options.ServiceId);
        _openAiTextEmbeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(_options.ServiceId);
        _logger = logger;
    }

    /// <summary>
    /// 服务提供商名称
    /// </summary>
    public string ProviderName => "OpenAI";

    /// <summary>
    /// 异步聊天接口
    /// </summary>
    public async Task<XiHanChatResult> ChatAsync(string message, XiHanChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new XiHanChatOptions();
        var stopwatch = Stopwatch.StartNew();

        // 创建聊天历史
        var chatHistory = new ChatHistory();

        // 添加系统消息
        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            chatHistory.AddSystemMessage(options.SystemPrompt);
        }

        // 添加历史消息
        foreach (var (role, content) in options.ChatHistory)
        {
            if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddUserMessage(content);
            }
            else if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddAssistantMessage(content);
            }
            else if (role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddSystemMessage(content);
            }
        }

        // 添加当前用户消息
        chatHistory.AddUserMessage(message);

        // 设置生成选项
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ModelId = options.ModelName ?? _currentModel,
            MaxTokens = options.MaxTokens,
            Temperature = options.Temperature
        };

        // 发送请求并获取回复
        var response = await _openAiChatService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel, cancellationToken);

        stopwatch.Stop();

        // 解析工具调用
        ToolCallResult[]? toolCalls = null;
        if (response.Metadata?.TryGetValue("toolCalls", out var toolCallsObj) == true &&
            toolCallsObj is JsonElement toolCallsElement)
        {
            toolCalls = JsonSerializer.Deserialize<ToolCallResult[]>(toolCallsElement.GetRawText());
        }

        // 创建结果
        var result = XiHanChatResult.Success(response.Content ?? string.Empty);
        result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
        result.ToolCalls = toolCalls;

        // 记录令牌使用情况
        if (response.Metadata?.TryGetValue("usage", out var usageObj) == true && usageObj is JsonElement usageElement)
        {
            result.TokenUsage = new TokenUsage
            {
                PromptTokens = usageElement.GetProperty("prompt_tokens").GetInt32(),
                CompletionTokens = usageElement.GetProperty("completion_tokens").GetInt32(),
                TotalTokens = usageElement.GetProperty("total_tokens").GetInt32()
            };
        }

        return result;
    }

    /// <summary>
    /// 流式聊天接口
    /// </summary>
    public async IAsyncEnumerable<XiHanChatStreamingResult> ChatStreamingAsync(string message, XiHanChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new XiHanChatOptions();

        // 创建聊天历史
        var chatHistory = new ChatHistory();

        // 添加系统消息
        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            chatHistory.AddSystemMessage(options.SystemPrompt);
        }

        // 添加历史消息
        foreach (var (role, content) in options.ChatHistory)
        {
            if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddUserMessage(content);
            }
            else if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddAssistantMessage(content);
            }
            else if (role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddSystemMessage(content);
            }
        }

        // 添加当前用户消息
        chatHistory.AddUserMessage(message);

        // 设置生成选项
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ModelId = options.ModelName ?? _currentModel,
            MaxTokens = options.MaxTokens,
            Temperature = options.Temperature
        };

        // 获取流式响应
        var response = _openAiChatService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, _kernel, cancellationToken);

        var contentSoFar = string.Empty;
        await foreach (var content in response)
        {
            contentSoFar += content.Content;

            yield return new XiHanChatStreamingResult
            {
                IsEnd = false,
                ContentDelta = content.Content ?? string.Empty,
                ContentSoFar = contentSoFar
            };
        }

        // 最后一个流式结果标记结束
        yield return new XiHanChatStreamingResult
        {
            IsEnd = true,
            ContentDelta = string.Empty,
            ContentSoFar = contentSoFar
        };
    }

    /// <summary>
    /// 生成文本嵌入向量
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            // 修正方法调用，将单个字符串放入数组中，并移除不兼容的_kernel参数
            var result = await _openAiTextEmbeddingService.GenerateAsync(
                [text],
                cancellationToken: cancellationToken);

            // 获取第一个结果的向量
            return result.Count > 0 ? result[0].Vector.ToArray() : [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI生成嵌入向量失败: {Message}", ex.Message);
            return [];
        }
    }

    /// <summary>
    /// 切换模型
    /// </summary>
    public Task<bool> SwitchModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return Task.FromResult(false);
            }

            _currentModel = modelName;
            _logger.LogInformation("已切换到OpenAI模型: {ModelName}", modelName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换OpenAI模型失败: {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }
}
