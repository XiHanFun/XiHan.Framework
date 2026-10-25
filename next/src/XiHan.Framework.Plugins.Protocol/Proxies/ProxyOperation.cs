// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Plugins.Protocol.Proxies;

/// <summary>
/// 握手声明的一个操作：契约、Schema 与是否流式。
/// </summary>
/// <param name="Contract">契约。</param>
/// <param name="Schema">请求与响应的 JSON Schema；可空。</param>
/// <param name="Streaming">以 <c>InvokeStream</c> 调用。</param>
internal sealed record ProxyOperation(OperationContract Contract, OperationSchema? Schema, bool Streaming);
