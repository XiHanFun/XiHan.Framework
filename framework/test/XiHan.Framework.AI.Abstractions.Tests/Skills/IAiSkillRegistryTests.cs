// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Skills;

namespace XiHan.Framework.AI.Abstractions.Tests.Skills;

/// <summary>
/// AI 技能注册表契约测试
/// </summary>
/// <remarks>
/// 注册表的两条约定都写在接口注释里、都无编译期保障：同名注册覆盖、按名未找到返回 null。
/// 「覆盖」而非「抛异常」是有意的：应用层要能用自己的实现替换框架内置的同名技能。
/// 这里用一个按约定实现的参考替身把两条约定固化成可执行用例。
/// </remarks>
public class IAiSkillRegistryTests
{
    /// <summary>
    /// 注册后可按名查找到该技能
    /// </summary>
    [Fact]
    public void Find_AfterRegister_ReturnsRegisteredSkill()
    {
        IAiSkillRegistry registry = new ReferenceSkillRegistry();
        var skill = new StubSkill("code_review", "审查代码");

        registry.Register(skill);

        Assert.Same(skill, registry.Find("code_review"));
    }

    /// <summary>
    /// 未注册的技能名返回 null 而不是抛异常
    /// </summary>
    /// <remarks>模型可能幻觉出并不存在的工具名，查不到必须是可处理的正常返回。</remarks>
    [Fact]
    public void Find_WhenNameNotRegistered_ReturnsNull()
    {
        IAiSkillRegistry registry = new ReferenceSkillRegistry();

        Assert.Null(registry.Find("not_registered"));
    }

    /// <summary>
    /// 同名重复注册后覆盖，而不是并存两条
    /// </summary>
    /// <remarks>
    /// 若并存，暴露给模型的工具列表会出现两个同名工具，MCP 客户端侧行为不确定；
    /// 覆盖语义同时让应用层能替换框架内置技能。
    /// </remarks>
    [Fact]
    public void Register_WithDuplicateName_ReplacesPreviousSkill()
    {
        IAiSkillRegistry registry = new ReferenceSkillRegistry();
        var original = new StubSkill("code_review", "旧实现");
        var replacement = new StubSkill("code_review", "新实现");

        registry.Register(original);
        registry.Register(replacement);

        Assert.Same(replacement, registry.Find("code_review"));
        Assert.Same(replacement, Assert.Single(registry.All));
    }

    /// <summary>
    /// 全部技能列表随注册增长
    /// </summary>
    [Fact]
    public void All_AfterMultipleRegistrations_ContainsEverySkill()
    {
        IAiSkillRegistry registry = new ReferenceSkillRegistry();
        registry.Register(new StubSkill("code_review", "审查代码"));
        registry.Register(new StubSkill("generate_module", "生成模块"));

        Assert.Equal(2, registry.All.Count);
        Assert.Contains(registry.All, skill => skill.Name == "code_review");
        Assert.Contains(registry.All, skill => skill.Name == "generate_module");
    }

    /// <summary>
    /// 注册是同步无返回值的操作
    /// </summary>
    /// <remarks>注册发生在启动装配期，不做 IO；返回 void 是「纯内存登记」的结构声明。</remarks>
    [Fact]
    public void Register_Signature_IsSynchronousWithSingleSkillParameter()
    {
        var method = typeof(IAiSkillRegistry).GetMethod(nameof(IAiSkillRegistry.Register))!;

        Assert.Equal(typeof(void), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Single(parameters);
        Assert.Equal(typeof(IAiSkill), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
    }

    /// <summary>
    /// 全部技能以只读列表暴露，不能被调用方就地增删
    /// </summary>
    /// <remarks>
    /// 只读属性 + 只读列表两层限制，保证注册只有 Register 一个入口，
    /// 避免有人绕过注册逻辑直接往集合里塞。
    /// </remarks>
    [Fact]
    public void All_Signature_IsReadOnlyListProperty()
    {
        var property = typeof(IAiSkillRegistry).GetProperty(nameof(IAiSkillRegistry.All))!;

        Assert.Equal(typeof(IReadOnlyList<IAiSkill>), property.PropertyType);
        Assert.Null(property.SetMethod);
    }

    /// <summary>
    /// 按名查找是同步方法，入参为必填的技能名
    /// </summary>
    [Fact]
    public void Find_Signature_IsSynchronousLookupByName()
    {
        var method = typeof(IAiSkillRegistry).GetMethod(nameof(IAiSkillRegistry.Find))!;

        Assert.Equal(typeof(IAiSkill), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Single(parameters);
        Assert.Equal("name", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
    }

    /// <summary>
    /// 按接口约定实现的注册表参考实现
    /// </summary>
    /// <remarks>把「同名覆盖、未找到返回 null」写成实现方可以照抄的样板。</remarks>
    private sealed class ReferenceSkillRegistry : IAiSkillRegistry
    {
        private readonly Dictionary<string, IAiSkill> _skills = new(StringComparer.Ordinal);

        /// <summary>
        /// 全部已注册技能
        /// </summary>
        public IReadOnlyList<IAiSkill> All => _skills.Values.ToList();

        /// <summary>
        /// 注册一个技能，同名覆盖
        /// </summary>
        /// <param name="skill">技能</param>
        public void Register(IAiSkill skill)
        {
            _skills[skill.Name] = skill;
        }

        /// <summary>
        /// 按名查找，未注册返回 null
        /// </summary>
        /// <param name="name">技能名</param>
        public IAiSkill? Find(string name)
        {
            _skills.TryGetValue(name, out var skill);

            return skill;
        }
    }

    /// <summary>
    /// 只承载名字与说明的技能替身
    /// </summary>
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
