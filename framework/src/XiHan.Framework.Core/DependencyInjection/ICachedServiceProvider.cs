#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICachedServiceProvider
// Guid:d439d759-5428-414d-8783-3dc097248dd3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/27 19:07:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 通过缓存已解析的服务来提供服务
/// 它缓存包括瞬态在内的所有类型的服务
/// 此服务的生命周期是作用域的，并且应在有限的范围内使用
/// </summary>
public interface ICachedServiceProvider : ICachedServiceProviderBase
{
}
