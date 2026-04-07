#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebGrpcServiceCollectionExtensions
// Guid:f2a3b4c5-6789-0abc-def1-234567890123
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/06 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Grpc.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 Web gRPC 服务集合扩展
/// </summary>
public static class XiHanWebGrpcServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒 Web gRPC 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWebGrpc(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGrpc();

        return services;
    }
}
