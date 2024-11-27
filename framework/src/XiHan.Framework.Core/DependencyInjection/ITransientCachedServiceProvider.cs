#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITransientCachedServiceProvider
// Guid:b95d0bd2-65f4-4654-858f-fb4524b7071d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 2:52:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 通过缓存已解析的服务来提供服务
/// 它缓存包括瞬态在内的所有类型的服务
/// 此服务的生命周期是瞬态的
/// 有关具有作用域生命周期的服务，请参见 <see cref="ICachedServiceProvider"/>
/// </summary>
public interface ITransientCachedServiceProvider : ICachedServiceProviderBase;