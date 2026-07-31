using BotGenerator.Api;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

// Load environment variables from .env file
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath))
{
    // Try parent directory (project root)
    envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
}
Console.WriteLine($"[ENV] Loading .env from: {Path.GetFullPath(envPath)}");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
    Console.WriteLine("[ENV] .env file loaded successfully");
}
else
{
    Console.WriteLine("[ENV] WARNING: .env file not found!");
}

var builder = WebApplication.CreateBuilder(args);

// Validate the DI container at build time so missing/misconfigured service
// registrations fail fast at startup instead of mid-conversation (e.g. a tool
// service resolved with a null dependency during a modification request).
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Override configuration with environment variables
var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_AI_API_KEY");
if (string.IsNullOrWhiteSpace(googleApiKey))
{
    googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
}
var minimaxApiKey = Environment.GetEnvironmentVariable("MINIMAX_API_KEY");
var whatsappApiUrl = Environment.GetEnvironmentVariable("WHATSAPP_API_URL");
var whatsappToken = Environment.GetEnvironmentVariable("WHATSAPP_TOKEN");
var whatsappProvider = Environment.GetEnvironmentVariable("WHATSAPP_PROVIDER");
var uazapiUrl = Environment.GetEnvironmentVariable("UAZAPI_URL");
var uazapiToken = Environment.GetEnvironmentVariable("UAZAPI_TOKEN");
var evolutionApiUrl = Environment.GetEnvironmentVariable("EVOLUTION_API_URL");
var evolutionApiKey = Environment.GetEnvironmentVariable("EVOLUTION_API_KEY");
var evolutionInstanceName = Environment.GetEnvironmentVariable("EVOLUTION_INSTANCE_NAME");
var evolutionWebhookSecret = Environment.GetEnvironmentVariable("EVOLUTION_WEBHOOK_SECRET");
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
var mysqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");
var externalBookingApiUrl = Environment.GetEnvironmentVariable("EXTERNAL_BOOKING_API_URL");
var externalBookingApiKey = Environment.GetEnvironmentVariable("EXTERNAL_BOOKING_API_KEY");
var chromaApiUrl = Environment.GetEnvironmentVariable("CHROMA_API_URL");
var chromaEnabled = Environment.GetEnvironmentVariable("CHROMA_ENABLED");
var chromaCollectionName = Environment.GetEnvironmentVariable("CHROMA_COLLECTION_NAME");

Console.WriteLine($"[ENV] GOOGLE_AI_API_KEY: {(string.IsNullOrEmpty(googleApiKey) ? "NOT SET" : "SET (" + googleApiKey?.Length + " chars)")}");
Console.WriteLine($"[ENV] MINIMAX_API_KEY: {(string.IsNullOrEmpty(minimaxApiKey) ? "NOT SET" : "SET (" + minimaxApiKey?.Length + " chars)")}");
Console.WriteLine($"[ENV] WHATSAPP_API_URL: {(string.IsNullOrEmpty(whatsappApiUrl) ? "NOT SET" : whatsappApiUrl)}");
Console.WriteLine($"[ENV] WHATSAPP_TOKEN: {(string.IsNullOrEmpty(whatsappToken) ? "NOT SET" : "SET (" + whatsappToken?.Length + " chars)")}");
Console.WriteLine($"[ENV] UAZAPI_URL: {(string.IsNullOrEmpty(uazapiUrl) ? "NOT SET" : uazapiUrl)}");
Console.WriteLine($"[ENV] UAZAPI_TOKEN: {(string.IsNullOrEmpty(uazapiToken) ? "NOT SET" : "SET (" + uazapiToken?.Length + " chars)")}");
Console.WriteLine($"[ENV] WHATSAPP_PROVIDER: {(string.IsNullOrEmpty(whatsappProvider) ? "NOT SET" : whatsappProvider)}");
Console.WriteLine($"[ENV] EVOLUTION_API_URL: {(string.IsNullOrEmpty(evolutionApiUrl) ? "NOT SET" : "SET")}");
Console.WriteLine($"[ENV] EVOLUTION_API_KEY: {(string.IsNullOrEmpty(evolutionApiKey) ? "NOT SET" : "SET")}");
Console.WriteLine($"[ENV] EVOLUTION_INSTANCE_NAME: {(string.IsNullOrEmpty(evolutionInstanceName) ? "NOT SET" : "SET")}");
Console.WriteLine($"[ENV] EVOLUTION_WEBHOOK_SECRET: {(string.IsNullOrEmpty(evolutionWebhookSecret) ? "NOT SET" : "SET")}");
Console.WriteLine($"[ENV] MYSQL_CONNECTION_STRING: {(string.IsNullOrEmpty(mysqlConnectionString) ? "NOT SET (using default)" : "SET")}");
Console.WriteLine($"[ENV] EXTERNAL_BOOKING_API_URL: {(string.IsNullOrEmpty(externalBookingApiUrl) ? "NOT SET" : externalBookingApiUrl)}");
Console.WriteLine($"[ENV] CHROMA_API_URL: {(string.IsNullOrEmpty(chromaApiUrl) ? "NOT SET" : chromaApiUrl)}");

if (!string.IsNullOrEmpty(googleApiKey))
    builder.Configuration["GoogleAI:ApiKey"] = googleApiKey;
if (!string.IsNullOrEmpty(minimaxApiKey))
    builder.Configuration["Minimax:ApiKey"] = minimaxApiKey;
if (!string.IsNullOrEmpty(whatsappApiUrl))
    builder.Configuration["WhatsApp:ApiUrl"] = whatsappApiUrl;
if (!string.IsNullOrEmpty(whatsappToken))
    builder.Configuration["WhatsApp:Token"] = whatsappToken;
if (!string.IsNullOrEmpty(whatsappProvider))
    builder.Configuration["WhatsApp:Provider"] = whatsappProvider;
// Backwards/alternative env var names for the same provider
if (string.IsNullOrEmpty(builder.Configuration["WhatsApp:ApiUrl"]) && !string.IsNullOrEmpty(uazapiUrl))
    builder.Configuration["WhatsApp:ApiUrl"] = uazapiUrl;
if (string.IsNullOrEmpty(builder.Configuration["WhatsApp:Token"]) && !string.IsNullOrEmpty(uazapiToken))
    builder.Configuration["WhatsApp:Token"] = uazapiToken;
if (!string.IsNullOrEmpty(evolutionApiUrl))
    builder.Configuration["WhatsApp:Evolution:ApiUrl"] = evolutionApiUrl;
if (!string.IsNullOrEmpty(evolutionApiKey))
    builder.Configuration["WhatsApp:Evolution:ApiKey"] = evolutionApiKey;
if (!string.IsNullOrEmpty(evolutionInstanceName))
    builder.Configuration["WhatsApp:Evolution:InstanceName"] = evolutionInstanceName;
if (!string.IsNullOrEmpty(evolutionWebhookSecret))
    builder.Configuration["WhatsApp:Evolution:WebhookSecret"] = evolutionWebhookSecret;
if (!string.IsNullOrEmpty(redisConnectionString))
    builder.Configuration["Redis:ConnectionString"] = redisConnectionString;
if (!string.IsNullOrEmpty(mysqlConnectionString))
    builder.Configuration["MySQL:ConnectionString"] = mysqlConnectionString;
if (!string.IsNullOrEmpty(externalBookingApiUrl))
    builder.Configuration["ExternalBooking:ApiUrl"] = externalBookingApiUrl;
if (!string.IsNullOrEmpty(externalBookingApiKey))
    builder.Configuration["ExternalBooking:ApiKey"] = externalBookingApiKey;
if (!string.IsNullOrEmpty(chromaApiUrl))
    builder.Configuration["Chroma:ApiUrl"] = chromaApiUrl;
if (!string.IsNullOrEmpty(chromaEnabled))
    builder.Configuration["Chroma:Enabled"] = chromaEnabled;
if (!string.IsNullOrEmpty(chromaCollectionName))
    builder.Configuration["Chroma:CollectionName"] = chromaCollectionName;

// Build MySQL connection string from .env variables if not explicitly provided
if (string.IsNullOrWhiteSpace(builder.Configuration["MySQL:ConnectionString"]))
{
    var isProd = builder.Environment.IsProduction();
    var prefix = isProd ? "HOSTINGER" : "LOCAL";

    var host = Environment.GetEnvironmentVariable($"DB_HOST_{prefix}");
    var user = Environment.GetEnvironmentVariable($"DB_USER_{prefix}");
    var pass = Environment.GetEnvironmentVariable($"DB_PASSWORD_{prefix}");
    var db = Environment.GetEnvironmentVariable($"DB_NAME_{prefix}");

    if (!string.IsNullOrWhiteSpace(host) &&
        !string.IsNullOrWhiteSpace(user) &&
        !string.IsNullOrWhiteSpace(pass) &&
        !string.IsNullOrWhiteSpace(db))
    {
        builder.Configuration["MySQL:ConnectionString"] = $"Server={host};Database={db};User={user};Password={pass};";
        Console.WriteLine($"[ENV] MySQL connection configured from DB_*_{prefix}");
    }
    else
    {
        Console.WriteLine($"[ENV] WARNING: MySQL connection string not configured. Set MYSQL_CONNECTION_STRING or DB_*_{prefix} variables.");
    }
}

var whatsappProviderName = builder.Configuration["WhatsApp:Provider"]?.Trim().ToLowerInvariant();
if (string.IsNullOrWhiteSpace(whatsappProviderName))
    whatsappProviderName = "evolution";
builder.Configuration["WhatsApp:Provider"] = whatsappProviderName;

if (whatsappProviderName is not "uazapi" and not "evolution")
{
    throw new InvalidOperationException(
        $"Unsupported WhatsApp:Provider '{whatsappProviderName}'. Supported values: uazapi, evolution.");
}

if (whatsappProviderName == "evolution")
{
    var missingEvolutionSettings = new[]
    {
        "WhatsApp:Evolution:ApiUrl",
        "WhatsApp:Evolution:ApiKey",
        "WhatsApp:Evolution:InstanceName",
        "WhatsApp:Evolution:WebhookSecret"
    }
    .Where(key => string.IsNullOrWhiteSpace(builder.Configuration[key]))
    .ToList();

    if (missingEvolutionSettings.Count > 0)
    {
        throw new InvalidOperationException(
            $"Evolution provider selected but required configuration is missing: {string.Join(", ", missingEvolutionSettings)}.");
    }

    var configuredEvolutionUrl = builder.Configuration["WhatsApp:Evolution:ApiUrl"]!;
    if (!Uri.TryCreate(configuredEvolutionUrl, UriKind.Absolute, out var evolutionUri) ||
        (!string.Equals(evolutionUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
         !string.Equals(evolutionUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("WhatsApp:Evolution:ApiUrl must be an absolute HTTP or HTTPS URL.");
    }
}

// ========== Add Core Services ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add memory cache for IMemoryCache dependencies
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IEvolutionWebhookDedupe, EvolutionWebhookDedupe>();

// ========== HTTP Clients ==========
// MiniMax Service - primary AI service (Anthropic-compatible endpoint)
// ClaudeService uses Anthropic SDK via IChatClient adapter (AIFunctionFactory.Create for tools).
// The SDK manages its own HttpClient; no AddHttpClient needed.
builder.Services.AddHttpClient<IGeminiService, MinimaxService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});

// External Booking Service
builder.Services.AddHttpClient<IExternalBookingService, ExternalBookingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// External Reservation Service (for PHP endpoint calls)
builder.Services.AddHttpClient<IExternalReservationService, ExternalReservationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

if (whatsappProviderName == "uazapi")
{
    builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var apiUrl = config["WhatsApp:ApiUrl"];
        if (!string.IsNullOrEmpty(apiUrl))
        {
            client.BaseAddress = new Uri(apiUrl);
        }
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}
else
{
    builder.Services.AddHttpClient<IWhatsAppService, EvolutionWhatsAppService>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var apiUrl = config["WhatsApp:Evolution:ApiUrl"]!.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(apiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}

builder.Services.AddHttpClient("Chroma", (serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var apiUrl = config["Chroma:ApiUrl"];
    if (!string.IsNullOrWhiteSpace(apiUrl))
    {
        client.BaseAddress = new Uri(apiUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(60); // Increased for ChromaDB operations
});

// ========== Singleton Services ==========
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
builder.Services.AddSingleton<IOpeningHoursService, OpeningHoursService>();
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
builder.Services.AddSingleton<IMessageRepository, MessageRepository>();
builder.Services.AddSingleton<IMenuRepository, MenuRepository>();
builder.Services.AddSingleton<IBookingRepository, BookingRepository>();
var bookingConfirmationOutboxOptions = BookingConfirmationOutboxOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(bookingConfirmationOutboxOptions);
builder.Services.AddSingleton<IBookingConfirmationOutboxRepository, BookingConfirmationOutboxRepository>();
builder.Services.AddTransient<BookingConfirmationOutboxProcessor>();
if (bookingConfirmationOutboxOptions.Enabled)
    builder.Services.AddHostedService<BookingConfirmationOutboxWorker>();
builder.Services.AddSingleton<IRestaurantConfigRepository, RestaurantConfigRepository>();
builder.Services.AddSingleton<IRiceMenuService, RiceMenuService>();
builder.Services.AddSingleton<IToolExecutor, ToolExecutor>();
builder.Services.AddHttpClient<IBookingAvailabilityService, BookingAvailabilityService>();
builder.Services.AddSingleton<IPendingBookingStore, PendingBookingStore>();
builder.Services.AddSingleton<ICallAutoReplyStore, CallAutoReplyStore>();

// ========== Singleton Services ==========
builder.Services.AddSingleton<IAiStateExtractorService, AiStateExtractorService>();

// ========== Agent (single AI-driven with tool calls) ==========
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
builder.Services.AddScoped<AgentOrchestrator>();

// ========== AI Message Understanding Services ==========
builder.Services.AddScoped<IAiBookingSelectionService, AiBookingSelectionService>();
builder.Services.AddScoped<IAiFieldSelectionService, AiFieldSelectionService>();
builder.Services.AddScoped<IAiIntentDetectionService, AiIntentDetectionService>();
builder.Services.AddScoped<IAiRiceUnderstandingService, AiRiceUnderstandingService>();

// ========== Logging ==========
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// ========== Configure Pipeline ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Disable HTTPS redirection in development for webhook compatibility
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
