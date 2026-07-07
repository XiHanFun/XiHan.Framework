#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobArgsHelper
// Guid:2d677d79-a426-4345-b7e6-ec47823f1ce5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业参数类型解析工具
/// </summary>
public static class BackgroundJobArgsHelper
{
    /// <summary>
    /// 从作业处理器类型反射出其 IAsyncBackgroundJob&lt;TArgs&gt; 的参数类型
    /// </summary>
    /// <param name="jobType">作业处理器类型</param>
    /// <returns>作业参数类型</returns>
    /// <exception cref="ArgumentException">当类型未实现 IAsyncBackgroundJob&lt;&gt; 时</exception>
    public static Type GetJobArgsType(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);

        foreach (var @interface in jobType.GetInterfaces())
        {
            if (@interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == typeof(IAsyncBackgroundJob<>))
            {
                return @interface.GetGenericArguments()[0];
            }
        }

        throw new ArgumentException($"类型 {jobType.AssemblyQualifiedName} 未实现 IAsyncBackgroundJob<TArgs>", nameof(jobType));
    }

    /// <summary>
    /// 判断类型是否为可用的后台作业处理器（非抽象且实现 IAsyncBackgroundJob&lt;&gt;）
    /// </summary>
    /// <param name="type">待判断类型</param>
    /// <returns>是否为后台作业处理器</returns>
    public static bool IsBackgroundJob(Type type)
    {
        if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
        {
            return false;
        }

        return Array.Exists(type.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncBackgroundJob<>));
    }
}
