// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.


namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单所描述的包的种类。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum ManifestKind
{
    /// <summary>能力包：编译期引用、同进程、可信。</summary>
    Capability = 0,

    /// <summary>Provider：某个抽象的实现。</summary>
    Provider = 1,

    /// <summary>插件：运行期加载、隔离。</summary>
    Plugin = 2,

    /// <summary>宿主适配。</summary>
    Host = 3,

    /// <summary>协议适配器。</summary>
    Protocol = 4,
}
