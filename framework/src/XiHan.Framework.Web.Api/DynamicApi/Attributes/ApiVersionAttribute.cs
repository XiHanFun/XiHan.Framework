#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiVersionAttribute
// Guid:e71f5506-68cf-4dfd-b971-0fa78aff2534
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Attributes;

/// <summary>
/// API 版本特性
/// <para>用于标识 API 的版本号，兼容 ASP.NET Core 标准版本控制</para>
/// <para>优先级：MapToApiVersion(方法) > DynamicApi.Version(方法) > ApiVersion(方法) > DynamicApi.Version(类) > ApiVersion(类) > DefaultApiVersion(配置)</para>
/// </summary>
/// <remarks>
/// <para><strong>使用场景：</strong></para>
/// <list type="bullet">
///   <item>为整个服务类指定默认版本号</item>
///   <item>为单个方法指定版本号</item>
///   <item>标记已废弃的 API（使用 Deprecated 属性）</item>
/// </list>
/// <para><strong>示例：</strong></para>
/// <code>
/// // 类级别版本
/// [ApiVersion("2")]
/// public class UserService : IApplicationService
/// {
///     // 所有方法默认使用 v2
///     public Task&lt;UserDto&gt; GetAsync(long id) { ... }
///     // 路由：api/v2/user/get/{id}
/// }
///
/// // 方法级别版本
/// public class ProductService : IApplicationService
/// {
///     [ApiVersion("1")]
///     public Task&lt;ProductDto&gt; GetAsync(long id) { ... }
///     // 路由：api/product/v1/get/{id}
///
///     [ApiVersion("2")]
///     public Task&lt;ProductDto&gt; GetDetailAsync(long id) { ... }
///     // 路由：api/product/v2/getdetail/{id}
/// }
///
/// // 标记废弃
/// [ApiVersion("1", Deprecated = true)]
/// public class LegacyService : IApplicationService { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ApiVersionAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="version">版本号</param>
    public ApiVersionAttribute(string version)
    {
        Version = version;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="majorVersion">主版本号</param>
    /// <param name="minorVersion">次版本号</param>
    public ApiVersionAttribute(int majorVersion, int minorVersion = 0)
    {
        Version = $"{majorVersion}.{minorVersion}";
    }

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 是否已弃用
    /// </summary>
    public bool Deprecated { get; set; }
}
