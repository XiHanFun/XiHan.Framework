#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAgentFactory
// Guid:b2c3d4e5-f6a7-4b20-9d20-1a1b1c1d1e20
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <inheritdoc />
    public AIAgent Create(string? instructions = null, string? name = null, IList<AITool>? tools = null, string? providerName = null)
    {
        var chatClient = _resolver.Resolve(providerName);
        return chatClient.AsAIAgent(instructions: instructions, name: name, tools: tools);
    }
}
