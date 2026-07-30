// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
