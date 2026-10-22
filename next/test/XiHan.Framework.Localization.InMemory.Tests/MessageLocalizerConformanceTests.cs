// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Conformance;

namespace XiHan.Framework.Localization.InMemory.Tests;

/// <summary>
/// 让 <see cref="MessageLocalizer"/> 跑完整套 <see cref="MessageLocalizerConformance"/> 用例。
/// </summary>
public sealed class MessageLocalizerConformanceTests : MessageLocalizerConformance
{
    /// <inheritdoc />
    protected override IMessageLocalizer CreateLocalizer(IEnumerable<MessageCatalog> catalogs, LocalizationOptions options) => new MessageLocalizer(catalogs, options);
}
