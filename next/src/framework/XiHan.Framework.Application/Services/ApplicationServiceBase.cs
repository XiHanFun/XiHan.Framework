using XiHan.Framework.Application.Contracts.Services;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// 表示应用服务基础类型。
/// </summary>
/// <remarks>
/// 当前阶段先提供最小基类，后续再按统一约定补充本地化、验证、授权、审计等横切能力接入点。
/// </remarks>
public abstract class ApplicationServiceBase : IApplicationService
{
}
