// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性配置上下文的默认实现。
/// </summary>
internal sealed class FeatureConfigurationContext : IFeatureConfigurationContext
{
    public FeatureConfigurationContext(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
    }

    public IServiceCollection Services { get; }
    public IConfiguration Configuration { get; }
}
