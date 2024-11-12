#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PropertyInfoExtensions
// Guid:3db8aa54-c08d-43bf-a55d-0da2bc860ef5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 1:09:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Utils.Extensions.System.Reflection;

/// <summary>
/// 属性信息扩展方法
/// </summary>
public static class PropertyInfoExtensions
{
    /// <summary>
    /// 返回当前属性信息是否为virtual
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public static bool IsVirtual(this PropertyInfo property)
    {
        MethodInfo? accessor = property.GetAccessors().FirstOrDefault();
        return accessor is not null and { IsVirtual: true, IsFinal: false };
    }
}
