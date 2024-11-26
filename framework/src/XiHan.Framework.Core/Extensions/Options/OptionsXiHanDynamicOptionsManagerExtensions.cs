#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OptionsXiHanDynamicOptionsManagerExtensions
// Guid:edbd1f43-5245-4793-8a0d-4930c2ac0bd5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/5/7 0:57:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Options;

namespace XiHan.Framework.Core.Extensions.Options;

/// <summary>
/// 配置曦寒动态选项管理器扩展方法
/// </summary>
public static class OptionsXiHanDynamicOptionsManagerExtensions
{
    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options"></param>
    /// <returns></returns>
    public static Task SetAsync<T>(this IOptions<T> options)
        where T : class
    {
        return options.ToDynamicOptions().SetAsync();
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static Task SetAsync<T>(this IOptions<T> options, string name)
        where T : class
    {
        return options.ToDynamicOptions().SetAsync(name);
    }

    /// <summary>
    /// 转化为动态选项
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    private static XiHanDynamicOptionsManager<T> ToDynamicOptions<T>(this IOptions<T> options)
        where T : class
    {
        return options is XiHanDynamicOptionsManager<T> dynamicOptionsManager
            ? dynamicOptionsManager
            : throw new XiHanException($"选项必须派生自 {typeof(XiHanDynamicOptionsManager<>).FullName}！");
    }
}