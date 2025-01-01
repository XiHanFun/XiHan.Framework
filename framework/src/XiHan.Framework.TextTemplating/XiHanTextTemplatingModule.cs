#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTextTemplatingModule
// Guid:957b2815-e1a0-4f9e-8023-1e5d68482316
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.TextTemplating;

/// <summary>
/// XiHanTextTemplatingModule
/// </summary>
[DependsOn(
    typeof(XiHanVirtualFileSystemModule)
)]
public class XiHanTextTemplatingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
