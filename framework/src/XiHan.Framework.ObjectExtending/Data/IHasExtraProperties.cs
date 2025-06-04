#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasExtraProperties
// Guid:c524ca3a-ceca-417d-9daa-0f4724a7acde
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 4:57:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.ObjectExtending.Data;

namespace XiHan.Framework.ObjectExtending;

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
