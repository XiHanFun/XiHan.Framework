// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Agents;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// XiHan Agent 工厂契约测试
/// </summary>
/// <remarks>
/// 工厂的四个参数全部可选且全部默认 null，调用方常用命名实参只传其中一两个，
/// 因此参数名与顺序都是硬契约：改名会让 <c>Create(providerName: "openai")</c> 直接编译失败，
/// 调序则会让位置实参静默错位（instructions 被当成 name 传下去）。
/// </remarks>
public class IXiHanAgentFactoryTests
{
    /// <summary>
    /// 全部省略时实现侧四个参数都收到 null
    /// </summary>
    /// <remarks>全 null 即「默认 provider + 无系统指令 + 无工具」的最简 Agent。</remarks>
    [Fact]
    public void Create_WhenAllArgumentsOmitted_PassesNullForEveryParameter()
    {
        var fake = new RecordingAgentFactory();
        IXiHanAgentFactory factory = fake;

        factory.Create();

        Assert.Null(fake.LastInstructions);
        Assert.Null(fake.LastName);
        Assert.Null(fake.LastTools);
        Assert.Null(fake.LastProviderName);
    }

    /// <summary>
    /// 按位置传参时，前两个实参分别落到系统指令与 Agent 名
    /// </summary>
    /// <remarks>这是参数顺序的可执行锁：调序会让本用例失败，而不是等到运行期才发现人格串了。</remarks>
    [Fact]
    public void Create_WithPositionalArguments_MapsInstructionsThenName()
    {
        var fake = new RecordingAgentFactory();
        IXiHanAgentFactory factory = fake;

        factory.Create("你是代码审查助手", "reviewer");

        Assert.Equal("你是代码审查助手", fake.LastInstructions);
        Assert.Equal("reviewer", fake.LastName);
        Assert.Null(fake.LastTools);
        Assert.Null(fake.LastProviderName);
    }

    /// <summary>
    /// 只用命名实参指定 provider 时，其余参数保持 null
    /// </summary>
    [Fact]
    public void Create_WithOnlyProviderName_LeavesOtherParametersNull()
    {
        var fake = new RecordingAgentFactory();
        IXiHanAgentFactory factory = fake;

        factory.Create(providerName: "openai");

        Assert.Equal("openai", fake.LastProviderName);
        Assert.Null(fake.LastInstructions);
        Assert.Null(fake.LastName);
        Assert.Null(fake.LastTools);
    }

    /// <summary>
    /// 工具列表原样抵达实现侧
    /// </summary>
    /// <remarks>
    /// 传的是空列表而不是 null：空列表表示「显式声明无工具」，与「未指定」是两种意图，
    /// 抽象层不得把空列表归一成 null。
    /// </remarks>
    [Fact]
    public void Create_WithToolList_PassesSameListInstance()
    {
        var fake = new RecordingAgentFactory();
        IXiHanAgentFactory factory = fake;
        var tools = new List<AITool>();

        factory.Create(tools: tools);

        Assert.Same(tools, fake.LastTools);
        Assert.NotNull(fake.LastTools);
    }

    /// <summary>
    /// 工厂返回 MAF 原生 AIAgent，不包 XiHan 自有 Agent 类型
    /// </summary>
    /// <remarks>
    /// 返回原生类型，调用方才能直接用 RunAsync/RunStreamingAsync 与 CreateSessionAsync（多轮会话/记忆）；
    /// 一旦包一层自有类型，这些能力都要逐个转发，也就失去了「薄封装 MAF」的意义。
    /// </remarks>
    [Fact]
    public void Create_Signature_ReturnsNativeAiAgent()
    {
        var method = typeof(IXiHanAgentFactory).GetMethod(nameof(IXiHanAgentFactory.Create))!;

        Assert.Equal(typeof(AIAgent), method.ReturnType);
    }

    /// <summary>
    /// 四个参数的名字、顺序、类型与默认值全部锁定
    /// </summary>
    [Fact]
    public void Create_Signature_HasStableParameterOrderAndDefaults()
    {
        var parameters = typeof(IXiHanAgentFactory).GetMethod(nameof(IXiHanAgentFactory.Create))!.GetParameters();

        Assert.Equal(4, parameters.Length);

        Assert.Equal("instructions", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);

        Assert.Equal("name", parameters[1].Name);
        Assert.Equal(typeof(string), parameters[1].ParameterType);

        Assert.Equal("tools", parameters[2].Name);
        Assert.Equal(typeof(IList<AITool>), parameters[2].ParameterType);

        Assert.Equal("providerName", parameters[3].Name);
        Assert.Equal(typeof(string), parameters[3].ParameterType);

        Assert.All(parameters, parameter =>
        {
            Assert.True(parameter.IsOptional);
            Assert.Null(parameter.DefaultValue);
        });
    }

    /// <summary>
    /// 创建是同步方法，不带取消令牌
    /// </summary>
    /// <remarks>
    /// 构建 Agent 只是按配置组装对象，不发起任何请求；
    /// 若哪天需要令牌，说明构建期混进了 IO（如探活/拉模型列表），应先质疑设计。
    /// </remarks>
    [Fact]
    public void Create_Signature_IsSynchronous()
    {
        var parameters = typeof(IXiHanAgentFactory).GetMethod(nameof(IXiHanAgentFactory.Create))!.GetParameters();

        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(CancellationToken));
    }

    /// <summary>
    /// 只记录调用参数的 Agent 工厂替身
    /// </summary>
    /// <remarks>
    /// 不构造真实 AIAgent：AIAgent 是 MAF 的抽象基类，实现它需要接上真实模型客户端，
    /// 与本抽象包无关。这里只验证参数如何抵达实现，返回值不参与断言。
    /// </remarks>
    private sealed class RecordingAgentFactory : IXiHanAgentFactory
    {
        /// <summary>
        /// 最近一次收到的系统指令
        /// </summary>
        public string? LastInstructions { get; private set; }

        /// <summary>
        /// 最近一次收到的 Agent 名
        /// </summary>
        public string? LastName { get; private set; }

        /// <summary>
        /// 最近一次收到的工具列表
        /// </summary>
        public IList<AITool>? LastTools { get; private set; }

        /// <summary>
        /// 最近一次收到的 provider 名
        /// </summary>
        public string? LastProviderName { get; private set; }

        /// <summary>
        /// 记录创建请求
        /// </summary>
        /// <param name="instructions">系统指令</param>
        /// <param name="name">Agent 名</param>
        /// <param name="tools">工具列表</param>
        /// <param name="providerName">provider 名</param>
        public AIAgent Create(string? instructions = null, string? name = null, IList<AITool>? tools = null, string? providerName = null)
        {
            LastInstructions = instructions;
            LastName = name;
            LastTools = tools;
            LastProviderName = providerName;

            return null!;
        }
    }
}
