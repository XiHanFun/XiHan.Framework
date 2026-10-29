// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 宿主应用的根容器。
/// </summary>
/// <remarks>
/// 回调服务由此开作用域，而不是用回调服务器自己的容器。
/// </remarks>
/// <param name="Root">宿主应用的根容器。</param>
internal sealed record HostServices(IServiceProvider Root);
