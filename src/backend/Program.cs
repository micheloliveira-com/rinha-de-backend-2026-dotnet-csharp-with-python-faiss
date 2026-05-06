using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------
// JSON CONFIG
// ----------------------------
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var socketPath = Environment.GetEnvironmentVariable("LISTEN_SOCK");
if (!string.IsNullOrEmpty(socketPath))
{
    if (File.Exists(socketPath)) File.Delete(socketPath);
    builder.WebHost.ConfigureKestrel(k => k.ListenUnixSocket(socketPath));
}

var warmupAsyncRetryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(
        retryCount: 60 * 10,
        sleepDurationProvider: _ => TimeSpan.FromSeconds(0.1),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Console.WriteLine($"Async Retry {retryCount}: {exception.GetType().Name} - {exception.Message}");
        });


// ----------------------------
// CONFIGS
// ----------------------------
var normalization = JsonSerializer.Deserialize<NormalizationConfig>(
    File.ReadAllText("Resources/normalization.json"), JsonContext.Default.NormalizationConfig)!;

var mccRisk = JsonSerializer.Deserialize<Dictionary<string, double>>(
    File.ReadAllText("Resources/mcc_risk.json"), JsonContext.Default.DictionaryStringDouble)!;
var url = builder.Configuration.GetConnectionString("faiss");

// ----------------------------
// HTTP CLIENT (FAISS PYTHON SERVICE)
// ----------------------------
builder.Services.AddHttpClient<FaissClient>(client =>
{
    client.BaseAddress = new Uri(url!);
    client.Timeout = TimeSpan.FromSeconds(2);
});
if (builder.Environment.IsProduction() || builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Error);
}
// ----------------------------
// DI
// ----------------------------
builder.Services.AddSingleton(normalization);
builder.Services.AddSingleton(mccRisk);

var app = builder.Build();

await warmupAsyncRetryPolicy.ExecuteAsync(async () =>
{
    using var scope = app.Services.CreateScope();
    Console.WriteLine("[FAISS] Starting warmup batch...");

    var faiss = scope.ServiceProvider.GetRequiredService<FaissClient>();

    var rand = new Random();
    var vector = new float[14]; // reused buffer

    for (int i = 0; i < 100; i++)
    {
        for (int j = 0; j < vector.Length; j++)
        {
            vector[j] = (float)rand.NextDouble();
        }

        try
        {
            await faiss.QueryAsync(vector);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAISS] Warmup failed at iter {i}: {ex.Message}");
            throw;
        }
    }

    Console.WriteLine("[FAISS] Warmup batch completed successfully.");
});

app.MapGet("/ready", () => Results.Ok());

// ----------------------------
// FRAUD ENDPOINT
// ----------------------------
app.MapPost("/fraud-score", async (
    FraudRequest req,
    NormalizationConfig norm,
    Dictionary<string, double> mcc,
    FaissClient faiss
) =>
{
    var vector = BuildVector(req, norm, mcc)
        .Select(x => (float)x)
        .ToArray();

    var result = await faiss.QueryAsync(vector);

    double score = result.FraudCount / 5.0;

    return Results.Ok(new FraudResponse(
        Approved: score < 0.6,
        FraudScore: score
    ));
});


if (!string.IsNullOrEmpty(socketPath))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        if (File.Exists(socketPath))
            File.SetUnixFileMode(socketPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
    });
}


app.Run();


// ======================================================
// VECTOR BUILDER
// ======================================================
static double[] BuildVector(
    FraudRequest req,
    NormalizationConfig norm,
    Dictionary<string, double> mccRisk)
{
    static double Clamp(double v)
        => Math.Max(0.0, Math.Min(1.0, v));

    var tx = req.Transaction;
    var customer = req.Customer;
    var merchant = req.Merchant;
    var terminal = req.Terminal;
    var last = req.LastTransaction;

    var v = new double[14];

    v[0] = Clamp(tx.Amount / norm.MaxAmount);
    v[1] = Clamp(tx.Installments / norm.MaxInstallments);

    var ratio = customer.AvgAmount == 0 ? 0 : tx.Amount / customer.AvgAmount;
    v[2] = Clamp(ratio / norm.AmountVsAvgRatio);

    var dt = ParseIsoUtc(tx.RequestedAt);
    v[3] = dt.Hour / 23.0;

    int dow = ((int)dt.DayOfWeek + 6) % 7;
    v[4] = dow / 6.0;

    if (last == null)
        v[5] = -1;
    else
    {
        var lastTime = ParseIsoUtc(last.Timestamp);
        v[5] = Clamp((dt - lastTime).TotalMinutes / norm.MaxMinutes);
    }

    v[6] = last == null ? -1 : Clamp(last.KmFromCurrent / norm.MaxKm);
    v[7] = Clamp(terminal.KmFromHome / norm.MaxKm);
    v[8] = Clamp(customer.TxCount24h / norm.MaxTxCount24h);

    v[9] = terminal.IsOnline ? 1 : 0;
    v[10] = terminal.CardPresent ? 1 : 0;

    v[11] = customer.KnownMerchants.Contains(merchant.Id) ? 0 : 1;

    v[12] = mccRisk.TryGetValue(merchant.Mcc, out var risk)
        ? risk
        : 0.5;

    v[13] = Clamp(merchant.AvgAmount / norm.MaxMerchantAvgAmount);

    return v;
}

// ======================================================
// FAST UTC PARSER
// ======================================================
static DateTime ParseIsoUtc(string s)
{
    int y = (s[0] - '0') * 1000 + (s[1] - '0') * 100 + (s[2] - '0') * 10 + (s[3] - '0');
    int M = (s[5] - '0') * 10 + (s[6] - '0');
    int d = (s[8] - '0') * 10 + (s[9] - '0');
    int h = (s[11] - '0') * 10 + (s[12] - '0');
    int m = (s[14] - '0') * 10 + (s[15] - '0');
    int sec = (s[17] - '0') * 10 + (s[18] - '0');

    return new DateTime(y, M, d, h, m, sec, DateTimeKind.Utc);
}

// ======================================================
// FAISS CLIENT
// ======================================================
public sealed class FaissClient
{
    private readonly HttpClient _http;

    public FaissClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<FaissResponse> QueryAsync(float[] vector)
    {
        var payload = new FaissRequest
        {
            Vector = vector
        };

        using var resp = await _http.PostAsJsonAsync(
            "/search",
            payload,
            JsonContext.Default.FaissRequest
        );

        resp.EnsureSuccessStatusCode();

        return (await resp.Content.ReadFromJsonAsync(
            JsonContext.Default.FaissResponse
        ))!;
    }
}