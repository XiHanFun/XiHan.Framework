// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Collections;

/// <summary>
/// 类型列表接口
/// </summary>
public interface ITypeList : ITypeList<object>;

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
