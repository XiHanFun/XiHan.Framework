#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBlobStoringModule
// Guid:eb9a53e1-398a-492a-9d68-89e08ff0adb2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:28:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.FileSystem;

namespace XiHan.Framework.BlobStoring;

/// <summary>
/// 曦寒框架粒子存储模块
/// </summary>
[DependsOn(
    typeof(XiHanVirtualFileSystemModule)
    )]
public class XiHanBlobStoringModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
