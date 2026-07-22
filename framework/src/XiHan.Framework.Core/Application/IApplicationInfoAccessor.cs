// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 应用信息访问器接口
/// </summary>
public interface IApplicationInfoAccessor
{
    /// <summary>
    /// 应用程序的名称
    /// 这对于有多个应用程序、应用程序资源位于一起的系统来说是很有用的
    /// </summary>
    string? ApplicationName { get; }

    /// <summary>
    /// 此应用程序实例的唯一标识符
    /// 当应用程序重新启动时，这个值会改变
    /// </summary>
    string InstanceId { get; }
}
