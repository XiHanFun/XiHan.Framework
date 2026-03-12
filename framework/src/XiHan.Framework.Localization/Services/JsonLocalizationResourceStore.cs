#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonLocalizationResourceStore
// Guid:67fdb226-f1c0-4d39-95b7-12dfbe035fe8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.VirtualFileSystem;
using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.Localization.Services;

/// <summary>
/// 基于虚拟文件系统的 JSON 本地化资源存储
/// </summary>
public sealed class JsonLocalizationResourceStore : IDisposable
{
    private readonly Lock _syncLock = new();
    private readonly IVirtualFileSystem _virtualFileSystem;
    private readonly IOptionsMonitor<XiHanLocalizationOptions> _optionsMonitor;
    private readonly IDisposable? _optionsChangeRegistration;
    private Dictionary<string, Dictionary<string, Dictionary<string, string>>> _resources = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// 初始化资源存储
    /// </summary>
    /// <param name="virtualFileSystem"></param>
    /// <param name="optionsMonitor"></param>
    public JsonLocalizationResourceStore(
        IVirtualFileSystem virtualFileSystem,
        IOptionsMonitor<XiHanLocalizationOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(virtualFileSystem);
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        _virtualFileSystem = virtualFileSystem;
        _optionsMonitor = optionsMonitor;

        _virtualFileSystem.OnFileChanged += OnVirtualFileChanged;
        _optionsChangeRegistration = _optionsMonitor.OnChange(_ => ResetCache());
    }

    /// <summary>
    /// 尝试获取本地化文本
    /// </summary>
    /// <param name="resourceName">资源名</param>
    /// <param name="culture">文化</param>
    /// <param name="name">键</param>
    /// <param name="value">文本</param>
    /// <returns></returns>
    public bool TryGetString(string resourceName, CultureInfo culture, string name, out string value)
    {
        value = string.Empty;
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        EnsureInitialized();
        var options = _optionsMonitor.CurrentValue;
        var normalizedResource = string.IsNullOrWhiteSpace(resourceName)
            ? options.DefaultResourceName
            : resourceName;

        var cultureChain = BuildCultureFallbackChain(culture, options);

        lock (_syncLock)
        {
            foreach (var cultureName in cultureChain)
            {
                if (TryGetFromResource(normalizedResource, cultureName, name, out value))
                {
                    return true;
                }

                if (!normalizedResource.Equals(options.DefaultResourceName, StringComparison.OrdinalIgnoreCase)
                    && TryGetFromResource(options.DefaultResourceName, cultureName, name, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取本地化资源集合
    /// </summary>
    /// <param name="resourceName">资源名</param>
    /// <param name="culture">文化</param>
    /// <param name="includeParentCultures">是否包含父文化</param>
    /// <returns></returns>
    public IReadOnlyList<LocalizedString> GetAllStrings(
        string resourceName,
        CultureInfo culture,
        bool includeParentCultures)
    {
        ArgumentNullException.ThrowIfNull(culture);
        EnsureInitialized();

        var options = _optionsMonitor.CurrentValue;
        var normalizedResource = string.IsNullOrWhiteSpace(resourceName)
            ? options.DefaultResourceName
            : resourceName;

        var cultureChain = includeParentCultures
            ? BuildCultureFallbackChain(culture, options)
            : [culture.Name];

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        lock (_syncLock)
        {
            foreach (var cultureName in Enumerable.Reverse(cultureChain))
            {
                if (!normalizedResource.Equals(options.DefaultResourceName, StringComparison.OrdinalIgnoreCase))
                {
                    MergeResourceEntries(options.DefaultResourceName, cultureName, merged);
                }

                MergeResourceEntries(normalizedResource, cultureName, merged);
            }
        }

        return merged
            .Select(x => new LocalizedString(x.Key, x.Value, resourceNotFound: false))
            .ToList();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _virtualFileSystem.OnFileChanged -= OnVirtualFileChanged;
        _optionsChangeRegistration?.Dispose();
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_syncLock)
        {
            if (_initialized)
            {
                return;
            }

            _resources = LoadResources();
            _initialized = true;
        }
    }

    private Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadResources()
    {
        var options = _optionsMonitor.CurrentValue;
        var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in EnumerateJsonFiles(options.ResourcesPath).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var fileInfo = _virtualFileSystem.GetFile(filePath);
            if (!fileInfo.Exists || fileInfo.IsDirectory)
            {
                continue;
            }

            using var stream = fileInfo.CreateReadStream();
            using var document = JsonDocument.Parse(stream);

            if (!TryResolveResourceAndCulture(filePath, document.RootElement, options, out var resourceName, out var cultureName))
            {
                continue;
            }

            var texts = ParseTexts(document.RootElement);
            if (texts.Count == 0)
            {
                continue;
            }

            if (!result.TryGetValue(resourceName, out var cultureMap))
            {
                cultureMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                result[resourceName] = cultureMap;
            }

            if (!cultureMap.TryGetValue(cultureName, out var textMap))
            {
                textMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                cultureMap[cultureName] = textMap;
            }

            foreach (var pair in texts)
            {
                textMap[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    private IEnumerable<string> EnumerateJsonFiles(string directoryPath)
    {
        var normalizedDirectory = NormalizeVirtualPath(directoryPath);
        var contents = _virtualFileSystem.GetDirectoryContents(normalizedDirectory);
        if (!contents.Exists)
        {
            yield break;
        }

        foreach (var item in contents)
        {
            var childPath = CombineVirtualPath(normalizedDirectory, item.Name);
            if (item.IsDirectory)
            {
                foreach (var nestedPath in EnumerateJsonFiles(childPath))
                {
                    yield return nestedPath;
                }

                continue;
            }

            if (item.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                yield return childPath;
            }
        }
    }

    private static bool TryResolveResourceAndCulture(
        string filePath,
        JsonElement root,
        XiHanLocalizationOptions options,
        out string resourceName,
        out string cultureName)
    {
        resourceName = options.DefaultResourceName;
        cultureName = options.DefaultCulture;

        var normalizedPath = NormalizeVirtualPath(filePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(normalizedPath);
        var directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? string.Empty;

        if (TryGetStringProperty(root, "resource", out var resourceFromJson) && !string.IsNullOrWhiteSpace(resourceFromJson))
        {
            resourceName = resourceFromJson;
        }
        else
        {
            resourceName = GetResourceNameFromPath(directory, options.DefaultResourceName);
        }

        if (TryGetStringProperty(root, "culture", out var cultureFromJson)
            && !string.IsNullOrWhiteSpace(cultureFromJson)
            && IsValidCultureCode(cultureFromJson))
        {
            cultureName = cultureFromJson;
            return true;
        }

        if (TryExtractCultureFromFileName(fileNameWithoutExt, out var cultureFromFileName, out var resourceFromFileName))
        {
            cultureName = cultureFromFileName;
            if (!string.IsNullOrWhiteSpace(resourceFromFileName))
            {
                resourceName = resourceFromFileName;
            }

            return true;
        }

        if (IsValidCultureCode(fileNameWithoutExt))
        {
            cultureName = fileNameWithoutExt;
            return true;
        }

        return IsValidCultureCode(cultureName);
    }

    private static Dictionary<string, string> ParseTexts(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (root.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        var textRoot = root;
        if (TryGetPropertyIgnoreCase(root, "texts", out var texts) && texts.ValueKind == JsonValueKind.Object)
        {
            textRoot = texts;
        }
        else if (TryGetPropertyIgnoreCase(root, "resources", out var resources) && resources.ValueKind == JsonValueKind.Object)
        {
            textRoot = resources;
        }

        FlattenJsonToDictionary(textRoot, null, result);
        return result;
    }

    private static void FlattenJsonToDictionary(
        JsonElement element,
        string? prefix,
        Dictionary<string, string> result)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrWhiteSpace(prefix)
                ? property.Name
                : $"{prefix}.{property.Name}";

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    FlattenJsonToDictionary(property.Value, key, result);
                    break;
                case JsonValueKind.Array:
                    result[key] = property.Value.ToString();
                    break;
                case JsonValueKind.String:
                    result[key] = property.Value.GetString() ?? string.Empty;
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    result[key] = property.Value.ToString();
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    result[key] = string.Empty;
                    break;
            }
        }
    }

    private static string GetResourceNameFromPath(string directoryPath, string defaultResourceName)
    {
        var segments = directoryPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0
            ? defaultResourceName
            : segments[^1];
    }

    private static bool TryExtractCultureFromFileName(
        string fileNameWithoutExtension,
        out string cultureName,
        out string resourceName)
    {
        cultureName = string.Empty;
        resourceName = string.Empty;

        var separatorIndex = fileNameWithoutExtension.LastIndexOf(".", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= fileNameWithoutExtension.Length - 1)
        {
            return false;
        }

        var maybeCulture = fileNameWithoutExtension[(separatorIndex + 1)..];
        if (!IsValidCultureCode(maybeCulture))
        {
            return false;
        }

        cultureName = maybeCulture;
        resourceName = fileNameWithoutExtension[..separatorIndex];
        return true;
    }

    private static bool IsValidCultureCode(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return false;
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(cultureCode);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private List<string> BuildCultureFallbackChain(CultureInfo culture, XiHanLocalizationOptions options)
    {
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            result.Add(culture.Name);
        }

        if (options.FallbackToParentCultures)
        {
            var parent = culture.Parent;
            while (!string.IsNullOrWhiteSpace(parent.Name))
            {
                result.Add(parent.Name);
                parent = parent.Parent;
            }
        }

        if (options.FallbackToDefaultCulture
            && IsValidCultureCode(options.DefaultCulture)
            && !result.Contains(options.DefaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(options.DefaultCulture);
        }

        if (result.Count == 0 && IsValidCultureCode(options.DefaultCulture))
        {
            result.Add(options.DefaultCulture);
        }

        return result;
    }

    private bool TryGetFromResource(string resourceName, string cultureName, string key, out string value)
    {
        value = string.Empty;

        if (!_resources.TryGetValue(resourceName, out Dictionary<string, Dictionary<string, string>>? cultureMap)
            || cultureMap is null)
        {
            return false;
        }

        if (!cultureMap.TryGetValue(cultureName, out Dictionary<string, string>? textMap)
            || textMap is null)
        {
            return false;
        }

        return textMap.TryGetValue(key, out value);
    }

    private void MergeResourceEntries(string resourceName, string cultureName, Dictionary<string, string> destination)
    {
        if (!_resources.TryGetValue(resourceName, out Dictionary<string, Dictionary<string, string>>? cultureMap)
            || cultureMap is null)
        {
            return;
        }

        if (!cultureMap.TryGetValue(cultureName, out Dictionary<string, string>? textMap)
            || textMap is null)
        {
            return;
        }

        foreach (var pair in textMap)
        {
            destination[pair.Key] = pair.Value;
        }
    }

    private void OnVirtualFileChanged(object? sender, FileChangedEventArgs eventArgs)
    {
        if (!eventArgs.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ResetCache();
    }

    private void ResetCache()
    {
        lock (_syncLock)
        {
            _resources = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            _initialized = false;
        }
    }

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeVirtualPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.TrimEnd('/');
    }

    private static string CombineVirtualPath(string left, string right)
    {
        var normalizedLeft = NormalizeVirtualPath(left);
        var normalizedRight = right.Replace('\\', '/').TrimStart('/');
        return $"{normalizedLeft}/{normalizedRight}";
    }
}
