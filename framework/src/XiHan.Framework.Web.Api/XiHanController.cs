#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanController
// Guid:43b9cb63-7a79-4772-9e93-a8e2abc16fdd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 1:51:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Core.Aspects;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// XiHanController
/// </summary>
public abstract class XiHanController : Controller, IAvoidDuplicateCrossCuttingConcerns
{
    /// <summary>
    /// 缓存服务提供程序
    /// </summary>
    public ICachedServiceProvider CachedServiceProvider { get; set; } = null!;

    /// <summary>
    /// 应用的横切关注点
    /// </summary>
    public List<string> AppliedCrossCuttingConcerns => throw new NotImplementedException();
}
