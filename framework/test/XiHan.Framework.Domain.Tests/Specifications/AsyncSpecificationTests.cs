// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Specifications.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Specifications;

/// <summary>
/// 异步规约基类测试
/// </summary>
/// <remarks>
/// 异步组合规约宣称短路求值：与运算左侧为假、或运算左侧为真时都不应再碰右侧。
/// 右侧往往是要访问数据库的重规约，短路失效等于凭空多打一次库，所以必须验证。
/// </remarks>
public class AsyncSpecificationTests
{
    /// <summary>
    /// 默认异步实现直接复用同步判定结果
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsSatisfiedByAsync_ByDefault_MirrorsSyncResult(bool expected)
    {
        var specification = new SampleConstantAsyncSpecification(expected);

        var result = await specification.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
        Assert.Equal(expected, specification.IsSatisfiedBy(new SamplePerson()));
    }

    /// <summary>
    /// 默认异步实现在令牌已取消时立即中断
    /// </summary>
    [Fact]
    public async Task IsSatisfiedByAsync_WhenCancelled_Throws()
    {
        var specification = new SampleConstantAsyncSpecification(true);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => specification.IsSatisfiedByAsync(new SamplePerson(), cancellation.Token));
    }

    /// <summary>
    /// 异步与运算要求两侧同时成立
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task AndAsync_RequiresBothSides(bool left, bool right, bool expected)
    {
        var combined = new SampleConstantAsyncSpecification(left).AndAsync(new SampleConstantAsyncSpecification(right));

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 异步与运算左侧为假时不再求值右侧
    /// </summary>
    [Fact]
    public async Task AndAsync_WhenLeftIsFalse_ShortCircuits()
    {
        var right = new SampleRecordingAsyncSpecification(true);
        var combined = new SampleConstantAsyncSpecification(false).AndAsync(right);

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(right.WasEvaluated);
    }

    /// <summary>
    /// 异步与运算左侧为真时继续求值右侧
    /// </summary>
    [Fact]
    public async Task AndAsync_WhenLeftIsTrue_EvaluatesRight()
    {
        var right = new SampleRecordingAsyncSpecification(true);
        var combined = new SampleConstantAsyncSpecification(true).AndAsync(right);

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(right.WasEvaluated);
    }

    /// <summary>
    /// 异步或运算只要一侧成立即可
    /// </summary>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task OrAsync_AcceptsEitherSide(bool left, bool right, bool expected)
    {
        var combined = new SampleConstantAsyncSpecification(left).OrAsync(new SampleConstantAsyncSpecification(right));

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 异步或运算左侧为真时不再求值右侧
    /// </summary>
    [Fact]
    public async Task OrAsync_WhenLeftIsTrue_ShortCircuits()
    {
        var right = new SampleRecordingAsyncSpecification(false);
        var combined = new SampleConstantAsyncSpecification(true).OrAsync(right);

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.False(right.WasEvaluated);
    }

    /// <summary>
    /// 异步或运算左侧为假时继续求值右侧
    /// </summary>
    [Fact]
    public async Task OrAsync_WhenLeftIsFalse_EvaluatesRight()
    {
        var right = new SampleRecordingAsyncSpecification(true);
        var combined = new SampleConstantAsyncSpecification(false).OrAsync(right);

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(right.WasEvaluated);
    }

    /// <summary>
    /// 异步非运算取反原规约
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NotAsync_InvertsSpecification(bool source, bool expected)
    {
        var combined = new SampleConstantAsyncSpecification(source).NotAsync();

        var result = await combined.IsSatisfiedByAsync(new SamplePerson(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 异步与运算传入空规约时抛出参数异常
    /// </summary>
    [Fact]
    public void AndAsync_WhenSpecificationIsNull_Throws()
    {
        var specification = new SampleConstantAsyncSpecification(true);

        Assert.Throws<ArgumentNullException>(() => { _ = specification.AndAsync(null!); });
    }

    /// <summary>
    /// 异步或运算传入空规约时抛出参数异常
    /// </summary>
    [Fact]
    public void OrAsync_WhenSpecificationIsNull_Throws()
    {
        var specification = new SampleConstantAsyncSpecification(true);

        Assert.Throws<ArgumentNullException>(() => { _ = specification.OrAsync(null!); });
    }

    /// <summary>
    /// 异步组合规约同样能产出可编译的查询表达式
    /// </summary>
    [Fact]
    public void AndAsync_ToExpression_ProducesCompilableSingleParameterLambda()
    {
        var combined = new SampleConstantAsyncSpecification(true).AndAsync(new SampleConstantAsyncSpecification(false));

        var expression = combined.ToExpression();

        Assert.Single(expression.Parameters);
        Assert.False(expression.Compile()(new SamplePerson()));
    }

    /// <summary>
    /// 异步或组合规约同样能产出可编译的查询表达式
    /// </summary>
    [Fact]
    public void OrAsync_ToExpression_ProducesCompilableSingleParameterLambda()
    {
        var combined = new SampleConstantAsyncSpecification(true).OrAsync(new SampleConstantAsyncSpecification(false));

        var expression = combined.ToExpression();

        Assert.Single(expression.Parameters);
        Assert.True(expression.Compile()(new SamplePerson()));
    }

    /// <summary>
    /// 异步取反规约的表达式同样可编译
    /// </summary>
    [Fact]
    public void NotAsync_ToExpression_ProducesCompilableLambda()
    {
        var combined = new SampleConstantAsyncSpecification(true).NotAsync();

        var expression = combined.ToExpression();

        Assert.Single(expression.Parameters);
        Assert.False(expression.Compile()(new SamplePerson()));
    }

    /// <summary>
    /// 异步规约同时满足同步与异步规约契约
    /// </summary>
    [Fact]
    public void AsyncSpecification_ImplementsBothContracts()
    {
        var specification = new SampleConstantAsyncSpecification(true);

        Assert.IsAssignableFrom<ISpecification<SamplePerson>>(specification);
        Assert.IsAssignableFrom<IAsyncSpecification<SamplePerson>>(specification);
        Assert.True(typeof(ISpecification<SamplePerson>).IsAssignableFrom(typeof(IAsyncSpecification<SamplePerson>)));
    }
}
