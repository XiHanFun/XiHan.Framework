#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExposedServiceTypesProvider
// Guid:4515b737-f713-4715-b669-beb70df9d1ed
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 01:43:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 暴露服务类型提供器接口
/// </summary>
public interface IExposedServiceTypesProvider
{
    /// <summary>
    /// 获取暴露的服务类型
    /// </summary>
    /// <param name="targetType"></param>
    /// <returns></returns>
    Type[] GetExposedServiceTypes(Type targetType);
}
