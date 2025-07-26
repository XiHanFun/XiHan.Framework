#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExtensibleService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可扩展服务接口
/// </summary>
/// <typeparam name="TExtension">扩展类型</typeparam>
public interface IExtensibleService<TExtension> : IFrameworkService
{
    /// <summary>
    /// 添加扩展
    /// </summary>
    /// <param name="extension">扩展对象</param>
    void AddExtension(TExtension extension);

    /// <summary>
    /// 移除扩展
    /// </summary>
    /// <param name="extension">扩展对象</param>
    void RemoveExtension(TExtension extension);

    /// <summary>
    /// 获取所有扩展
    /// </summary>
    /// <returns>扩展集合</returns>
    IEnumerable<TExtension> GetExtensions();
}
