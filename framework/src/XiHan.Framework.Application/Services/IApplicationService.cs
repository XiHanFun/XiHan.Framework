#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IApplicationService
// Guid:a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// 应用服务接口
/// 实现此接口的服务将自动暴露为 REST API
/// </summary>
public interface IApplicationService : IRemoteService
{
}

