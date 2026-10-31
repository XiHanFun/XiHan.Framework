// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 本地化错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。区域名不合法不占用错误码：传给 API 或写在选项里的是编程或配置错误，在调用处抛出 <see cref="ArgumentException"/>，目录里的归 <see cref="InvalidCatalog"/>。原先登记的 <c>LOCALIZATION_VALIDATION_001</c> 已撤销，序号不复用。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class LocalizationErrors
{
    /// <summary>
    /// 消息目录不合法（JSON 结构、键或区域名错误）。
    /// </summary>
    public const string InvalidCatalog = "LOCALIZATION_VALIDATION_002";

    /// <summary>
    /// 同名消息目录里同一键同一区域的模板不一致。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="LocalizationServiceCollectionExtensions.AddMessageCatalog"/> 在注册时抛出，应用启动失败。同名目录的其余情形（内容相同、互为补充）合并为一份。
    /// </remarks>
    public const string DuplicateCatalog = "LOCALIZATION_CONFLICT_001";

    /// <summary>
    /// 数据库里没有这条翻译（<c>Localization.Database</c>）。
    /// </summary>
    public const string TranslationNotFound = "LOCALIZATION_NOTFOUND_001";
}
