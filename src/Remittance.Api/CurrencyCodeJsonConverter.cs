using System.Text.Json;
using System.Text.Json.Serialization;
using Remittance.Core.Money;

namespace Remittance.Api;

public sealed class CurrencyCodeJsonConverter : JsonConverter<CurrencyCode>
{
    public override CurrencyCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        return CurrencyCode.TryParse(raw, out var code)
            ? code
            : throw new JsonException($"'{raw}' is not a valid currency code.");
    }

    public override void Write(Utf8JsonWriter writer, CurrencyCode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
