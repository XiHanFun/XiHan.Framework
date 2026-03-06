#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IClientInfoProvider
// Guid:8f21f95c-b3fb-4fa5-ac5c-00993f8498ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 14:32:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// 客户端信息提供器
/// </summary>
public interface IClientInfoProvider
{
    /// <summary>
    /// 获取当前客户端信息
    /// </summary>
    /// <returns></returns>
    ClientInfo GetCurrent();
}
