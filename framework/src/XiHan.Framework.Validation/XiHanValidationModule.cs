#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanValidationModule
// Guid:f08d98d7-2ff2-484b-aa45-acf3d88c0c09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/06 04:53:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.Validation;

/// <summary>
/// 曦寒框架数据校验模块
/// </summary>
[DependsOn(
    typeof(XiHanValidationAbstractionsModule)
)]
public class XiHanValidationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
    }
}
