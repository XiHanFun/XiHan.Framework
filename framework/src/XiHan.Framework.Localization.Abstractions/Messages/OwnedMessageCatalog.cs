// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Localization.Abstractions.Messages;

/// <summary>
/// 配置阶段按归属注册的一份目录
/// </summary>
/// <remarks>
/// 由 <see cref="LocalizationServiceCollectionExtensions.AddMessageCatalog(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, MessageCatalog)"/> 放进容器，建立 <see cref="MessageCatalogRegistry"/> 时按归属成组登记
/// </remarks>
/// <param name="Owner">归属</param>
/// <param name="Catalog">目录</param>
internal sealed record OwnedMessageCatalog(string Owner, MessageCatalog Catalog);
