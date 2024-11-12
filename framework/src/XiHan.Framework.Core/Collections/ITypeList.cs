#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITypeList
// Guid:9ae02503-64bb-4f7a-8209-ea2f7cce7daa
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-24 下午 03:20:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Collections;

/// <summary>
/// 类型列表接口
/// </summary>
public interface ITypeList : ITypeList<object>
{
}

/// <summary>
/// 泛型类型列表接口
/// </summary>
/// <typeparam name="TBaseType"></typeparam>
public interface ITypeList<in TBaseType> : IList<Type>
{
    /// <summary>
    /// 添加
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    void Add<T>() where T : TBaseType;

    /// <summary>
    /// 尝试添加
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    bool TryAdd<T>() where T : TBaseType;

    /// <summary>
    /// 包含
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <returns></returns>
    bool Contains<T>() where T : TBaseType;

    /// <summary>
    /// 移除
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void Remove<T>() where T : TBaseType;
}
