#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFrameworkAspNetCoreMVCModule
// Guid:3373b5b4-d47d-4691-8604-08247351259f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:00:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.MVC;

/// <summary>
/// XiHanFrameworkAspNetCoreMVCModule
/// </summary>
[DependsOn(
    typeof(XiHanFrameworkAspNetCoreModule)
    )]
public class XiHanFrameworkAspNetCoreMVCModule : XiHanModule
{
}
