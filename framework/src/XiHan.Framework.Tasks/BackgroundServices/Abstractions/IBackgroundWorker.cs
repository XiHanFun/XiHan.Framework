#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBackgroundWorker
// Guid:c44bb977-e1e4-4aee-9333-b0685f5098f9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/27 04:28:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Tasks.BackgroundServices.Abstractions;

/// <summary>
/// IBackgroundWorker
/// </summary>
public interface IBackgroundWorker : ISingletonDependency
{
}
