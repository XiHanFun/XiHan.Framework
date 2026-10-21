// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 能力包入口。一个实例对应一份清单；通过 <see cref="AppBuilder.UseFeature{TFeature}()"/> 显式加入，内核不扫描、不推断。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IFeature
{
    /// <summary>
    /// 清单。源生成器可从 <see cref="Capabilities.CapabilityAttribute"/> 生成此属性。
    /// </summary>
    Manifest Manifest { get; }

    /// <summary>
    /// 注册服务、贡献中间件、读取绑定。在依赖解析完成后、按拓扑顺序调用。
    /// </summary>
    void Configure(FeatureContext context);
}
