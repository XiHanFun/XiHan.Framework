// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.BackgroundJobs.Attributes;

/// <summary>
/// 标注后台作业参数类型的稳定名称
/// </summary>
/// <remarks>标注在“作业参数类型”上；不标注时回退为参数类型的完整名称。</remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class BackgroundJobNameAttribute : Attribute, IBackgroundJobNameProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">作业名称</param>
    public BackgroundJobNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// 作业名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 解析作业参数类型的名称（优先特性，否则回退完整类型名）
    /// </summary>
    /// <param name="jobArgsType">作业参数类型</param>
    /// <returns>作业名称</returns>
    public static string GetName(Type jobArgsType)
    {
        ArgumentNullException.ThrowIfNull(jobArgsType);

        var nameProvider = jobArgsType
            .GetCustomAttributes(true)
            .OfType<IBackgroundJobNameProvider>()
            .FirstOrDefault();

        return nameProvider?.Name ?? jobArgsType.FullName ?? jobArgsType.Name;
    }
}
