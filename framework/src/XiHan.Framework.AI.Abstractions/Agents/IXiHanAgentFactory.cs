#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAgentFactory
// Guid:a1b2c3d4-e5f6-4a09-9c09-0a0b0c0d0e09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Agents;

/// <summary>
/// XiHan Agent 工厂（基于 Microsoft Agent Framework，从 M1 的 provider 解析器构建 <see cref="AIAgent"/>）
/// </summary>
/// <remarks>
/// 薄封装 MAF：只加「按 provider 名选模型」这层 XiHan 语义；返回 MAF 原生 <see cref="AIAgent"/>——
/// 调用方直接用 <c>RunAsync</c>/<c>RunStreamingAsync</c> 与 <c>CreateSessionAsync</c>(多轮会话/记忆),不再另造包装类型。
/// </remarks>
public interface IXiHanAgentFactory
{
    /// <summary>
    /// 创建一个 Agent
    /// </summary>
    /// <param name="instructions">系统指令(人格/职责)</param>
    /// <param name="name">Agent 名</param>
    /// <param name="tools">工具(可传技能 <c>AsFunction()</c> 或 MCP client tools)</param>
    /// <param name="providerName">provider 配置名(null 用默认 provider)</param>
    AIAgent Create(string? instructions = null, string? name = null, IList<AITool>? tools = null, string? providerName = null);
}
