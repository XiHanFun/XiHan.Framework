// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Kernel;

/// <summary>
/// 框架特性契约。任何类实现此接口即可作为一个特性（模块/插件/扩展），
/// 通过 <c>UseFeature&lt;T&gt;()</c> 注册到应用中。
/// 没有强制生命周期钩子，没有依赖声明要求。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IXiHanFeature
{
    /// <summary>
    /// 特性名称。默认返回类型名。
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    /// 配置此特性的服务注册。
    /// </summary>
    void Configure(IFeatureConfigurationContext context);
}

/// <summary>
/// 特性配置上下文，提供对 IServiceCollection 和 IConfiguration 的访问。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IFeatureConfigurationContext
{
    /// <summary>
    /// 服务集合。
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// 应用配置。
    /// </summary>
    IConfiguration Configuration { get; }
}
