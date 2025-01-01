#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMethodInvocation
// Guid:7956d463-bc5e-44ce-8b62-14d39cd2655d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:34:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Core.DynamicProxy;

/// <summary>
/// 方法调用接口
/// </summary>
public interface IMethodInvocation
{
    /// <summary>
    /// 参数
    /// </summary>
    object[] Arguments { get; }

    /// <summary>
    /// 参数字典
    /// </summary>
    IReadOnlyDictionary<string, object> ArgumentsDictionary { get; }

    /// <summary>
    /// 泛型参数
    /// </summary>
    Type[] GenericArguments { get; }

    /// <summary>
    /// 目标对象
    /// </summary>
    object TargetObject { get; }

    /// <summary>
    /// 方法
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// 返回值
    /// </summary>
    object ReturnValue { get; set; }

    /// <summary>
    /// 方法调用
    /// </summary>
    /// <returns></returns>
    Task ProceedAsync();
}
