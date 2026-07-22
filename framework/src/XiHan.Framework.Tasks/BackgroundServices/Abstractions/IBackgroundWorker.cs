// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Tasks.BackgroundServices.Abstractions;

/// <summary>
/// IBackgroundWorker
/// </summary>
public interface IBackgroundWorker : ISingletonDependency
{
}
