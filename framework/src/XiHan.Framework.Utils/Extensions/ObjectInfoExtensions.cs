#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectInfoExtensions
// Guid:3c22cdf5-2be0-4377-9412-322dcc2ab5e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-09-28 下午 06:58:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Runtime.CompilerServices;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 对象信息拓展类
/// </summary>
public static class ObjectInfoExtensions
{
    /// <summary>
    /// 获取对象全名
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="fullName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string GetObjectFullNameOf(this object instance, [CallerArgumentExpression(nameof(instance))] string fullName = "")
    {
        return instance == null
            ? throw new ArgumentNullException(nameof(instance))
            : fullName ?? throw new ArgumentNullException(nameof(fullName));
    }

    #region 字段信息

    /// <summary>
    /// 利用反射来判断对象是否包含某个字段
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="fieldName">需要判断的字段</param>
    /// <returns>是否包含</returns>
    public static bool IsObjectContainField(this object? instance, string fieldName)
    {
        if (instance == null || string.IsNullOrEmpty(fieldName))
            return false;

        var foundFieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundFieldInfo != null;
    }

    /// <summary>
    /// 利用反射来获取对象某字段信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="fieldName">字段名称</param>
    /// <returns>字段信息</returns>
    public static FieldInfo GetObjectField(this object? instance, string fieldName)
    {
        if (instance == null || string.IsNullOrEmpty(fieldName))
            throw new NotImplementedException(nameof(fieldName));

        var foundFieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundFieldInfo ?? throw new NotImplementedException(nameof(fieldName));
    }

    /// <summary>
    /// 利用反射来获取对象所有字段信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <returns>字段信息</returns>
    public static FieldInfo[] GetObjectFields(this object? instance)
    {
        if (instance == null)
            throw new NotImplementedException(nameof(instance));

        var foundFieldInfos = instance.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundFieldInfos ?? throw new NotImplementedException(nameof(foundFieldInfos));
    }

    #endregion

    #region 属性信息

    /// <summary>
    /// 利用反射来判断对象是否包含某个属性
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="propertyName">需要判断的属性</param>
    /// <returns>是否包含</returns>
    public static bool IsContainObjectProperty(this object? instance, string propertyName)
    {
        if (instance == null || string.IsNullOrEmpty(propertyName))
            return false;

        var foundPropertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundPropertyInfo != null;
    }

    /// <summary>
    /// 利用反射来获取对象某属性信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>属性信息</returns>
    public static PropertyInfo GetObjectProperty(this object? instance, string propertyName)
    {
        if (instance == null || string.IsNullOrEmpty(propertyName))
            throw new NotImplementedException(nameof(propertyName));

        var foundPropertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundPropertyInfo ?? throw new NotImplementedException(nameof(foundPropertyInfo));
    }

    /// <summary>
    /// 利用反射来获取对象所有属性信息
    /// </summary>+
    /// <param name="instance">对象</param>
    /// <returns>属性信息</returns>
    public static PropertyInfo[] GetObjectProperties(this object? instance)
    {
        if (instance == null)
            throw new NotImplementedException(nameof(instance));

        var foundPropertyInfos = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundPropertyInfos ?? throw new NotImplementedException(nameof(foundPropertyInfos));
    }

    #endregion

    #region 判断为空

    /// <summary>
    /// 判断对象是否为空，为空返回true
    /// </summary>
    /// <param name="data">要验证的对象</param>
    public static bool IsNullOrEmpty(this object? data)
    {
        // 如果为null
        if (data == null) return true;

        // 如果为""
        if (data is not string) return data is DBNull;

        if (string.IsNullOrEmpty(data.ToString()?.Trim())) return true;

        // 如果为DBNull
        return data is DBNull;
    }

    #endregion
}