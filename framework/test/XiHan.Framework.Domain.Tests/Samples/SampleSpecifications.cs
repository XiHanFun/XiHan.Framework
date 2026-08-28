// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Domain.Specifications;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 规约测试用的简单模型
/// </summary>
public sealed class SamplePerson
{
    /// <summary>
    /// 姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 年龄
    /// </summary>
    public int Age { get; set; }
}

/// <summary>
/// 成年人规约
/// </summary>
/// <remarks>
/// 表达式参数刻意命名为 person，与 <see cref="SampleNameStartsWithSpecification"/> 的 candidate 不同，
/// 用于暴露组合时参数替换是否正确。
/// </remarks>
public sealed class SampleAdultSpecification : Specification<SamplePerson>
{
    /// <summary>
    /// 转换为表达式
    /// </summary>
    /// <returns>查询表达式</returns>
    public override Expression<Func<SamplePerson, bool>> ToExpression()
    {
        return person => person.Age >= 18;
    }
}

/// <summary>
/// 姓名前缀规约
/// </summary>
public sealed class SampleNameStartsWithSpecification : Specification<SamplePerson>
{
    private readonly string _prefix;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="prefix">姓名前缀</param>
    public SampleNameStartsWithSpecification(string prefix)
    {
        _prefix = prefix;
    }

    /// <summary>
    /// 转换为表达式
    /// </summary>
    /// <returns>查询表达式</returns>
    public override Expression<Func<SamplePerson, bool>> ToExpression()
    {
        return candidate => candidate.Name.StartsWith(_prefix, StringComparison.Ordinal);
    }
}

/// <summary>
/// 结果固定的异步规约，沿用基类的同步转异步默认实现
/// </summary>
public sealed class SampleConstantAsyncSpecification : AsyncSpecification<SamplePerson>
{
    private readonly bool _result;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="result">固定结果</param>
    public SampleConstantAsyncSpecification(bool result)
    {
        _result = result;
    }

    /// <summary>
    /// 转换为表达式
    /// </summary>
    /// <returns>查询表达式</returns>
    public override Expression<Func<SamplePerson, bool>> ToExpression()
    {
        return person => _result;
    }
}

/// <summary>
/// 记录是否被求值的异步规约，用于验证组合规约的短路行为
/// </summary>
public sealed class SampleRecordingAsyncSpecification : AsyncSpecification<SamplePerson>
{
    private readonly bool _result;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="result">固定结果</param>
    public SampleRecordingAsyncSpecification(bool result)
    {
        _result = result;
    }

    /// <summary>
    /// 是否已被异步求值
    /// </summary>
    public bool WasEvaluated { get; private set; }

    /// <summary>
    /// 转换为表达式
    /// </summary>
    /// <returns>查询表达式</returns>
    public override Expression<Func<SamplePerson, bool>> ToExpression()
    {
        return person => _result;
    }

    /// <summary>
    /// 异步检查实体是否满足规约
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果满足返回 true，否则返回 false</returns>
    public override Task<bool> IsSatisfiedByAsync(SamplePerson entity, CancellationToken cancellationToken = default)
    {
        WasEvaluated = true;
        return Task.FromResult(_result);
    }
}
