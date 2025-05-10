#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIAgentManager
// Guid:6c1ce556-0668-4ca6-a86e-41c0918d5fd4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Agents;

/// <summary>
/// Agent管理器
/// </summary>
public class XiHanAIAgentManager : IXiHanAIAgentService
{
    private readonly ConcurrentDictionary<string, IXiHanAIAgent> _agents = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<XiHanAIAgentManager> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="logger">日志记录器</param>
    public XiHanAIAgentManager(IServiceProvider serviceProvider, ILogger<XiHanAIAgentManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 创建新Agent
    /// </summary>
    public async Task<IXiHanAIAgent> CreateAgentAsync(string agentId, AgentOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_agents.TryGetValue(agentId, out var existingAgent))
        {
            return existingAgent;
        }

        options ??= new AgentOptions
        {
            Name = $"Agent_{agentId}",
            ProviderName = "OpenAI"
        };

        // 创建Agent实例
        var agent = new BaseAgent(agentId, options, _serviceProvider);

        await agent.InitializeAsync(cancellationToken);
        _agents.TryAdd(agentId, agent);

        _logger.LogInformation("已创建Agent: {AgentId}", agentId);

        return agent;
    }

    /// <summary>
    /// 获取已存在Agent
    /// </summary>
    public Task<IXiHanAIAgent?> GetAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    /// <summary>
    /// 删除Agent
    /// </summary>
    public Task<bool> RemoveAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var result = _agents.TryRemove(agentId, out _);
        if (result)
        {
            _logger.LogInformation("已删除Agent: {AgentId}", agentId);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// 向Agent发送消息
    /// </summary>
    public async Task<ChatResult> SendMessageAsync(string agentId, string message, CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return ChatResult.Failure($"未找到Agent: {agentId}");
        }

        try
        {
            return await agent.InvokeAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentId} 处理消息时出错", agentId);
            return ChatResult.Failure($"处理消息时出错: {ex.Message}");
        }
    }
}
