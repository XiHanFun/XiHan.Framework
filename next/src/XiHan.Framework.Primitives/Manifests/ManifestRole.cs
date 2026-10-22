// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 实现包在能力解析中的角色。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum ManifestRole
{
    /// <summary>普通 Provider。</summary>
    Provider = 0,

    /// <summary>默认实现：没有显式指定 Provider 时被选中。</summary>
    Default = 1,
}
