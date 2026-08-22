using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rah_Negar.Foundation.Application.Security;

public static class VendorAuthorizationPayloadVersions
{
    public const string Version1 = "1";
}

public sealed record VendorAuthorizationPayload(
    string DeviceId,
    string RequestId,
    VendorSupportAction Action,
    decimal ProposedEsdAdjustment,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string PayloadVersion = VendorAuthorizationPayloadVersions.Version1);

public interface IVendorAuthorizationPayloadSerializer
{
    byte[] SerializeCanonical(VendorAuthorizationPayload payload);
    bool TryDeserializeCanonical(ReadOnlySpan<byte> utf8Payload, out VendorAuthorizationPayload? payload);
}

/// <summary>Canonical V1 JSON: fixed property order, UTF-8, UTC timestamps and invariant decimal string.</summary>
public sealed class CanonicalVendorAuthorizationPayloadSerializer : IVendorAuthorizationPayloadSerializer
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public byte[] SerializeCanonical(VendorAuthorizationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateRequired(payload.DeviceId, nameof(payload.DeviceId));
        ValidateRequired(payload.RequestId, nameof(payload.RequestId));
        ValidateRequired(payload.PayloadVersion, nameof(payload.PayloadVersion));
        if (payload.IssuedAtUtc.Offset != TimeSpan.Zero || payload.ExpiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Payload timestamps must be UTC.", nameof(payload));

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("payloadVersion", payload.PayloadVersion);
            writer.WriteString("deviceId", payload.DeviceId);
            writer.WriteString("requestId", payload.RequestId);
            writer.WriteString("action", payload.Action.ToString());
            writer.WriteString("proposedEsdAdjustment", payload.ProposedEsdAdjustment.ToString("G29", CultureInfo.InvariantCulture));
            writer.WriteString("issuedAtUtc", payload.IssuedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            writer.WriteString("expiresAtUtc", payload.ExpiresAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    public bool TryDeserializeCanonical(ReadOnlySpan<byte> utf8Payload, out VendorAuthorizationPayload? payload)
    {
        payload = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Payload.ToArray());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 7)
                return false;
            string version = RequiredString(root, "payloadVersion");
            string deviceId = RequiredString(root, "deviceId");
            string requestId = RequiredString(root, "requestId");
            if (!Enum.TryParse(RequiredString(root, "action"), false, out VendorSupportAction action) ||
                !Enum.IsDefined(action)) return false;
            if (!decimal.TryParse(RequiredString(root, "proposedEsdAdjustment"), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal proposed)) return false;
            if (!DateTimeOffset.TryParseExact(RequiredString(root, "issuedAtUtc"), TimestampFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset issued) ||
                !DateTimeOffset.TryParseExact(RequiredString(root, "expiresAtUtc"), TimestampFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset expires)) return false;

            var candidate = new VendorAuthorizationPayload(deviceId, requestId, action, proposed,
                issued.ToUniversalTime(), expires.ToUniversalTime(), version);
            byte[] canonical = SerializeCanonical(candidate);
            if (!utf8Payload.SequenceEqual(canonical)) return false;
            payload = candidate;
            return true;
        }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString())) throw new FormatException("Required payload field is absent.");
        return value.GetString()!;
    }

    private static void ValidateRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
    }
}

public sealed record VendorSignedAuthorizationEnvelope(
    string KeyId,
    string EnvelopeVersion,
    byte[] PayloadUtf8,
    byte[] Signature);

public static class VendorSignedAuthorizationEnvelopeCodec
{
    public const string CurrentEnvelopeVersion = "1";

    public static string Encode(VendorSignedAuthorizationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["envelopeVersion"] = envelope.EnvelopeVersion,
            ["keyId"] = envelope.KeyId,
            ["payload"] = Convert.ToBase64String(envelope.PayloadUtf8),
            ["signature"] = Convert.ToBase64String(envelope.Signature)
        });
    }

    public static bool TryDecode(ReadOnlyMemory<char> encoded, out VendorSignedAuthorizationEnvelope? envelope)
    {
        envelope = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetBytes(encoded.ToString()));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4) return false;
            string version = Get(root, "envelopeVersion");
            string keyId = Get(root, "keyId");
            byte[] payload = Convert.FromBase64String(Get(root, "payload"));
            byte[] signature = Convert.FromBase64String(Get(root, "signature"));
            if (payload.Length == 0 || signature.Length == 0) return false;
            envelope = new(keyId, version, payload, signature);
            return true;
        }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    private static string Get(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new FormatException();
}
