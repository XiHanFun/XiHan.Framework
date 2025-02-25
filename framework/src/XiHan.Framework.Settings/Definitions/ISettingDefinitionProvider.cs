#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISettingDefinitionProvider
// Guid:7d6e5f4a-3e2d-1f0a-9b8c-7d6e-5f4a-3e2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 14:30:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
