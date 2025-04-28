#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NameValue
// Guid:91f1a98a-dd4b-43af-bce9-b55f9c523880
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 4:07:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 可用于存储名称/值（或键/值）对
/// </summary>
[Serializable]
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
/// 可用于存储名称/值（或键/值）对
/// </summary>
[Serializable]
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
