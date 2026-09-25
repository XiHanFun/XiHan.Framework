// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Skills;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 测试用技能：技能名与投影出的工具名可分别指定，回显内容里带上技能名
/// </summary>
/// <remarks>
/// 回显内容带技能名，供断言实际被调用的是哪个技能。
/// </remarks>
/// <param name="name">技能名</param>
/// <param name="functionName">投影出的工具名，null 表示与技能名相同</param>
internal sealed class NamedEchoAiSkill(string name, string? functionName = null) : IAiSkill
{
    /// <summary>
    /// 技能名
    /// </summary>
    public string Name => name;

    /// <summary>
    /// 技能说明
    /// </summary>
    public string Description => $"回显技能 {name}，仅用于测试";

    /// <summary>
    /// 投影出的工具名
    /// </summary>
    public string ToolName => functionName ?? name;

    /// <summary>
    /// 转为可被投影成 MCP 工具的函数
    /// </summary>
    /// <returns>回显函数</returns>
    public AIFunction AsFunction()
    {
        return AIFunctionFactory.Create(Echo, ToolName, Description);
    }

    /// <summary>
    /// 回显实现
    /// </summary>
    /// <param name="text">待回显的文本</param>
    /// <returns>带技能名的回显文本</returns>
    private string Echo(string text)
    {
        return $"echo:{name}:{text}";
    }
}
