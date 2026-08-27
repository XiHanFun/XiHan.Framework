// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Rules;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 结果可控的业务规则测试替身
/// </summary>
/// <remarks>
/// 额外记录被检查次数，用于断言批量校验是否短路。
/// </remarks>
public sealed class SampleBusinessRule : IBusinessRule
{
    private readonly bool _broken;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">规则描述消息</param>
    /// <param name="broken">规则是否被违反</param>
    public SampleBusinessRule(string message, bool broken)
    {
        Message = message;
        _broken = broken;
    }

    /// <summary>
    /// 规则描述消息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 规则被检查的次数
    /// </summary>
    public int CheckedCount { get; private set; }

    /// <summary>
    /// 检查规则是否被违反
    /// </summary>
    /// <returns>如果规则被违反返回 true，否则返回 false</returns>
    public bool IsBroken()
    {
        CheckedCount++;
        return _broken;
    }
}
