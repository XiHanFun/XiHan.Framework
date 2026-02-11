#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanHostEnvironment
// Guid:eab9c067-3a17-4e62-ab00-dc7489d448da
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:30:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒宿主环境接口
/// </summary>
public interface IXiHanHostEnvironment
{
    /// <summary>
    /// 环境名称
    /// </summary>
    string? EnvironmentName { get; set; }
}
