#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GatewayUsageExample
// Guid:8d9e0f1a-2b3c-4d5e-6f7a-8b9c0d1e2f3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Implementations;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Web.Gateway.Extensions;
using XiHan.Framework.Web.Gateway.Helpers;

namespace XiHan.Framework.Web.Gateway.Examples;

/// <summary>
/// 网关使用示例
/// </summary>
/// <remarks>
/// 本文件展示如何在实际项目中使用 Gateway 和 Traffic 模块
/// </remarks>
public static class GatewayUsageExample
{
    /// <summary>
    /// 示例 1: 基础配置
    /// </summary>
    public static void Example1_BasicSetup(WebApplicationBuilder builder)
    {
        // 1. 配置服务
        builder.Services.AddGateway(options =>
        {
            options.EnableGrayRouting = true;
            options.EnableRequestTracing = true;
            options.RequestTimeoutSeconds = 30;
            options.AllowedOrigins = ["http://localhost:3000"];
        });

        var app = builder.Build();

        // 2. 使用中间件
        app.UseGateway(); // 包含所有网关中间件

        app.MapControllers();
        app.Run();
    }

    /// <summary>
    /// 示例 2: 单独使用灰度路由
    /// </summary>
    public static void Example2_GrayRoutingOnly(WebApplicationBuilder builder)
    {
        builder.Services.AddGateway();

        var app = builder.Build();

        // 只使用灰度路由中间件
        app.UseGrayRouting();

        app.MapControllers();
        app.Run();
    }

    /// <summary>
    /// 示例 3: 添加自定义灰度规则
    /// </summary>
    public static void Example3_AddCustomGrayRules(IServiceCollection services)
    {
        services.AddGateway();

        // 在应用启动后添加规则
        var sp = services.BuildServiceProvider();
        var repository = sp.GetRequiredService<IGrayRuleRepository>();

        if (repository is InMemoryGrayRuleRepository memoryRepo)
        {
            // 添加一个百分比灰度规则
            memoryRepo.AddRule(new GrayRule
            {
                RuleId = "rule-percentage-20",
                RuleName = "20% 流量灰度",
                RuleType = GrayRuleType.Percentage,
                IsEnabled = true,
                Priority = 100,
                TargetVersion = "v2",
                Configuration = "{\"percentage\":20}",
                CreatedTime = DateTime.UtcNow,
                Remark = "20% 的流量路由到新版本"
            });

            // 添加一个用户白名单规则
            memoryRepo.AddRule(new GrayRule
            {
                RuleId = "rule-user-whitelist",
                RuleName = "用户白名单",
                RuleType = GrayRuleType.UserId,
                IsEnabled = true,
                Priority = 10,
                TargetVersion = "v2",
                Configuration = "{\"userIds\":[\"admin\",\"testuser\"]}",
                CreatedTime = DateTime.UtcNow,
                Remark = "指定用户访问新版本"
            });
        }
    }

    /// <summary>
    /// 示例 6: 注册自定义匹配器
    /// </summary>
    public static void Example6_RegisterCustomMatcher(IServiceCollection services)
    {
        services.AddGateway();

        // 注册自定义匹配器
        //services.AddGrayMatcher<DeviceTypeGrayMatcher>();
    }

    /// <summary>
    /// 示例 8: 灰度规则的生命周期管理
    /// </summary>
    public static void Example8_GrayRuleLifecycle(IServiceCollection services)
    {
        services.AddGateway();

        var sp = services.BuildServiceProvider();
        var repository = sp.GetRequiredService<IGrayRuleRepository>();

        if (repository is InMemoryGrayRuleRepository memoryRepo)
        {
            // 添加一个有时间限制的灰度规则
            memoryRepo.AddRule(new GrayRule
            {
                RuleId = "rule-limited-time",
                RuleName = "限时灰度",
                RuleType = GrayRuleType.Percentage,
                IsEnabled = true,
                Priority = 100,
                TargetVersion = "v2",
                Configuration = "{\"percentage\":50}",
                EffectiveTime = DateTime.UtcNow, // 立即生效
                ExpiryTime = DateTime.UtcNow.AddDays(7), // 7天后失效
                CreatedTime = DateTime.UtcNow,
                Remark = "7天限时灰度测试"
            });
        }
    }

    /// <summary>
    /// 示例 9: 完整的灰度发布流程
    /// </summary>
    public static class GrayReleaseProcess
    {
        // 阶段 1: 内部测试（用户白名单）
        public static GrayRule Phase1_InternalTest()
        {
            return new GrayRule
            {
                RuleId = "phase1-internal",
                RuleName = "内部测试",
                RuleType = GrayRuleType.UserId,
                IsEnabled = true,
                Priority = 10,
                TargetVersion = "v2",
                Configuration = "{\"userIds\":[\"internal-tester-1\",\"internal-tester-2\"]}",
                CreatedTime = DateTime.UtcNow
            };
        }

        // 阶段 2: 小范围灰度（5% 流量）
        public static GrayRule Phase2_SmallScale()
        {
            return new GrayRule
            {
                RuleId = "phase2-5percent",
                RuleName = "5% 流量灰度",
                RuleType = GrayRuleType.Percentage,
                IsEnabled = true,
                Priority = 100,
                TargetVersion = "v2",
                Configuration = "{\"percentage\":5}",
                CreatedTime = DateTime.UtcNow
            };
        }

        // 阶段 3: 扩大灰度（20% 流量）
        public static GrayRule Phase3_MediumScale()
        {
            return new GrayRule
            {
                RuleId = "phase3-20percent",
                RuleName = "20% 流量灰度",
                RuleType = GrayRuleType.Percentage,
                IsEnabled = true,
                Priority = 100,
                TargetVersion = "v2",
                Configuration = "{\"percentage\":20}",
                CreatedTime = DateTime.UtcNow
            };
        }

        // 阶段 4: 全量发布（50% 流量）
        public static GrayRule Phase4_LargeScale()
        {
            return new GrayRule
            {
                RuleId = "phase4-50percent",
                RuleName = "50% 流量灰度",
                RuleType = GrayRuleType.Percentage,
                IsEnabled = true,
                Priority = 100,
                TargetVersion = "v2",
                Configuration = "{\"percentage\":50}",
                CreatedTime = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 示例 4: 在 Controller 中使用灰度决策
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetProducts()
        {
            // 获取灰度决策
            var decision = HttpContext.GetGrayDecision();

            if (decision?.IsGray == true)
            {
                // 执行新版本逻辑
                return Ok(new
                {
                    Version = "v2",
                    Products = new[] { "Product A (New)", "Product B (New)" },
                    Message = $"命中灰度规则: {decision.MatchedRuleId}"
                });
            }

            // 执行旧版本逻辑
            return Ok(new
            {
                Version = "v1",
                Products = new[] { "Product A", "Product B" },
                Message = "正常版本"
            });
        }

        [HttpGet("{id}")]
        public IActionResult GetProductById(int id)
        {
            // 判断是否是灰度请求
            if (HttpContext.IsGrayRequest())
            {
                // 新版本：返回详细信息
                return Ok(new
                {
                    Id = id,
                    Name = "Product",
                    Description = "详细描述（新版本）",
                    Price = 99.99,
                    Stock = 100,
                    Images = new[] { "img1.jpg", "img2.jpg" }
                });
            }

            // 旧版本：返回基本信息
            return Ok(new
            {
                Id = id,
                Name = "Product",
                Price = 99.99
            });
        }
    }

    /// <summary>
    /// 示例 5: 自定义灰度匹配器
    /// </summary>
    public class DeviceTypeGrayMatcher : IGrayMatcher
    {
        public GrayRuleType RuleType => GrayRuleType.Custom;

        public bool IsMatch(GrayContext context, IGrayRule rule)
        {
            // 从 Header 中获取设备类型
            if (context.Headers == null || !context.Headers.TryGetValue("X-Device-Type", out var deviceType))
            {
                return false;
            }

            // 只对移动设备启用灰度
            return deviceType.Equals("mobile", StringComparison.OrdinalIgnoreCase);
        }

        public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsMatch(context, rule));
        }
    }

    /// <summary>
    /// 示例 7: 在中间件中使用灰度决策
    /// </summary>
    public class CustomMiddleware
    {
        private readonly RequestDelegate _next;

        public CustomMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 获取灰度决策
            var decision = context.GetGrayDecision();

            if (decision?.IsGray == true)
            {
                // 为灰度请求添加特殊 Header
                context.Response.Headers["X-Gray-Version"] = decision.TargetVersion ?? "unknown";
                context.Response.Headers["X-Gray-Rule"] = decision.MatchedRuleId ?? "unknown";
            }

            await _next(context);
        }
    }
}
