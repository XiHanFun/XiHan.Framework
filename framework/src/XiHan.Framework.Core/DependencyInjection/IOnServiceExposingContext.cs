// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务暴露时上下文接口
/// </summary>
public interface IOnServiceExposingContext
{
    /// <summary>
    /// 服务实现类型
    /// </summary>
    Type ImplementationType { get; }

    /// <summary>
    /// 暴露的服务类型
    /// </summary>
    List<ServiceIdentifier> ExposedTypes { get; }
}
