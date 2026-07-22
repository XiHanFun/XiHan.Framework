// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 对象访问器接口
/// </summary>
/// <typeparam name="T"></typeparam>
public class ObjectAccessor<T> : IObjectAccessor<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ObjectAccessor()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="obj"></param>
    public ObjectAccessor(T? obj)
    {
        Value = obj;
    }

    /// <summary>
    /// 泛型对象
    /// </summary>
    public T? Value { get; set; }
}
