#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSwaggerGenServiceCollectionExtensions
// Guid:3e93b49a-674f-4383-be13-473f17a0348a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/12 2:10:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using XiHan.Framework.AspNetCore.Mvc;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.AspNetCore.Swagger.Extensions.DependencyInjection;

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
            // 配置Swagger文档信息
            SwaggerInfoConfig(options);
            // 配置Swagger分组信息
            SwaggerGroupConfig(options);
            // 加载Swagger注释文档
            SwaggerDocLoad(options);
            // 配置Swagger文档请求 带JWT Token
            SwaggerJwtConfig(options);

            setupAction?.Invoke(options);
        });
        return services;
    }

    /// <summary>
    /// Swagger 文档信息配置
    /// </summary>
    /// <param name="options"></param>
    private static void SwaggerInfoConfig(SwaggerGenOptions options)
    {
        //// 需要暴露的分组
        //string[] publishGroup = AppSettings.Swagger.PublishGroup.GetSection();
        //// 利用枚举反射加载出每个分组的接口文档，Skip(1)是因为Enum第一个FieldInfo 是内置的一个 Int 值
        //typeof(ApiGroupNameEnum).GetFields().Skip(1).ToList().ForEach(group =>
        //{
        //    // 获取枚举值上的特性
        //    if (publishGroup.All(
        //            pGroup => !string.Equals(pGroup, group.Name, StringComparison.CurrentCultureIgnoreCase)))
        //    {
        //        return;
        //    }
        //    // 获取分组信息
        //    GroupInfoAttribute? info = group.GetCustomAttributes(typeof(GroupInfoAttribute), true).OfType<GroupInfoAttribute>()
        //        .FirstOrDefault();
        //    // 添加文档介绍
        //    options.SwaggerDoc(group.Name, new OpenApiInfo
        //    {
        //        Title = info?.Title,
        //        Version = info?.Version,
        //        Description = info?.Description +
        //                      $" Powered by {EnvironmentInfoHelper.FrameworkDescription} on {SystemInfoHelper.OperatingSystem}",
        //        Contact = new OpenApiContact
        //        {
        //            Name = "ZhaiFanhua",
        //            Email = "me@zhaifanhua.com",
        //            Url = new Uri("https://www.zhaifanhua.com")
        //        },
        //        License = new OpenApiLicense
        //        {
        //            Name = "MIT",
        //            Url = new Uri("http://license.coscl.org.cn/MulanPSL2")
        //        }
        //    });
        //    // 根据相对路径排序
        //    //options.OrderActionsBy(o => o.RelativePath);
        //});
    }

    /// <summary>
    /// 配置Swagger分组信息
    /// </summary>
    /// <param name="options"></param>
    private static void SwaggerGroupConfig(SwaggerGenOptions options)
    {
        //// 核心逻辑代码，指定分组被加载时回调进入。也就是 swagger 右上角下拉框内的分组加载时，每一个分组加载时都会遍历所有控制器的 action 进入一次这个方法体内，返回 true 就暴露，否则隐藏
        //options.DocInclusionPredicate((docName, apiDescription) =>
        //{
        //    // 反射获取基类 BaseApiController 的 ApiGroupAttribute 信息
        //    List<ApiGroupAttribute>? baseControllerAttributeList =
        //        ((ControllerActionDescriptor)apiDescription.ActionDescriptor)
        //        .ControllerTypeInfo.BaseType?
        //        .GetCustomAttributes(typeof(ApiGroupAttribute), true).OfType<ApiGroupAttribute>()
        //        .ToList();
        //    // 反射获取 ApiController 的 ApiGroupAttribute 信息
        //    var controllerAttributeList =
        //        ((ControllerActionDescriptor)apiDescription.ActionDescriptor)
        //        .ControllerTypeInfo
        //        .GetCustomAttributes(typeof(ApiGroupAttribute), true).OfType<ApiGroupAttribute>()
        //        .ToList();
        //    // 反射获取派生类 Action 的 ApiGroupAttribute 信息
        //    List<ApiGroupAttribute> actionAttributeList = apiDescription.ActionDescriptor.EndpointMetadata
        //        .Where(x => x is ApiGroupAttribute).OfType<ApiGroupAttribute>()
        //        .ToList();

        //    // 所有含 ApiGroupAttribute 的集合
        //    List<ApiGroupAttribute> apiGroupAttributeList = [];
        //    // 为空时插入空，减少 if 判断
        //    List<ApiGroupAttribute> emptyAttribute = [];
        //    apiGroupAttributeList.AddRange(baseControllerAttributeList ?? emptyAttribute);
        //    apiGroupAttributeList.AddRange(controllerAttributeList);
        //    apiGroupAttributeList.AddRange(actionAttributeList);

        //    // 判断所有的分组名称是否含有此名称
        //    if (apiGroupAttributeList.Count != 0)
        //    {
        //        List<bool> containList = [];
        //        // 遍历判断是否包含这个分组
        //        apiGroupAttributeList.ForEach(attribute =>
        //        {
        //            containList.Add(attribute.GroupNames.Any(x => x.ToString() == docName));
        //        });
        //        // 若有，则为该分组名称分配此 Action
        //        if (containList.Any(c => c))
        //        {
        //            return true;
        //        }
        //    }

        //    return false;
        //});
    }

    /// <summary>
    /// 加载Swagger注释文档
    /// </summary>
    /// <param name="options"></param>
    private static void SwaggerDocLoad(SwaggerGenOptions options)
    {
        // 枚举添加摘要
        options.UseInlineDefinitionsForEnums();

        try
        {
            // 生成注释文档，必须在 OperationFilter<AppendAuthorizeToSummaryOperationFilter>() 之前，否则没有(Auth)标签
            Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.xml").ToList().ForEach(xmlPath =>
            {
                // 默认的第二个参数是false，这个是 controller 的注释，true 时会显示注释，否则只显示方法注释
                options.IncludeXmlComments(xmlPath, true);
            });
        }
        catch (Exception ex)
        {
            throw new XiHanException("Swagger 注释文档加载失败！", ex);
        }
    }

    /// <summary>
    /// Swagger 文档中请求带 JWT Token
    /// </summary>
    /// <param name="options"></param>
    private static void SwaggerJwtConfig(SwaggerGenOptions options)
    {
        // 定义安全方案
        OpenApiSecurityScheme securitySchemeOAuth2 = new()
        {
            // JWT默认的参数名称
            Name = "Authorization",
            // 描述
            Description = "在下框中输入<code>token</code>进行身份验证",
            // 标识承载令牌的Bearer认证的数据格式，该信息主要是用于文档
            BearerFormat = "JWT",
            // 认证主题，在Type=Http时，只能是Basic和Bearer
            Scheme = "Bearer",
            // 表示认证信息发在Http请求的哪个位置
            In = ParameterLocation.Header,
            // 表示认证方式，有ApiKey，Http，OAuth2，OpenIdConnect四种，其中ApiKey是用的最多的
            Type = SecuritySchemeType.Http
        };

        // 定义认证，方案名称必须是 oauth2
        options.AddSecurityDefinition("oauth2", securitySchemeOAuth2);
        // 注册全局认证，即所有的接口都可以使用认证
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                // 必须与上面声明的一致，否则小绿锁混乱,即API全部会加小绿锁
                securitySchemeOAuth2,
                Array.Empty<string>()
            }
        });

        // 文档中显示安全小绿锁，在对应的 Action 上添加[Authorize]
        options.OperationFilter<AddResponseHeadersFilter>();
        // 安全小绿锁旁标记 Auth 标签
        options.OperationFilter<AppendAuthorizeToSummaryOperationFilter>();
        // 添加请求头的 Header 中的 token,传递到后台
        options.OperationFilter<SecurityRequirementsOperationFilter>();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<string> GetAllApiGroups()
    {
        var assembly = Assembly.GetExecutingAssembly(); // 替换为你的 Web 层程序集
        var controllerTypes = assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)) || type.IsSubclassOf(typeof(XiHanController)))
            .ToList();

        var groups = controllerTypes
            .SelectMany(type => type.GetCustomAttributes<ApiExplorerSettingsAttribute>())
            .Where(attr => !string.IsNullOrWhiteSpace(attr.GroupName))
            .Select(attr => attr.GroupName)
            .Distinct();

        return groups;
    }
}
