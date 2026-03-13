#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOpenApiSecurityClientStore
// Guid:b1294d69-a6a6-4f06-abf6-3392e1091768
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 23:33:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// OpenApi 客户端存储
/// </summary>
public interface IOpenApiSecurityClientStore
{
    /// <summary>
    /// 根据 AccessKey 查找客户端
    /// </summary>
    /// <param name="accessKey">访问键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<OpenApiSecurityClient?> FindByAccessKeyAsync(
        string accessKey,
        CancellationToken cancellationToken = default);
}
