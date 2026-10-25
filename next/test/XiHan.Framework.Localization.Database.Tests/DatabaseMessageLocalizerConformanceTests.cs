// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.Conformance;

namespace XiHan.Framework.Localization.Database.Tests;

/// <summary>
/// 没有数据库翻译时，<see cref="DatabaseMessageLocalizer"/> 跑完整套 <see cref="MessageLocalizerConformance"/> 用例。
/// </summary>
public sealed class DatabaseMessageLocalizerConformanceTests : MessageLocalizerConformance
{
    /// <inheritdoc />
    protected override IMessageLocalizer CreateLocalizer(IEnumerable<MessageCatalog> catalogs, LocalizationOptions options) => new DatabaseMessageLocalizer(catalogs, options);
}
