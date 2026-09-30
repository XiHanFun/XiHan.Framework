// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 外层幂等过滤器测试夹具
/// </summary>
internal sealed class IdempotencyFilterTestContext : IAsyncDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public IdempotencyFilterTestContext(Action<XiHanIdempotencyOptions>? configure = null)
    {
        configure?.Invoke(Options);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<XiHanUnitOfWorkDefaultOptions>();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddSingleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        Provider = services.BuildServiceProvider();
        Store = new DefaultIdempotencyStore(Microsoft.Extensions.Options.Options.Create(Options), Clock,
            Provider.GetRequiredService<IUnitOfWorkManager>());
    }

    /// <summary>
    /// 幂等配置
    /// </summary>
    public XiHanIdempotencyOptions Options { get; } = new();

    /// <summary>
    /// 可推进的时钟
    /// </summary>
    public ManualTimeProvider Clock { get; } = new();

    /// <summary>
    /// 进程内存储
    /// </summary>
    public DefaultIdempotencyStore Store { get; }

    /// <summary>
    /// 被测过滤器使用的存储，为空时使用 <see cref="Store"/>
    /// </summary>
    public IIdempotencyStore? FilterStore { get; set; }

    /// <summary>
    /// 当前用户
    /// </summary>
    public FakeCurrentUser User { get; } = new() { IsAuthenticated = true, UserId = 42 };

    /// <summary>
    /// 当前租户
    /// </summary>
    public FakeCurrentTenant Tenant { get; } = new();

    /// <summary>
    /// 服务提供者
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// 请求中携带的表单文件，非空时请求按表单编码
    /// </summary>
    public List<IFormFile> FormFiles { get; } = [];

    /// <summary>
    /// 动作被真正执行的次数
    /// </summary>
    public int ActionInvocations { get; private set; }

    /// <summary>
    /// 创建被测过滤器
    /// </summary>
    public XiHanIdempotencyFilter CreateFilter()
    {
        var jsonOptions = new JsonOptions();
        jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        return new XiHanIdempotencyFilter(
            FilterStore ?? Store, User, Tenant,
            Microsoft.Extensions.Options.Options.Create(Options),
            Microsoft.Extensions.Options.Options.Create(jsonOptions),
            NullLogger<XiHanIdempotencyFilter>.Instance);
    }

    /// <summary>
    /// 执行一次过滤器；action 返回动作结果或抛出异常，completeInAction 模拟内层过滤器写入完成，
    /// throwFromNext 为真时动作异常从后续管道直接抛出，configureRequest 可调整请求
    /// </summary>
    public async Task<(IActionResult? ShortCircuit, HttpContext HttpContext)> ExecuteAsync(
        string actionName,
        string? key,
        IDictionary<string, object?> arguments,
        Func<IActionResult>? action = null,
        bool completeInAction = true,
        string path = "/api/idempotency-sample/orders",
        bool throwFromNext = false,
        Action<HttpRequest>? configureRequest = null)
    {
        var method = typeof(IdempotencySampleController).GetMethod(actionName)!;
        var httpContext = new DefaultHttpContext { RequestServices = Provider };
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = path;
        if (key is not null)
        {
            httpContext.Request.Headers[Options.HeaderName] = key;
        }

        if (FormFiles.Count > 0)
        {
            httpContext.Request.ContentType = "multipart/form-data; boundary=x";
            var files = new FormFileCollection();
            files.AddRange(FormFiles);
            httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        }

        configureRequest?.Invoke(httpContext.Request);

        var descriptor = new ControllerActionDescriptor
        {
            MethodInfo = method,
            ControllerTypeInfo = typeof(IdempotencySampleController).GetTypeInfo(),
            ActionName = method.Name,
            ControllerName = nameof(IdempotencySampleController),
            Parameters = method.GetParameters()
                .Select(parameter => (ParameterDescriptor)new ControllerParameterDescriptor
                {
                    Name = parameter.Name!,
                    ParameterType = parameter.ParameterType,
                    ParameterInfo = parameter,
                    BindingInfo = BindingInfo.GetBindingInfo(parameter.GetCustomAttributes())
                })
                .ToList()
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        var executing = new ActionExecutingContext(actionContext, [], arguments, controller: new object());

        await CreateFilter().OnActionExecutionAsync(executing, async () =>
        {
            ActionInvocations++;
            var executed = new ActionExecutedContext(actionContext, [], controller: new object());
            try
            {
                executed.Result = action?.Invoke() ?? new ObjectResult(new OrderResult(ActionInvocations, "SKU"));
                if (completeInAction && httpContext.Items[IdempotencyExecution.ItemKey] is IdempotencyExecution execution)
                {
                    await Store.CompleteAsync(execution.Key, execution.OwnerToken,
                        new StoredResponse(200, JsonSerializer.SerializeToUtf8Bytes(new { orderNo = ActionInvocations })));
                    execution.IsCompleted = true;
                }
            }
            catch (Exception ex) when (!throwFromNext)
            {
                executed.Exception = ex;
            }

            return executed;
        });

        return (executing.Result, httpContext);
    }

    /// <summary>
    /// 释放服务提供者
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return Provider.DisposeAsync();
    }
}

/// <summary>
/// 可设置的当前用户
/// </summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    /// <inheritdoc />
    public bool IsAuthenticated { get; set; }

    /// <inheritdoc />
    public long? UserId { get; set; }

    /// <inheritdoc />
    public string? UserName => null;

    /// <inheritdoc />
    public string? Name => null;

    /// <inheritdoc />
    public string? SurName => null;

    /// <inheritdoc />
    public string? PhoneNumber => null;

    /// <inheritdoc />
    public bool PhoneNumberVerified => false;

    /// <inheritdoc />
    public string? Email => null;

    /// <inheritdoc />
    public bool EmailVerified => false;

    /// <inheritdoc />
    public long? TenantId => null;

    /// <inheritdoc />
    public string[] Roles => [];

    /// <inheritdoc />
    public Claim? FindClaim(string claimType)
    {
        return null;
    }

    /// <inheritdoc />
    public Claim[] FindClaims(string claimType)
    {
        return [];
    }

    /// <inheritdoc />
    public Claim[] GetAllClaims()
    {
        return [];
    }

    /// <inheritdoc />
    public bool IsInRole(string roleName)
    {
        return false;
    }
}

/// <summary>
/// 可设置的当前租户
/// </summary>
internal sealed class FakeCurrentTenant : ICurrentTenant
{
    /// <inheritdoc />
    public bool IsAvailable => Id.HasValue;

    /// <inheritdoc />
    public long? Id { get; set; }

    /// <inheritdoc />
    public string? Name { get; set; }

    /// <inheritdoc />
    public IDisposable Change(long? id, string? name = null)
    {
        var (previousId, previousName) = (Id, Name);
        (Id, Name) = (id, name);
        return new RestoreScope(() => (Id, Name) = (previousId, previousName));
    }

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        public void Dispose()
        {
            restore();
        }
    }
}
