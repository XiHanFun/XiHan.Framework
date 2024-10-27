#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectAccessor
// Guid:3d52e9f7-5d1e-4255-8f6f-e5a51bd64582
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:26:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 对象访问器接口
/// </summary>
/// <typeparam name="T"></typeparam>
public class ObjectAccessor<T> : IObjectAccessor<T>
{
    /// <summary>
    /// 泛型对象
    /// </summary>
    public T? Value { get; set; }

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
}