// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 单例目录读数据库的方式
/// </summary>
/// <remarks>
/// 在根提供者的新作用域里直接开只读工作单元取仓储，读完提交（<see cref="UnitOfWorkScope"/>，ADR-0259）；数据库故障回滚后以 <see cref="MultiTenancyErrors.StoreUnavailable"/> 返回。不经 <c>App.InvokeAsync</c> 再走一遍管道：目录是 Tenant 阶段自己要查的东西，嵌套调用会再过一遍 Tenant / Authorize 阶段（<c>RequireTenant</c> 为真时嵌套调用没有租户就失败，目录读自己成了自指），而且要求应用处于服务状态，启动期读不了。租户是平台级数据，<see cref="TenantRecord"/> 不实现 <c>IMultiTenant</c>，工作单元不带租户
/// </remarks>
internal sealed partial class CatalogInvocation
{
    /// <summary>
    /// 只读、无租户、读主库的工作单元选项
    /// </summary>
    /// <remarks>
    /// 目录只读不写，Provider 不必开写事务。目录决定租户进不进得来：读到落后的从库，停用就要多等复制延迟，读回来的旧值还要再缓存一轮（ADR-0122）
    /// </remarks>
    private static readonly UnitOfWorkOptions _readOnly = new() { IsReadOnly = true, ReadsPrimary = true };

    /// <summary>
    /// 根服务提供者
    /// </summary>
    /// <remarks>
    /// 每次读取开新作用域
    /// </remarks>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 日志
    /// </summary>
    /// <remarks>
    /// 记录数据库故障的原因：错误本身不带底层异常的文本，不对调用方暴露连接串、主机名一类信息
    /// </remarks>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public CatalogInvocation(IServiceProvider services, ILogger logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 在新作用域的只读工作单元里用仓储读取并返回结果
    /// </summary>
    /// <remarks>
    /// 数据库故障（连接、超时、提交失败）返回 <see cref="MultiTenancyErrors.StoreUnavailable"/>，调用方取消照常抛出；容器里没有工作单元是装配错误，照常抛出
    /// </remarks>
    public async ValueTask<Result<T>> ReadAsync<T>(Func<IRepository<TenantRecord, string>, CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        var result = await UnitOfWorkScope.RunAsync<T>(
            _services,
            _readOnly,
            async (services, ct) => Result.Success(await read(services.GetRequiredService<IRepository<TenantRecord, string>>(), ct).ConfigureAwait(false)),
            Unavailable,
            _logger,
            cancellationToken).ConfigureAwait(false);

        // 读取不返回失败、抛出的已经映射成存储不可达：外壳原样交还的其余失败只有提交返回的那一种
        if (result.IsFailure && !string.Equals(result.Error!.Code, MultiTenancyErrors.StoreUnavailable, StringComparison.Ordinal))
        {
            LogCommitFailed(_logger, result.Error.Code);
            return Unavailable(result.Error);
        }

        return result;
    }

    /// <summary>
    /// 把读取时抛出的异常归为存储不可达
    /// </summary>
    /// <remarks>
    /// 连接、超时（含驱动以取消异常报告的命令超时）、事务失败都算：租户读不到不能当成不存在，调用据此以 503 结束而不是 404。完整异常只进日志；框架自己的异常带错误码，留作内部错误
    /// </remarks>
    private Error Unavailable(Exception exception)
    {
        LogReadFailed(_logger, exception);
        return Unavailable(exception is FrameworkException framework ? framework.ToError() : null);
    }

    /// <summary>
    /// 存储不可达的错误
    /// </summary>
    /// <remarks>
    /// <paramref name="cause"/> 作为内部错误保留，不进响应
    /// </remarks>
    private static Error Unavailable(Error? cause)
        => new(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable, null, cause);

    /// <summary>
    /// 读租户目录失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the tenant catalog failed; the tenant store is reported as unavailable.")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 提交只读工作单元失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Committing the read-only unit of work of the tenant catalog failed with '{ErrorCode}'.")]
    private static partial void LogCommitFailed(ILogger logger, string errorCode);
}
