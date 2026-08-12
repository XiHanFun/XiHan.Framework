// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;
using XiHan.Framework.Web.Api.DynamicApi.Options;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 动态 API 路由与 HTTP 方法推导的测试
/// </summary>
/// <remarks>
/// 覆盖「方法名决定 HTTP 动词与路由片段」这条约定的正确性，
/// 重点是动词前缀必须按词边界匹配，不得把方法名腰斩。
/// </remarks>
public class DynamicApiRouteConventionTests
{
    /// <summary>
    /// 常规动词前缀推导出对应 HTTP 方法
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="expected">期望 HTTP 方法</param>
    [Theory]
    [InlineData(nameof(RouteSampleAppService.GetUserAsync), "GET")]
    [InlineData(nameof(RouteSampleAppService.QueryUsersAsync), "GET")]
    [InlineData(nameof(RouteSampleAppService.SearchUsersAsync), "GET")]
    [InlineData(nameof(RouteSampleAppService.CreateUserAsync), "POST")]
    [InlineData(nameof(RouteSampleAppService.UpdateUserAsync), "PUT")]
    [InlineData(nameof(RouteSampleAppService.DeleteUserAsync), "DELETE")]
    [InlineData(nameof(RouteSampleAppService.PartialUpdateUserAsync), "PATCH")]
    [InlineData(nameof(RouteSampleAppService.HandleUserAsync), "POST")]
    public void HttpMethod_IsInferredFromVerbPrefix(string methodName, string expected)
    {
        Assert.Equal(expected, ResolveHttpMethod(methodName));
    }

    /// <summary>
    /// 动词前缀与 Async 后缀被剥离
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="expected">期望动作名</param>
    [Theory]
    [InlineData(nameof(RouteSampleAppService.GetUserAsync), "User")]
    [InlineData(nameof(RouteSampleAppService.QueryUsersAsync), "Users")]
    [InlineData(nameof(RouteSampleAppService.CreateUserAsync), "User")]
    [InlineData(nameof(RouteSampleAppService.DeleteUserAsync), "User")]
    public void ActionName_StripsVerbPrefixAndAsyncSuffix(string methodName, string expected)
    {
        Assert.Equal(expected, ResolveActionName(methodName));
    }

    /// <summary>
    /// 动词前缀后必须是新词，方法名不得被腰斩
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <param name="expectedAction">期望动作名</param>
    /// <param name="expectedHttpMethod">期望 HTTP 方法</param>
    [Theory]
    [InlineData(nameof(RouteSampleAppService.AddressBookAsync), "AddressBook", "POST")]
    [InlineData(nameof(RouteSampleAppService.EditorTemplateAsync), "EditorTemplate", "POST")]
    [InlineData(nameof(RouteSampleAppService.ListingDetailAsync), "ListingDetail", "POST")]
    public void VerbPrefix_RequiresWordBoundary(string methodName, string expectedAction, string expectedHttpMethod)
    {
        Assert.Equal(expectedAction, ResolveActionName(methodName));
        Assert.Equal(expectedHttpMethod, ResolveHttpMethod(methodName));
    }

    /// <summary>
    /// 同义动词产出相同的动作名与 HTTP 方法
    /// </summary>
    [Fact]
    public void SynonymVerbs_ProduceSameEndpoint()
    {
        Assert.Equal(
            ResolveActionName(nameof(RouteSampleAppService.QueryUsersAsync)),
            ResolveActionName(nameof(RouteSampleAppService.SearchUsersAsync)));
        Assert.Equal(
            ResolveHttpMethod(nameof(RouteSampleAppService.QueryUsersAsync)),
            ResolveHttpMethod(nameof(RouteSampleAppService.SearchUsersAsync)));
    }

    /// <summary>
    /// HTTP 方法推断与动作名剥离对同一方法名得出一致的前缀判定
    /// </summary>
    /// <param name="methodName">方法名</param>
    [Theory]
    [InlineData(nameof(RouteSampleAppService.GetUserAsync))]
    [InlineData(nameof(RouteSampleAppService.AddressBookAsync))]
    [InlineData(nameof(RouteSampleAppService.PartialUpdateUserAsync))]
    [InlineData(nameof(RouteSampleAppService.EditorTemplateAsync))]
    [InlineData(nameof(RouteSampleAppService.HandleUserAsync))]
    public void HttpMethodAndActionName_AgreeOnPrefixMatch(string methodName)
    {
        var actionName = ResolveActionName(methodName);
        var withoutAsync = methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName[..^"Async".Length]
            : methodName;

        var prefixStripped = actionName != withoutAsync;
        var verbInferred = ResolveHttpMethod(methodName) != "POST";

        Assert.Equal(verbInferred, prefixStripped);
    }

    /// <summary>
    /// 控制器名移除服务后缀
    /// </summary>
    [Fact]
    public void ControllerName_RemovesServiceSuffix()
    {
        var options = new DynamicApiOptions();
        var context = new DynamicApiConventionContext { ServiceType = typeof(RouteSampleAppService) };

        new DefaultDynamicApiConvention(options).Apply(context);

        Assert.Equal("RouteSample", context.ControllerName);
    }

    /// <summary>
    /// 类级 Name 定制控制器名
    /// </summary>
    [Fact]
    public void ControllerName_HonorsClassLevelCustomName()
    {
        var options = new DynamicApiOptions();
        var context = new DynamicApiConventionContext { ServiceType = typeof(NamedSampleAppService) };

        new DefaultDynamicApiConvention(options).Apply(context);

        Assert.Equal("custom-sample", context.ControllerName);
    }

    /// <summary>
    /// 类级 Name 只作用于控制器名，动作名仍由方法名推导
    /// </summary>
    [Fact]
    public void ClassLevelCustomName_DoesNotAffectActionName()
    {
        var options = new DynamicApiOptions();
        var context = new DynamicApiConventionContext
        {
            ServiceType = typeof(NamedSampleAppService),
            MethodInfo = typeof(NamedSampleAppService).GetMethod(nameof(NamedSampleAppService.GetItemAsync))
        };

        new DefaultDynamicApiConvention(options).Apply(context);

        Assert.Equal("custom-sample", context.ControllerName);
        Assert.Equal("Item", context.ActionName);
    }

    /// <summary>
    /// 经约定解析指定方法的 HTTP 方法
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <returns>HTTP 方法</returns>
    private static string? ResolveHttpMethod(string methodName)
    {
        return ApplyConvention(methodName).HttpMethod;
    }

    /// <summary>
    /// 经约定解析指定方法的动作名
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <returns>动作名</returns>
    private static string? ResolveActionName(string methodName)
    {
        return ApplyConvention(methodName).ActionName;
    }

    /// <summary>
    /// 对指定方法应用默认约定
    /// </summary>
    /// <param name="methodName">方法名</param>
    /// <returns>约定上下文</returns>
    private static DynamicApiConventionContext ApplyConvention(string methodName)
    {
        var options = new DynamicApiOptions();
        var context = new DynamicApiConventionContext
        {
            ServiceType = typeof(RouteSampleAppService),
            MethodInfo = typeof(RouteSampleAppService).GetMethod(methodName)
        };

        new DefaultDynamicApiConvention(options).Apply(context);

        return context;
    }
}

/// <summary>
/// 路由推导测试用的应用服务
/// </summary>
public class RouteSampleAppService : IApplicationService
{
    /// <summary>
    /// 查询单个用户
    /// </summary>
    /// <returns></returns>
    public Task<string> GetUserAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 查询用户列表
    /// </summary>
    /// <returns></returns>
    public Task<string> QueryUsersAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 搜索用户列表
    /// </summary>
    /// <returns></returns>
    public Task<string> SearchUsersAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 新建用户
    /// </summary>
    /// <returns></returns>
    public Task<string> CreateUserAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 更新用户
    /// </summary>
    /// <returns></returns>
    public Task<string> UpdateUserAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <returns></returns>
    public Task<string> DeleteUserAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 局部更新用户
    /// </summary>
    /// <returns></returns>
    public Task<string> PartialUpdateUserAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 处理用户，方法名不含任何动词前缀
    /// </summary>
    /// <returns></returns>
    public Task<string> HandleUserAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 通讯录，名称以 Add 开头但并非新增动词
    /// </summary>
    /// <returns></returns>
    public Task<string> AddressBookAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 编辑器模板，名称以 Edit 开头但并非更新动词
    /// </summary>
    /// <returns></returns>
    public Task<string> EditorTemplateAsync() => Task.FromResult(string.Empty);

    /// <summary>
    /// 挂牌详情，名称以 List 开头但并非列表动词
    /// </summary>
    /// <returns></returns>
    public Task<string> ListingDetailAsync() => Task.FromResult(string.Empty);
}

/// <summary>
/// 类级 Name 定制控制器名的应用服务
/// </summary>
[DynamicApi(Name = "custom-sample")]
public class NamedSampleAppService : IApplicationService
{
    /// <summary>
    /// 查询条目
    /// </summary>
    /// <returns></returns>
    public Task<string> GetItemAsync() => Task.FromResult(string.Empty);
}
