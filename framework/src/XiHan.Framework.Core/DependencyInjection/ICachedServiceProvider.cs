// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 通过缓存已解析的服务来提供服务
/// 它缓存包括瞬态在内的所有类型的服务
/// 此服务的生命周期是作用域的，并且应在有限的范围内使用
/// </summary>
public interface ICachedServiceProvider : ICachedServiceProviderBase;
