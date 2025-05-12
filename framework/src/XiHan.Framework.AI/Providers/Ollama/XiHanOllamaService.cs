#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOllamaService
// Guid:d4584ab9-9936-492d-be25-222fe3931676
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/2 2:00:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Embeddings;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers.Ollama;

#pragma warning disable SKEXP0070,SKEXP0010,SKEXP0001

/// <summary>
/// 基于本地 Ollama 的曦寒 AI 服务
/// </summary>
public class XiHanOllamaService : IXiHanAIService
{
    private readonly Kernel _kernel;
    private readonly OllamaOptions _options;
    private readonly IChatCompletionService _ollamaChatService;
    private readonly ITextEmbeddingGenerationService _ollamaTextEmbeddingService;
    private readonly ILogger<XiHanOllamaService> _logger;

    private string _currentModel;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="kernel">Semantic Kernel实例</param>
    /// <param name="options">Ollama配置选项</param>
    /// <param name="logger">日志记录器</param>
    public XiHanOllamaService(
        Kernel kernel,
        IOptions<OllamaOptions> options,
        ILogger<XiHanOllamaService> logger)
    {
        _kernel = kernel;
        _options = options.Value;
        _currentModel = _options.ModelName;
        _ollamaChatService = _kernel.GetRequiredService<IChatCompletionService>(_options.ServiceId);
        _ollamaTextEmbeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>(_options.ServiceId);
        _logger = logger;
    }

    /// <summary>
    /// 服务提供商名称
    /// </summary>
    public string ProviderName => "Ollama";

    /// <summary>
    /// 异步聊天接口
    /// </summary>
    public async Task<ChatResult> ChatAsync(string message, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();
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
        var executionSettings = new OllamaPromptExecutionSettings
        {
            ModelId = options.ModelName ?? _currentModel,
            Temperature = options.Temperature
        };

        // 发送请求并获取回复
        var response = await _ollamaChatService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel, cancellationToken);

        stopwatch.Stop();

        // 解析工具调用
        ToolCallResult[]? toolCalls = null;
        if (response.Metadata?.TryGetValue("toolCalls", out var toolCallsObj) == true &&
            toolCallsObj is JsonElement toolCallsElement)
        {
            toolCalls = JsonSerializer.Deserialize<ToolCallResult[]>(toolCallsElement.GetRawText());
        }

        // 创建结果
        var result = ChatResult.Success(response.Content ?? string.Empty);
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
    public async IAsyncEnumerable<ChatStreamingResult> ChatStreamingAsync(
        string message,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();

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
        var executionSettings = new OllamaPromptExecutionSettings
        {
            ModelId = options.ModelName ?? _currentModel,
            Temperature = options.Temperature
        };

        // 获取流式响应
        var response = _ollamaChatService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, _kernel, cancellationToken);

        var contentSoFar = string.Empty;
        await foreach (var content in response.WithCancellation(cancellationToken))
        {
            contentSoFar += content.Content;

            yield return new ChatStreamingResult
            {
                IsEnd = false,
                ContentDelta = content.Content ?? string.Empty,
                ContentSoFar = contentSoFar
            };
        }

        // 最后一个流式结果标记结束
        yield return new ChatStreamingResult
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
            var embeddings = await _ollamaTextEmbeddingService.GenerateEmbeddingAsync(text, _kernel, cancellationToken);

            return embeddings.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama生成嵌入向量失败: {Message}", ex.Message);
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
            _logger.LogInformation("已切换到Ollama模型: {ModelName}", modelName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换Ollama模型失败: {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }
}

#pragma warning restore SKEXP0070, SKEXP0010, SKEXP0001
