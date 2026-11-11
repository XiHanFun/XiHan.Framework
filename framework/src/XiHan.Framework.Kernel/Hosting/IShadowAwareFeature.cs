// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 被顶替时仍要登记服务的能力包入口
/// </summary>
/// <remarks>
/// 声明的能力全部被别的 Provider 顶替的节点（图里标为 shadowed）不装配、不启动（RFC-0002 解析规则 4）。实现本接口的特性被顶替时，内核在同一位置改调 <see cref="ConfigureShadowed"/>，只登记与能力选择无关的服务，例如对象存储的命名实例 Provider（RFC-0012）：不注册能力契约、不贡献中间件、操作与权限码。被顶替的特性不校验必需绑定，也不启动。没被顶替时照常调 <see cref="IFeature.Configure"/>，不调本方法
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IShadowAwareFeature : IFeature
{
    /// <summary>
    /// 被顶替时登记与能力选择无关的服务
    /// </summary>
    void ConfigureShadowed(FeatureContext context);
}
