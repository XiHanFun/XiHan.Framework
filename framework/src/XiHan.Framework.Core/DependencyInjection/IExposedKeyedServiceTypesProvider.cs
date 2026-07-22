// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 暴露键服务类型提供器接口
/// </summary>
public interface IExposedKeyedServiceTypesProvider
{
    /// <summary>
    /// 获取暴露的服务类型
    /// </summary>
    /// <param name="targetType"></param>
    /// <returns></returns>
    ServiceIdentifier[] GetExposedServiceTypes(Type targetType);
}
