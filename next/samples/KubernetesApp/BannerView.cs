// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace KubernetesApp;

/// <summary>
/// 首页：横幅与应答的 Pod。
/// </summary>
/// <param name="Banner">横幅文本。</param>
/// <param name="Pod">主机名（Pod 名）。</param>
public sealed record BannerView(string Banner, string Pod);
