// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Skills;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 测试用技能：把入参原样回显
/// </summary>
/// <remarks>
/// 回显是刻意选的：返回值由调用方传进去的实参决定，断言拿到 <c>echo:&lt;实参&gt;</c> 才能证明
/// 工具真的被执行了一遍，而不只是「服务端认得这个名字」。返回常量的话，一个空壳实现也能让断言通过。
/// </remarks>
internal sealed class EchoAiSkill : IAiSkill
{
    /// <summary>
    /// 技能名，同时也是投影出的 MCP 工具名
    /// </summary>
    public string Name => "xihan_test_echo";

    /// <summary>
    /// 技能说明
    /// </summary>
    public string Description => "把传入的文本原样回显，仅用于测试";

    /// <summary>
    /// 转为可被投影成 MCP 工具的函数
    /// </summary>
    /// <returns>回显函数</returns>
    public AIFunction AsFunction()
    {
        return AIFunctionFactory.Create(Echo, Name, Description);
    }

    /// <summary>
    /// 回显实现
    /// </summary>
    /// <param name="text">待回显的文本</param>
    /// <returns>带 <c>echo:</c> 前缀的文本</returns>
    private static string Echo(string text)
    {
        return "echo:" + text;
    }
}
