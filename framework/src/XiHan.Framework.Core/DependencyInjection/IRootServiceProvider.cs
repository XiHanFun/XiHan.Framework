#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRootServiceProvider
// Guid:7bbdd189-f4ea-4982-a9d7-579ae91e6bcc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/27 21:59:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 应用程序的根服务提供器
/// 请小心使用根服务提供程序，因为无法释放/处置从根服务提供程序解析的对象
/// 因此，如果需要解析任何服务，始终创建一个新范围
/// </summary>
public interface IRootServiceProvider : IKeyedServiceProvider;