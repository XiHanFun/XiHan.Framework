#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MapToApiVersionAttribute
// Guid:b2c1bac6-3907-412a-973e-984d10fecadc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Attributes;

/// <summary>
/// 映射到 API 版本特性
/// <para>将方法映射到指定的 API 版本，该特性可以多次使用来支持多版本映射</para>
/// <para>优先级：MapToApiVersion(方法) > DynamicApi.Version(方法) > ApiVersion(方法) > DynamicApi.Version(类) > ApiVersion(类) > DefaultApiVersion(配置)</para>
/// </summary>
/// <remarks>
/// <para><strong>使用场景：</strong></para>
/// <list type="bullet">
///   <item>为方法指定特定的 API 版本</item>
///   <item>实验性功能的版本隔离（如 beta、alpha 版本）</item>
///   <item>渐进式版本升级（同时支持新旧版本）</item>
/// </list>
/// <para><strong>示例：</strong></para>
/// <code>
/// public class UserService : IApplicationService
/// {
///     // 映射到 v2 版本
///     [MapToApiVersion("2")]
///     public Task&lt;UserDto&gt; GetByIdAsync(long id) { ... }
///     // 生成路由：api/user/v2/getbyid/{id}
///
///     // 映射到 beta 版本
///     [MapToApiVersion("beta")]
///     public Task&lt;UserDto&gt; GetBetaFeatureAsync() { ... }
///     // 生成路由：api/user/vbeta/getbetafeature
///
///     // 多版本映射（当前使用第一个版本）
///     [MapToApiVersion("1")]
///     [MapToApiVersion("2")]
///     public Task&lt;UserDto&gt; GetCompatibleAsync() { ... }
///     // 生成路由：api/user/v1/getcompatible
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MapToApiVersionAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="version">版本号</param>
    public MapToApiVersionAttribute(string version)
    {
        Version = version;
    }

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; }
}
