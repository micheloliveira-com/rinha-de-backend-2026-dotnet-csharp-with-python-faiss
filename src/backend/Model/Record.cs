
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
public record Transaction(
    double Amount,
    int Installments,
    [property: JsonPropertyName("requested_at")] string RequestedAt);

public record Customer(
    [property: JsonPropertyName("avg_amount")] double AvgAmount,
    [property: JsonPropertyName("tx_count_24h")] int TxCount24h,
    [property: JsonPropertyName("known_merchants")] string[] KnownMerchants);

public record Merchant(
    string Id,
    string Mcc,
    [property: JsonPropertyName("avg_amount")] double AvgAmount);

public record Terminal(
    [property: JsonPropertyName("is_online")] bool IsOnline,
    [property: JsonPropertyName("card_present")] bool CardPresent,
    [property: JsonPropertyName("km_from_home")] double KmFromHome);

public record LastTransaction(
    string Timestamp,
    [property: JsonPropertyName("km_from_current")] double KmFromCurrent);

public record FraudRequest(
    string Id,
    Transaction Transaction,
    Customer Customer,
    Merchant Merchant,
    Terminal Terminal,
    [property: JsonPropertyName("last_transaction")] LastTransaction? LastTransaction);

public record FraudResponse(
    bool Approved,
    [property: JsonPropertyName("fraud_score")] double FraudScore);

public record Reference(
    [property: JsonPropertyName("vector")] float[] Vector,
    [property: JsonPropertyName("label")] string Label);

public record NormalizationConfig(
    [property: JsonPropertyName("max_amount")] double MaxAmount,
    [property: JsonPropertyName("max_installments")] double MaxInstallments,
    [property: JsonPropertyName("amount_vs_avg_ratio")] double AmountVsAvgRatio,
    [property: JsonPropertyName("max_minutes")] double MaxMinutes,
    [property: JsonPropertyName("max_km")] double MaxKm,
    [property: JsonPropertyName("max_tx_count_24h")] double MaxTxCount24h,
    [property: JsonPropertyName("max_merchant_avg_amount")] double MaxMerchantAvgAmount);

public class MccRiskConfig
{
    public Dictionary<string, double> Values { get; init; } = new();
}
public record FraudResult(bool approved, double fraud_score);

public class FraudVector
{
    public ulong Id { get; set; }
    public ReadOnlyMemory<float> Vector { get; set; }

    public bool IsFraud { get; set; }
}

public sealed class FaissRequest
{
    public float[] Vector { get; set; } = default!;
    }

public sealed class FaissResponse 
{

    public int FraudCount {get;set;}= default!;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FraudRequest))]
[JsonSerializable(typeof(FraudVector))]
[JsonSerializable(typeof(FraudResponse))]
[JsonSerializable(typeof(FraudResult))]
[JsonSerializable(typeof(FaissRequest))]
[JsonSerializable(typeof(FaissResponse))]

[JsonSerializable(typeof(NormalizationConfig))]
[JsonSerializable(typeof(MccRiskConfig))]
[JsonSerializable(typeof(Reference))]
[JsonSerializable(typeof(Dictionary<string, double>))]

// 👇 THESE ARE ALMOST ALWAYS THE MISSING ONES
[JsonSerializable(typeof(Transaction))]
[JsonSerializable(typeof(Customer))]
[JsonSerializable(typeof(Merchant))]
[JsonSerializable(typeof(Terminal))]
[JsonSerializable(typeof(LastTransaction))]
[JsonSerializable(typeof(string))]
internal partial class JsonContext : JsonSerializerContext
{

}