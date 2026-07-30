// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置定义提供者接口
/// </summary>
public interface ISettingDefinitionProvider
{
    /// <summary>
    /// 定义设置
    /// </summary>
    /// <param name="context">设置定义上下文</param>
    void Define(ISettingDefinitionContext context);
}
