// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// 完整分组信息的取样应用服务（分组键 docs-alpha，Order 3）
/// </summary>
/// <remarks>
/// DynamicApiSwaggerGroupHelper 扫描的是 AppDomain 里所有已加载程序集中的 IApplicationService 实现，
/// 因此本文件里的每个取样服务都会真实进入扫描结果。分组键统一用 docs- 前缀，
/// 用例据此过滤掉框架自身可能引入的其他分组，保证断言是确定性的。
/// 同一分组的竞争特性一律写在同一个类上：GetOrderedClassAttributes 按 Order 升序排，
/// 跨类型的扫描顺序不受保证，写在同一个类上才不依赖反射返回特性的顺序。
/// </remarks>
[DynamicApi(Group = "docs-alpha", GroupName = "字母分组", Order = 3)]
public class DocsAlphaAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetAlphaAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 只给分组键、且键两侧带空白的取样应用服务（分组键 docs-beta，Order 0）
/// </summary>
public class DocsBetaAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    [DynamicApi(Group = "  docs-beta  ")]
    public Task<string> GetBetaAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 同分组两个不同 Order 的取样应用服务（分组键 docs-ordered，高 Order 胜出）
/// </summary>
[DynamicApi(Group = "docs-ordered", GroupName = "低优先级分组", Order = 1)]
[DynamicApi(Group = "docs-ordered", GroupName = "高优先级分组", Order = 9)]
public class DocsOrderedAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetOrderedAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 同 Order 下由后来者补齐显示名的取样应用服务（分组键 docs-fill）
/// </summary>
/// <remarks>
/// 两个特性 Order 相同，无论反射以何种顺序返回它们，合并结果都应当是「显示名被补齐、Order 保持 2」，
/// 用例因此不依赖特性顺序。
/// </remarks>
[DynamicApi(Group = "docs-fill", Order = 2)]
[DynamicApi(Group = "docs-fill", GroupName = "补名分组", Order = 2)]
public class DocsFillDisplayNameAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetFillAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 分组键仅写在方法上的取样应用服务（分组键 docs-method，Order 4）
/// </summary>
public class DocsMethodGroupAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    [DynamicApi(Group = "docs-method", GroupName = "方法级分组", Order = 4)]
    public Task<string> GetMethodScopedAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 分组键大小写不同的取样应用服务（合并后只剩一条，Order 5 的写法胜出）
/// </summary>
[DynamicApi(Group = "docs-case", GroupName = "小写分组", Order = 1)]
[DynamicApi(Group = "DOCS-CASE", GroupName = "大写分组", Order = 5)]
public class DocsCaseAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetCaseAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 已禁用动态 API 的取样应用服务（分组键 docs-disabled 不应进入文档）
/// </summary>
[DynamicApi(false, Group = "docs-disabled", GroupName = "禁用分组")]
public class DocsDisabledAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetDisabledAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 不在 API 浏览器中显示的取样应用服务（分组键 docs-hidden 不应进入文档）
/// </summary>
[DynamicApi(Group = "docs-hidden", GroupName = "隐藏分组", VisibleInApiExplorer = false)]
public class DocsHiddenAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetHiddenAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 分组键为纯空白的取样应用服务（不应产生任何分组）
/// </summary>
[DynamicApi(Group = "   ", GroupName = "空白分组")]
public class DocsBlankGroupAppService : IApplicationService
{
    /// <summary>
    /// 取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetBlankAsync(string id)
    {
        return Task.FromResult(id);
    }
}
