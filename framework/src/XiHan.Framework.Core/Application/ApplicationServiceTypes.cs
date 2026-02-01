#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationServiceTypes
// Guid:446e8c29-2ff4-4160-882c-9d111a62ad88
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 03:38:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 应用服务类型
/// </summary>
/// <remarks><see cref="FlagsAttribute"/> 是为了方便使用位运算</remarks>
[Flags]
public enum ApplicationServiceTypes : byte
{
    /// <summary>
    /// 仅应用服务，不包含集成服务<see cref="IntegrationServiceAttribute"/>
    /// </summary>
    ApplicationServices = 1,

    /// <summary>
    /// 集成服务<see cref="IntegrationServiceAttribute"/>
    /// </summary>
    IntegrationServices = 2,

    /// <summary>
    /// 所有服务
    /// </summary>
    All = ApplicationServices | IntegrationServices
}
