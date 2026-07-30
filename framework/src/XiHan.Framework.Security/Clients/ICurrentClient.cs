// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Security.Clients;

/// <summary>
/// 当前客户端接口
/// </summary>
public interface ICurrentClient
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }
}
