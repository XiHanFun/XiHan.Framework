#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFrameworkAspNetCoreApiDocSwaggerModule
// Guid:8d8f4d0c-4b66-4d52-b9b7-ef10c658842a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 2:39:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AspNetCore.MVC;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.ApiDoc.Swagger;

/// <summary>
/// XiHanFrameworkAspNetCoreApiDocSwaggerModule
/// </summary>
[DependsOn(
    typeof(XiHanFrameworkAspNetCoreMVCModule)
    )]
public class XiHanFrameworkAspNetCoreApiDocSwaggerModule : XiHanModule
{
}
