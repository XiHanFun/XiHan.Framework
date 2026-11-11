// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 能力包入口
/// </summary>
/// <remarks>
/// 一个实例对应一份清单；通过 <see cref="AppBuilder.UseFeature{TFeature}()"/> 显式加入，内核不扫描、不推断
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IFeature
{
    /// <summary>
    /// 清单
    /// </summary>
    /// <remarks>
    /// 当前由特性手写；生成此属性的源生成器顺延（RFC-0002），<c>[Capability]</c> 特性随生成器一起提供（ADR-0135）
    /// </remarks>
    Manifest Manifest { get; }

    /// <summary>
    /// 注册服务、贡献中间件、读取绑定
    /// </summary>
    /// <remarks>
    /// 在依赖解析完成后、按拓扑顺序调用
    /// </remarks>
    void Configure(FeatureContext context);
}
