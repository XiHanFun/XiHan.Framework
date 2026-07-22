// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Extensions.Data;

/// <summary>
/// 额外属性字典
/// 用于存储动态属性的可序列化字典，键为字符串，值为任意对象
/// </summary>
public class ExtraPropertyDictionary : Dictionary<string, object?>
{
    /// <summary>
    /// 初始化额外属性字典的新实例
    /// </summary>
    public ExtraPropertyDictionary()
    {
    }

    /// <summary>
    /// 使用指定的字典初始化额外属性字典的新实例
    /// </summary>
    /// <param name="dictionary">要复制的字典</param>
    /// <exception cref="ArgumentNullException">当 dictionary 为 null 时</exception>
    public ExtraPropertyDictionary(IDictionary<string, object?> dictionary)
        : base(dictionary)
    {
    }
}
