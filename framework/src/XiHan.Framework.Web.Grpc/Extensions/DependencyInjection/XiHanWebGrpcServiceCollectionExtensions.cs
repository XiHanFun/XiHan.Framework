// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
