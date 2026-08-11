// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 严格隔离的多租户实体接口
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IMultiTenantEntity"/> 的读口径是「读共享」：租户态同时放行本租户行与 <c>TenantId=0</c> 的平台行，
/// 适用于字典、消息模板一类「平台维护、各租户共用」的基础数据。
/// </para>
/// <para>
/// 但写路径的租户守卫禁止租户态改写平台行（否则一个租户能改到影响所有租户的共享数据）。
/// 于是对「平台与租户各自拥有独立数据、不存在共用」的业务表，读共享会造成读写口径打架：
/// 平台态建的行在租户里看得见，一写就被守卫拦下。
/// </para>
/// <para>
/// 标记本接口即收紧为严格相等：租户态只看本租户行，平台态只看 <c>TenantId=0</c> 行。
/// 两侧都不再跨越，写守卫也就不会被触发。
/// </para>
/// </remarks>
public interface IStrictMultiTenantEntity : IMultiTenantEntity
{
}
