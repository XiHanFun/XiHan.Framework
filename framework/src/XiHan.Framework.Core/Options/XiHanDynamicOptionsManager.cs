#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDynamicOptionsManager
// Guid:037ab531-c811-471e-a3bb-169a20ad9cb1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 04:29:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Core.Options;

/// <summary>
/// 曦寒动态选项管理器
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class XiHanDynamicOptionsManager<T> : OptionsManager<T>
    where T : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="factory"></param>
    protected XiHanDynamicOptionsManager(IOptionsFactory<T> factory)
        : base(factory)
    {
    }

    /// <summary>
    /// 设置
    /// </summary>
    /// <returns></returns>
    public async Task SetAsync()
    {
        await SetAsync(string.Empty);
    }

    /// <summary>
    /// 设置
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public virtual Task SetAsync(string name)
    {
        return OverrideOptionsAsync(name, base.Get(name));
    }

    /// <summary>
    /// 重写选项
    /// </summary>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected abstract Task OverrideOptionsAsync(string name, T options);
}
