// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.ValueObjects;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 双成员值对象，其中币种允许为 null，用于验证 null 成员的相等性与哈希
/// </summary>
public class SampleMoney : ValueObject
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currency">币种</param>
    public SampleMoney(decimal amount, string? currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// 金额
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// 币种
    /// </summary>
    public string? Currency { get; }

    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return [Amount, Currency];
    }
}

/// <summary>
/// 与 <see cref="SampleMoney"/> 成员完全一致但 CLR 类型不同的值对象
/// </summary>
public sealed class SampleMoneyLookAlike : ValueObject
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currency">币种</param>
    public SampleMoneyLookAlike(decimal amount, string? currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// 金额
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// 币种
    /// </summary>
    public string? Currency { get; }

    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return [Amount, Currency];
    }
}

/// <summary>
/// 由 <see cref="SampleMoney"/> 派生并追加一个成员的值对象，用于验证继承场景
/// </summary>
public sealed class SampleTaggedMoney : SampleMoney
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="amount">金额</param>
    /// <param name="currency">币种</param>
    /// <param name="tag">标签</param>
    public SampleTaggedMoney(decimal amount, string? currency, string tag) : base(amount, currency)
    {
        Tag = tag;
    }

    /// <summary>
    /// 标签
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return [Amount, Currency, Tag];
    }
}

/// <summary>
/// 不产出任何相等性成员的值对象
/// </summary>
public sealed class SampleEmptyValueObject : ValueObject
{
    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>空集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return [];
    }
}

/// <summary>
/// 把整个集合当成单个相等性成员的值对象（按引用比较）
/// </summary>
public sealed class SampleTagsAsSingleComponent : ValueObject
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tags">标签集合</param>
    public SampleTagsAsSingleComponent(IReadOnlyList<string> tags)
    {
        Tags = tags;
    }

    /// <summary>
    /// 标签集合
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return [Tags];
    }
}

/// <summary>
/// 把集合逐项展开为相等性成员的值对象（按内容比较）
/// </summary>
public sealed class SampleTagsAsExpandedComponents : ValueObject
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tags">标签集合</param>
    public SampleTagsAsExpandedComponents(IReadOnlyList<string> tags)
    {
        Tags = tags;
    }

    /// <summary>
    /// 标签集合
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return [.. Tags];
    }
}

/// <summary>
/// 字符串单一值对象
/// </summary>
public sealed class SampleUserName : SingleValueObject<string>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">用户名</param>
    public SampleUserName(string value) : base(value)
    {
    }
}

/// <summary>
/// 与 <see cref="SampleUserName"/> 底层类型相同的另一个单一值对象
/// </summary>
public sealed class SampleNickName : SingleValueObject<string>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">昵称</param>
    public SampleNickName(string value) : base(value)
    {
    }
}

/// <summary>
/// 值类型的单一值对象
/// </summary>
public sealed class SampleUserAge : SingleValueObject<int>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">年龄</param>
    public SampleUserAge(int value) : base(value)
    {
    }
}
