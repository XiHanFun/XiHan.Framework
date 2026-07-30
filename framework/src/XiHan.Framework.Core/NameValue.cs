// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core;

/// <summary>
/// 可用于存储名称/值(或键/值)对
/// </summary>
public class NameValue : NameValue<string>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public NameValue()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public NameValue(string name, string value)
    {
        Name = name;
        Value = value;
    }
}

/// <summary>
/// 可用于存储名称/值(或键/值)对
/// </summary>
public class NameValue<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public NameValue()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public NameValue(string name, T value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// 键
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 值
    /// </summary>
    public T Value { get; set; } = default!;
}
