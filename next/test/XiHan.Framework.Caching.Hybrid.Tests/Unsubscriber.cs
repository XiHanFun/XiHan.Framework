// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 释放时退订。
/// </summary>
internal sealed class Unsubscriber(Action unsubscribe) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => unsubscribe();
}
