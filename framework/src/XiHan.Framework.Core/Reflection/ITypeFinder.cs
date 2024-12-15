﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITypeFinder
// Guid:64df6a54-ea03-437b-a823-f48aaec74155
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-22 下午 03:59:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Reflection;

/// <summary>
/// 类型查找器接口
/// 它可能不会返回所有类型，但这些类型都与模块相关
/// </summary>
public interface ITypeFinder
{
    /// <summary>
    /// 类型列表
    /// </summary>
    IReadOnlyList<Type> Types { get; }
}
