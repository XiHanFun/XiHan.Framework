// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Agents;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Agents;

/// <summary>
/// <see cref="IXiHanAgentFactory"/> 默认实现（M1 解析器 → <c>IChatClient</c> → MAF <see cref="AIAgent"/>）
/// </summary>
/// <remarks>
/// 注意:MAF 的 <c>ChatClientAgent</c> 内部套 <c>FunctionInvokingChatClient</c>,会自动执行工具、无需人工批准。
/// v1 技能均只读(知识检索),安全;将来接入有副作用的技能须加批准/审计。
/// </remarks>
public sealed class XiHanAgentFactory : IXiHanAgentFactory
{
    private readonly IAiChatClientResolver _resolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanAgentFactory(IAiChatClientResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// 创建一个 Agent
    /// </summary>
    /// <param name="instructions">系统指令，描述人格与职责</param>
    /// <param name="name">Agent 名</param>
    /// <param name="tools">工具集合，可传技能函数或 MCP 客户端工具</param>
    /// <param name="providerName">provider 配置名，为空取默认 provider</param>
    /// <returns>基于该 provider 对话客户端构建的 Agent</returns>
    public AIAgent Create(string? instructions = null, string? name = null, IList<AITool>? tools = null, string? providerName = null)
    {
        var chatClient = _resolver.Resolve(providerName);
        return chatClient.AsAIAgent(instructions: instructions, name: name, tools: tools);
    }
}
