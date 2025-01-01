#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHostEnvironment
// Guid:6e7fabbd-46b7-4dfe-a5d7-9394a5148ebb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:31:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒宿主环境
/// </summary>
public class XiHanHostEnvironment : IXiHanHostEnvironment
{
    /// <summary>
    /// 环境名称
    /// </summary>
    public string? EnvironmentName { get; set; }
}
