#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDataModule
// Guid:f08d98d7-2ff2-484b-aa45-acf3d88c0c09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/06 4:53:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data;

/// <summary>
/// 曦寒框架数据模块
/// </summary>
[DependsOn(
    typeof(XiHanUowModule)
    )]
public class XiHanDataModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
