// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Extensions.Data;

/// <summary>
/// 拥有额外属性的接口
/// 实现此接口的类可以动态存储和访问额外的属性数据
/// </summary>
public interface IHasExtraProperties
{
    /// <summary>
    /// 额外属性字典
    /// 用于存储动态属性的键值对集合
    /// </summary>
    ExtraPropertyDictionary ExtraProperties { get; }
}
