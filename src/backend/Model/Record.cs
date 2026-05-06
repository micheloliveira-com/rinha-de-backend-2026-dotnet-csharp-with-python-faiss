
using System.Text.Json.Serialization;
public record Transaction(
    float Amount,
    int Installments,
    [property: JsonPropertyName("requested_at")] string RequestedAt);

public record Customer(
    [property: JsonPropertyName("avg_amount")] float AvgAmount,
    [property: JsonPropertyName("tx_count_24h")] int TxCount24h,
    [property: JsonPropertyName("known_merchants")] string[] KnownMerchants);

public record Merchant(
    string Id,
    string Mcc,
    [property: JsonPropertyName("avg_amount")] float AvgAmount);

public record Terminal(
    [property: JsonPropertyName("is_online")] bool IsOnline,
    [property: JsonPropertyName("card_present")] bool CardPresent,
    [property: JsonPropertyName("km_from_home")] float KmFromHome);

public record LastTransaction(
    string Timestamp,
    [property: JsonPropertyName("km_from_current")] float KmFromCurrent);

public record FraudRequest(
    string Id,
    Transaction Transaction,
    Customer Customer,
    Merchant Merchant,
    Terminal Terminal,
    [property: JsonPropertyName("last_transaction")] LastTransaction? LastTransaction);

public record FraudResponse(
    bool Approved,
    [property: JsonPropertyName("fraud_score")] float FraudScore);

public record Reference(
    [property: JsonPropertyName("vector")] float[] Vector,
    [property: JsonPropertyName("label")] string Label);

public record NormalizationConfig(
    [property: JsonPropertyName("max_amount")] float MaxAmount,
    [property: JsonPropertyName("max_installments")] float MaxInstallments,
    [property: JsonPropertyName("amount_vs_avg_ratio")] float AmountVsAvgRatio,
    [property: JsonPropertyName("max_minutes")] float MaxMinutes,
    [property: JsonPropertyName("max_km")] float MaxKm,
    [property: JsonPropertyName("max_tx_count_24h")] float MaxTxCount24h,
    [property: JsonPropertyName("max_merchant_avg_amount")] float MaxMerchantAvgAmount);

public record MccRiskConfig(Dictionary<string, float> Values);

public record FaissRequest(float[] Vector);
public record FaissResponse(int FraudCount);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(FraudRequest))]
[JsonSerializable(typeof(FraudResponse))]
[JsonSerializable(typeof(FaissRequest))]
[JsonSerializable(typeof(FaissResponse))]
[JsonSerializable(typeof(NormalizationConfig))]
[JsonSerializable(typeof(MccRiskConfig))]
[JsonSerializable(typeof(Dictionary<string, float>))]
[JsonSerializable(typeof(string))]
internal partial class JsonContext : JsonSerializerContext
{

}