// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace KubernetesApp.Endpoints;

/// <summary>
/// 当前配置：横幅、停机宽限与挂载的配置目录。
/// </summary>
/// <param name="Banner">横幅文本。</param>
/// <param name="StopTimeoutSeconds">内核停机宽限（秒）。</param>
/// <param name="ConfigPath">挂载的配置目录；本地运行时为空。</param>
public sealed record ConfigView(string Banner, string? StopTimeoutSeconds, string? ConfigPath);
