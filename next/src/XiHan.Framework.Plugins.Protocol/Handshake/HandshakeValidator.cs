// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;
using MessageCatalog = XiHan.Framework.Localization.Abstractions.Messages.MessageCatalog;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 核对握手响应与目录里的清单（<c>spec/plugin/handshake.md</c> §3）。
/// </summary>
internal static class HandshakeValidator
{
    /// <summary>
    /// 校验并整理握手结果。
    /// </summary>
    public static Result<HandshakeInfo> Validate(Manifest manifest, HandshakeResponse response, int primitives, PluginIsolation isolation)
    {
        if (!PluginProtocol.SupportedVersions.Contains(response.ProtocolVersion))
        {
            return Invalid(manifest.Id, $"protocol version {response.ProtocolVersion} is not supported by the host");
        }

        if (response.Primitives != primitives)
        {
            return Mismatch(manifest.Id, $"primitives version {response.Primitives} does not match the host's {primitives}");
        }

        var read = ManifestReader.Read(response.Manifest);
        if (!read.TryGetValue(out var declared))
        {
            return Mismatch(manifest.Id, $"the manifest in the handshake is invalid: {read.Error!.Message}");
        }

        if (declared.Kind != ManifestKind.Plugin || declared.Isolation != isolation)
        {
            return Mismatch(manifest.Id, $"the manifest in the handshake does not describe a plugin with isolation '{isolation}'");
        }

        var canonical = ManifestReader.Write(manifest);
        if (!string.Equals(ManifestReader.Write(declared), canonical, StringComparison.Ordinal))
        {
            return Mismatch(manifest.Id, "the manifest in the handshake differs from the manifest file");
        }

        var operations = new List<ProxyOperation>(response.Operations.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in response.Operations)
        {
            if (!manifest.Contributes.Operations.Contains(declaration.Id, StringComparer.Ordinal))
            {
                return Mismatch(manifest.Id, $"operation '{declaration.Id}' is not declared in contributes.operations");
            }

            if (!seen.Add(declaration.Id))
            {
                return Mismatch(manifest.Id, $"operation '{declaration.Id}' is declared more than once");
            }

            var operation = ToOperation(declaration);
            if (!operation.TryGetValue(out var proxy))
            {
                return Mismatch(manifest.Id, operation.Error!.Message);
            }

            operations.Add(proxy);
        }

        var subscriptions = new List<PluginSubscription>(response.Subscriptions.Count);
        foreach (var subscription in response.Subscriptions)
        {
            if (!TopicName.IsValid(subscription.Topic))
            {
                return Mismatch(manifest.Id, $"subscription topic '{subscription.Topic}' is not valid");
            }

            var item = new PluginSubscription(subscription.Topic, string.IsNullOrEmpty(subscription.MessageType) ? null : subscription.MessageType);
            if (subscriptions.Contains(item))
            {
                return Mismatch(manifest.Id, $"subscription '{subscription.Topic}' is declared more than once");
            }

            subscriptions.Add(item);
        }

        var catalogs = new List<MessageCatalog>(response.Catalogs.Count);
        foreach (var catalog in response.Catalogs)
        {
            try
            {
                var name = string.IsNullOrWhiteSpace(catalog.Name) ? manifest.Id.Split('.')[1] : catalog.Name;
                catalogs.Add(new MessageCatalog(name, catalog.Locales.ToDictionary(static l => l.Locale, static l => (IReadOnlyDictionary<string, string>)l.Templates.ToDictionary(static t => t.Key, static t => t.Value, StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase)));
            }
            catch (Exception exception) when (exception is ArgumentException or FrameworkException)
            {
                return Mismatch(manifest.Id, $"message catalog '{catalog.Name}' is invalid: {exception.Message}");
            }
        }

        var sdk = response.Sdk is { } info ? $"{info.Name} {info.Version} ({info.Language})".Trim() : string.Empty;
        return new HandshakeInfo(response.ProtocolVersion, canonical, operations, subscriptions, catalogs, sdk);
    }

    /// <summary>
    /// 声明 → 契约与 Schema。
    /// </summary>
    /// <remarks>
    /// ID、稳定性、暴露面或 Schema 不合法即失败。
    /// </remarks>
    private static Result<ProxyOperation> ToOperation(OperationDeclaration declaration)
    {
        if (!OperationId.IsValid(declaration.Id))
        {
            return Error.Validation(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", declaration.Id), ("reason", $"operation ID '{declaration.Id}' is not valid")));
        }

        var stability = Stability.Stable;
        if (!string.IsNullOrEmpty(declaration.Stability) && !Enum.TryParse(declaration.Stability, ignoreCase: true, out stability))
        {
            return Error.Validation(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", declaration.Id), ("reason", $"stability '{declaration.Stability}' is not valid")));
        }

        var expose = Expose.None;
        foreach (var protocol in declaration.Expose)
        {
            expose |= protocol.ToLowerInvariant() switch
            {
                "http" => Expose.Http,
                "grpc" => Expose.Grpc,
                "mcp" => Expose.Mcp,
                "commandline" => Expose.CommandLine,
                "ai" => Expose.Ai,
                _ => Expose.None,
            };
        }

        try
        {
            var contract = new OperationContract(
                declaration.Id,
                stability,
                expose,
                string.IsNullOrEmpty(declaration.Policy) ? null : declaration.Policy,
                string.IsNullOrEmpty(declaration.Permission) ? null : declaration.Permission);
            var schema = string.IsNullOrEmpty(declaration.RequestSchema) && string.IsNullOrEmpty(declaration.ResponseSchema)
                ? null
                : new OperationSchema(
                    string.IsNullOrEmpty(declaration.RequestSchema) ? "{}" : declaration.RequestSchema,
                    string.IsNullOrEmpty(declaration.ResponseSchema) ? "{}" : declaration.ResponseSchema);
            return new ProxyOperation(contract, schema, declaration.Streaming);
        }
        catch (Exception exception) when (exception is ArgumentException or FrameworkException)
        {
            return Error.Validation(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", declaration.Id), ("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 协议层面的握手失败。
    /// </summary>
    private static Error Invalid(string id, string reason)
        => new(PluginErrors.InvalidHandshake, PluginMessages.HandshakeInvalid, MessageArguments.Of(("id", id), ("reason", reason)));

    /// <summary>
    /// 与清单不一致。
    /// </summary>
    private static Error Mismatch(string id, string reason)
        => new(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", id), ("reason", reason)));
}
