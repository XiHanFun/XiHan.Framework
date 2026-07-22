// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
