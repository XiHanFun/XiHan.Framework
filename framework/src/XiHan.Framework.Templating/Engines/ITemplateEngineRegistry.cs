#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateEngineRegistry
// Guid:425c5c48-cbe0-4d9a-a426-30cf26b7239b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:51:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 模板引擎注册表
/// </summary>
public interface ITemplateEngineRegistry
{
    /// <summary>
    /// 注册模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <param name="engine">模板引擎实例</param>
    void RegisterEngine<T>(string engineName, ITemplateEngine<T> engine);

    /// <summary>
    /// 获取模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <returns>模板引擎实例</returns>
    ITemplateEngine<T>? GetEngine<T>(string engineName);

    /// <summary>
    /// 获取默认模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <returns>默认模板引擎实例</returns>
    ITemplateEngine<T>? GetDefaultEngine<T>();

    /// <summary>
    /// 设置默认模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    void SetDefaultEngine<T>(string engineName);
}
