#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SingleValueObject
// Guid:edf23a4b-5c6d-4e7f-8a9b-1234567890cd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 15:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.ValueObjects;

/// <summary>
/// 单一值对象基类
/// 适用于只包含一个值的值对象，如唯一标识、Name 等
/// </summary>
/// <typeparam name="T">值的类型</typeparam>
public abstract class SingleValueObject<T> : ValueObject
    where T : notnull
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">值</param>
    /// <exception cref="ArgumentNullException">值为空时抛出</exception>
    protected SingleValueObject(T value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 值
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// 隐式转换到基础类型
    /// </summary>
    /// <param name="singleValueObject">单一值对象</param>
    /// <returns>基础值</returns>
    public static implicit operator T(SingleValueObject<T> singleValueObject)
    {
        return singleValueObject.Value;
    }

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    /// <returns>值的字符串表示</returns>
    public override string ToString()
    {
        return Value.ToString()!;
    }

    /// <summary>
    /// 获取相等性比较的属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
