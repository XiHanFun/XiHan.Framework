#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSwaggerGenServiceCollectionExtensions
// Guid:3e93b49a-674f-4383-be13-473f17a0348a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/12 2:10:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Swashbuckle.AspNetCore.SwaggerGen;

namespace XiHan.Framework.Web.Docs.Extensions.DependencyInjection;

/// <summary>
/// XiHanSwaggerGenServiceCollectionExtensions
/// </summary>
public static class XiHanSwaggerGenServiceCollectionExtensions
{
    /// <summary>
    /// 添加 SwaggerGen
    /// </summary>
    /// <param name="services"></param>
    /// <param name="setupAction"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanSwagger(this IServiceCollection services, Action<SwaggerGenOptions>? setupAction = null)
    {
        // 配置Swagger，从路由、控制器和模型构建对象
        _ = services.AddSwaggerGen(options =>
        {
            //TODO: 配置Swagger

            setupAction?.Invoke(options);
        });
        return services;
    }
}
