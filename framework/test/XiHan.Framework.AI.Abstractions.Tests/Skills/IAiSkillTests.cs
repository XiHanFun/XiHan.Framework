// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Skills;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 应用层 AI 技能契约测试
/// </summary>
/// <remarks>
/// 技能一份定义要同时走两条交付通道（对话工具与 MCP tool），因此它的形态被压到最薄：
/// 名字、说明、转成 AIFunction。转换返回 M.E.AI 原生的 AIFunction 而不是 XiHan 自有类型，
/// 是这两条通道能共用同一份定义的前提。
/// </remarks>
public class IAiSkillTests
{
    /// <summary>
    /// 技能对外暴露名字与说明
    /// </summary>
    /// <remarks>说明是给模型读的，决定它何时选用该工具，因此和名字同属对外契约。</remarks>
    [Fact]
    public void Skill_ExposesNameAndDescription()
    {
        IAiSkill skill = new StubSkill("generate_module", "按 XiHan 规范生成一个模块骨架");

        Assert.Equal("generate_module", skill.Name);
        Assert.Equal("按 XiHan 规范生成一个模块骨架", skill.Description);
    }

    /// <summary>
    /// 名字与说明是只读属性，注册后不可被改写
    /// </summary>
    /// <remarks>
    /// 技能按名注册进注册表并暴露成 MCP tool 名；若名字可变，注册表的键与技能自述会脱钩，
    /// 外部客户端按旧名调用就会找不到实现。
    /// </remarks>
    [Theory]
    [InlineData(nameof(IAiSkill.Name))]
    [InlineData(nameof(IAiSkill.Description))]
    public void Metadata_Properties_AreReadOnly(string propertyName)
    {
        var property = typeof(IAiSkill).GetProperty(propertyName)!;

        Assert.Equal(typeof(string), property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
    }

    /// <summary>
    /// 转换方法返回原生 AIFunction 且不需要任何参数
    /// </summary>
    /// <remarks>
    /// 无参意味着技能自身就持有全部元数据（名字/说明/参数模式），
    /// 框架侧无须再喂上下文即可把它挂进对话工具列表或 MCP tool 列表。
    /// </remarks>
    [Fact]
    public void AsFunction_Signature_ReturnsNativeAiFunctionWithoutParameters()
    {
        var method = typeof(IAiSkill).GetMethod(nameof(IAiSkill.AsFunction))!;

        Assert.Equal(typeof(AIFunction), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    /// <summary>
    /// 技能接口只有三个成员，保持最薄形态
    /// </summary>
    /// <remarks>
    /// 成员一旦增多，应用层实现技能的成本就上升，而技能本该是「随手加一个」的粒度。
    /// 这条断言用于挡住往接口上顺手挂功能的冲动。
    /// </remarks>
    [Fact]
    public void Interface_KeepsMinimalSurface()
    {
        var propertyNames = typeof(IAiSkill).GetProperties().Select(property => property.Name).ToArray();

        // 排除属性访问器（IsSpecialName），只数真正的方法
        var methodNames = typeof(IAiSkill).GetMethods()
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(2, propertyNames.Length);
        Assert.Contains(nameof(IAiSkill.Name), propertyNames);
        Assert.Contains(nameof(IAiSkill.Description), propertyNames);
        Assert.Equal(nameof(IAiSkill.AsFunction), Assert.Single(methodNames));
    }

    /// <summary>
    /// 只承载名字与说明的技能替身
    /// </summary>
    /// <remarks>
    /// AsFunction 不构造真实 AIFunction：AIFunctionFactory 位于 Microsoft.Extensions.AI 实现包，
    /// 本抽象包只引用 Abstractions，构造不出来也不该构造。转换行为由实现包的测试覆盖。
    /// </remarks>
    private sealed class StubSkill : IAiSkill
    {
        /// <summary>
        /// 构造技能替身
        /// </summary>
        /// <param name="name">技能名</param>
        /// <param name="description">技能说明</param>
        public StubSkill(string name, string description)
        {
            Name = name;
            Description = description;
        }

        /// <summary>
        /// 技能名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 技能说明
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 本替身不参与函数转换
        /// </summary>
        public AIFunction AsFunction()
        {
            return null!;
        }
    }
}
