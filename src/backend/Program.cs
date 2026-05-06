using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;

var builder = WebApplication.CreateBuilder(args);

var socketPath = Environment.GetEnvironmentVariable(Constant.LISTEN_SOCK_ENV_VAR_NAME)!;
var faissUrl = builder.Configuration.GetConnectionString(Constant.FAISS_URL_CONN_STRING_NAME)!;

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.UseUnixSocketFromEnv(socketPath);

var normalization = JsonSerializer.Deserialize(
    File.ReadAllText(Constant.NORMALIZATION_JSON_FILE_PATH), JsonContext.Default.NormalizationConfig)!;

var mccRiskConfig = new MccRiskConfig
{
    Values = JsonSerializer.Deserialize(
        File.ReadAllText(Constant.RISK_JSON_FILE_PATH), JsonContext.Default.DictionaryStringDouble)!
};

builder.Services.AddHttpClient<FaissClient>(client =>
{
    client.BaseAddress = new Uri(faissUrl!);
    client.Timeout = TimeSpan.FromSeconds(Constant.FAISS_TIMEOUT_SECONDS);
});

if (builder.Environment.IsProduction() || builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Error);
}

builder.Services.AddSingleton(normalization);
builder.Services.AddSingleton(mccRiskConfig);
builder.Services.AddSingleton<VectorService>();
builder.Services.AddSingleton<FraudService>();
builder.Services.AddSingleton<WarmupService>();

var app = builder.Build();

await app.UseWarmupWithRetryAsync(
    retryCount: Constant.WARMUP_RETRY_COUNT,
    delaySeconds: Constant.WARMUP_RETRY_DELAY_SECONDS);

app.UseUnixSocketPermissions(socketPath);

app.MapGet("/ready", () => Results.Ok());

app.MapPost("/fraud-score", async (
    FraudRequest fraudRequest,
    FraudService fraudService
) => await fraudService.ProcessAsync(fraudRequest));

app.Run();