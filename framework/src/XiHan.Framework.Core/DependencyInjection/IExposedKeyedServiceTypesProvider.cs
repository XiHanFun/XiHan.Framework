#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExposedKeyedServiceTypesProvider
// Guid:4d75efe9-7d25-4150-bcbd-48c8e9a995ca
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:47:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
