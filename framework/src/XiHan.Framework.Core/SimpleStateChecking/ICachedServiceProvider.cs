#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICachedServiceProvider
// Guid:f4122ca3-a323-49f8-9d50-e082a0174ef0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:24:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 通过缓存已解析的服务来提供服务。它缓存包括瞬态在内的所有类型的服务。此服务的生命周期是作用域的，应在有限的范围内使用。
/// </summary>
public interface ICachedServiceProvider : ICachedServiceProviderBase
{
}