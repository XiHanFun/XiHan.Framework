// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件操作的请求。
/// </summary>
/// <param name="Script"><see cref="ConformanceScripts.Add"/> 返回的键。</param>
internal sealed record ConformanceScriptRequest(string Script);
