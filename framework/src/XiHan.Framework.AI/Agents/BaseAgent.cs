#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BaseAgent
// Guid:75134be5-bbf6-410f-8915-2b86c78780a2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Memory;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Prompts;
using XiHan.Framework.AI.Providers;
using XiHan.Framework.AI.Results;
using XiHan.Framework.AI.Skills;

namespace XiHan.Framework.AI.Agents;

/// <summary>
/// 基础Agent实现
/// </summary>
public class BaseAgent : IXiHanAIAgent
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentOptions _options;
    private readonly ILogger<BaseAgent> _logger;
    private readonly List<IXiHanSkill> _skills = [];
    private readonly List<(string Role, string Content)> _chatHistory = [];

    private IXiHanAIService? _aiService;
    private IXiHanAIMemoryService? _memoryService;
    private IXiHanAIPromptManager? _promptManager;
    private bool _isInitialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">Agent唯一标识</param>
    /// <param name="options">配置选项</param>
    /// <param name="serviceProvider">服务提供程序</param>
    public BaseAgent(string id, AgentOptions options, IServiceProvider serviceProvider)
    {
        Id = id;
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<BaseAgent>>();
    }

    /// <summary>
    /// Agent唯一标识
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Agent名称
    /// </summary>
    public string Name => _options.Name;

    /// <summary>
    /// Agent记忆服务
    /// </summary>
    public IXiHanAIMemoryService? Memory => _memoryService;

    /// <summary>
    /// Agent技能列表
    /// </summary>
    public IReadOnlyCollection<IXiHanSkill> Skills => _skills.AsReadOnly();

    /// <summary>
    /// 初始化Agent
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        // 获取AI服务
        if (_options.ProviderName == "OpenAI")
        {
            _aiService = _serviceProvider.GetKeyedService<IXiHanAIService>("OpenAI");
        }
        else if (_options.ProviderName == "Ollama")
        {
            _aiService = _serviceProvider.GetKeyedService<IXiHanAIService>("Ollama");
        }
        else
        {
            throw new ArgumentException($"不支持的AI服务提供商: {_options.ProviderName}");
        }

        // 初始化记忆服务
        if (_options.EnableMemory)
        {
            _memoryService = _serviceProvider.GetRequiredService<IXiHanAIMemoryService>();
        }

        // 获取提示词管理器
        _promptManager = _serviceProvider.GetRequiredService<IXiHanAIPromptManager>();

        // 加载技能
        var skillRegistry = _serviceProvider.GetRequiredService<ISkillRegistry>();
        foreach (var skillId in _options.SkillIds)
        {
            var skill = await skillRegistry.GetSkillAsync(skillId, cancellationToken);
            if (skill != null)
            {
                _skills.Add(skill);
            }
        }

        _isInitialized = true;
    }

    /// <summary>
    /// 执行Agent任务
    /// </summary>
    public async Task<XiHanChatResult> InvokeAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }

        if (_aiService == null)
        {
            return XiHanChatResult.Failure("AI服务未初始化");
        }

        // 添加用户消息到历史
        _chatHistory.Add(("user", message));

        // 检查是否有匹配的技能
        var context = new SkillContext
        {
            SessionId = Id,
            AgentId = Id
        };

        // 设置聊天历史
        context.SetChatHistory(_chatHistory);

        try
        {
            // 首先尝试使用技能处理
            foreach (var skill in _skills)
            {
                if (skill.CanHandle(message, context))
                {
                    var skillResult = await skill.ExecuteAsync(message, context, cancellationToken);
                    if (skillResult.IsSuccess)
                    {
                        // 添加Assistant回复到历史
                        _chatHistory.Add(("assistant", skillResult.Content));
                        return XiHanChatResult.Success(skillResult.Content);
                    }
                }
            }

            // 如果没有匹配的技能，使用AI服务
            var memoryResults = new List<string>();

            // 检索相关记忆
            if (_memoryService != null)
            {
                var memories = await _memoryService.SearchAsync(Id, message, 3, 0.7, cancellationToken);
                memoryResults.AddRange(memories.Select(m => m.Text));
            }

            // 构建聊天选项
            var chatOptions = new XiHanChatOptions
            {
                ModelName = _options.ModelName,
                SystemPrompt = _options.SystemPrompt,
                ChatHistory = [.. _chatHistory.Take(Math.Max(0, _chatHistory.Count - 1))]
            };

            // 如果有相关记忆，加入系统提示
            if (memoryResults.Count > 0)
            {
                chatOptions.SystemPrompt += $"\n\n相关上下文信息:\n{string.Join("\n", memoryResults)}";
            }

            // 调用AI服务
            var result = await _aiService.ChatAsync(message, chatOptions, cancellationToken);

            if (result.IsSuccess)
            {
                // 添加Assistant回复到历史
                _chatHistory.Add(("assistant", result.Content));

                // 如果启用了记忆，存储对话
                if (_memoryService != null)
                {
                    await _memoryService.AddAsync(Id, $"用户: {message}\n助手: {result.Content}",
                        new Dictionary<string, object> { { "timestamp", DateTime.UtcNow } },
                        cancellationToken);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentId} 处理消息时出错", Id);
            return XiHanChatResult.Failure($"处理消息时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 添加技能
    /// </summary>
    public Task<bool> AddSkillAsync(IXiHanSkill skill, CancellationToken cancellationToken = default)
    {
        if (_skills.Any(s => s.Name == skill.Name))
        {
            return Task.FromResult(false);
        }

        _skills.Add(skill);
        return Task.FromResult(true);
    }
}
