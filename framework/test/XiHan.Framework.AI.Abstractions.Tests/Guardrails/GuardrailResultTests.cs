// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Guardrails;

namespace XiHan.Framework.AI.Abstractions.Tests.Guardrails;

/// <summary>
/// 护栏检查结果测试
/// </summary>
/// <remarks>
/// 这是本抽象包里唯一带真实逻辑的类型。它的两个工厂方法是安全决策的载体：
/// 结果一旦可被外部构造或事后改写，fail-closed 语义就形同虚设，因此不可变性与工厂唯一入口同样要断言。
/// </remarks>
public class GuardrailResultTests
{
    /// <summary>
    /// 放行结果不拦截且无原因
    /// </summary>
    [Fact]
    public void Allow_WhenCreated_IsNotBlockedAndHasNoReason()
    {
        var result = GuardrailResult.Allow();

        Assert.False(result.IsBlocked);
        Assert.Null(result.Reason);
    }

    /// <summary>
    /// 拦截结果标记为拦截并原样保留原因
    /// </summary>
    /// <param name="reason">拦截原因</param>
    /// <remarks>原因会进诊断日志与拒绝话术上下文，必须逐字保留，不做修剪或改写。</remarks>
    [Theory]
    [InlineData("命中敏感词：foo")]
    [InlineData("prompt injection heuristic matched")]
    [InlineData("   前后有空白   ")]
    public void Block_WhenCreatedWithReason_IsBlockedAndKeepsReasonVerbatim(string reason)
    {
        var result = GuardrailResult.Block(reason);

        Assert.True(result.IsBlocked);
        Assert.Equal(reason, result.Reason);
    }

    /// <summary>
    /// 放行与拦截是互斥的两种结果
    /// </summary>
    /// <remarks>只有一个 IsBlocked 位表达结论，避免出现「既没拦也没放」的第三态。</remarks>
    [Fact]
    public void AllowAndBlock_ProduceOppositeDecisions()
    {
        var allowed = GuardrailResult.Allow();
        var blocked = GuardrailResult.Block("nope");

        Assert.NotEqual(allowed.IsBlocked, blocked.IsBlocked);
    }

    /// <summary>
    /// 结果只能经工厂方法创建，不暴露公共构造器
    /// </summary>
    /// <remarks>
    /// 若外部能 new 出结果，就能造出「IsBlocked=true 而 Reason=null」这类非法组合；
    /// 私有构造器是把状态组合收口在工厂里的唯一手段。
    /// </remarks>
    [Fact]
    public void Type_ExposesNoPublicConstructor()
    {
        Assert.Empty(typeof(GuardrailResult).GetConstructors());
    }

    /// <summary>
    /// 结果创建后不可被改写
    /// </summary>
    [Fact]
    public void Properties_AreReadOnlyAfterConstruction()
    {
        Assert.Null(typeof(GuardrailResult).GetProperty(nameof(GuardrailResult.IsBlocked))!.SetMethod);
        Assert.Null(typeof(GuardrailResult).GetProperty(nameof(GuardrailResult.Reason))!.SetMethod);
    }

    /// <summary>
    /// 类型为 sealed，不允许派生出改写判定的子类
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(GuardrailResult).IsSealed);
    }

    /// <summary>
    /// 多次放行互不干扰，各自独立成立
    /// </summary>
    /// <remarks>
    /// 只断言值语义，不断言是否复用同一实例——是否缓存放行单例属实现细节，
    /// 把它写死会挡住将来的无谓分配优化。
    /// </remarks>
    [Fact]
    public void Allow_WhenCalledRepeatedly_YieldsEquivalentDecisions()
    {
        var first = GuardrailResult.Allow();
        var second = GuardrailResult.Allow();

        Assert.False(first.IsBlocked);
        Assert.False(second.IsBlocked);
        Assert.Null(first.Reason);
        Assert.Null(second.Reason);
    }
}
