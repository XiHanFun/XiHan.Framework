#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreScalarModule
// Guid:7b69fc24-fbf3-4e1b-8175-eed3f45a7c76
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/12 0:38:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AspNetCore.Mvc;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.Scalar;

/// <summary>
/// XiHanAspNetCoreScalarModule
/// </summary>
[DependsOn(
    typeof(XiHanAspNetCoreMvcModule)
)]
public class XiHanAspNetCoreScalarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
