// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Core.DynamicProxy;

/// <summary>
/// 曦寒方法调用接口
/// </summary>
public interface IXiHanMethodInvocation
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
    /// <remarks>
    /// 可为 null：<see cref="ProceedAsync"/> 之前目标方法尚未执行、方法无返回值，或方法本身就返回 null。
    /// </remarks>
    object? ReturnValue { get; set; }

    /// <summary>
    /// 方法调用
    /// </summary>
    /// <returns></returns>
    Task ProceedAsync();
}
