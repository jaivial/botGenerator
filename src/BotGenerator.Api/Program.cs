using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
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

// Override configuration with environment variables
var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_AI_API_KEY");
var whatsappApiUrl = Environment.GetEnvironmentVariable("WHATSAPP_API_URL");
var whatsappToken = Environment.GetEnvironmentVariable("WHATSAPP_TOKEN");
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
var mysqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

Console.WriteLine($"[ENV] GOOGLE_AI_API_KEY: {(string.IsNullOrEmpty(googleApiKey) ? "NOT SET" : "SET (" + googleApiKey?.Length + " chars)")}");
Console.WriteLine($"[ENV] WHATSAPP_API_URL: {(string.IsNullOrEmpty(whatsappApiUrl) ? "NOT SET" : whatsappApiUrl)}");
Console.WriteLine($"[ENV] WHATSAPP_TOKEN: {(string.IsNullOrEmpty(whatsappToken) ? "NOT SET" : "SET (" + whatsappToken?.Length + " chars)")}");
Console.WriteLine($"[ENV] MYSQL_CONNECTION_STRING: {(string.IsNullOrEmpty(mysqlConnectionString) ? "NOT SET (using default)" : "SET")}");

if (!string.IsNullOrEmpty(googleApiKey))
    builder.Configuration["GoogleAI:ApiKey"] = googleApiKey;
if (!string.IsNullOrEmpty(whatsappApiUrl))
    builder.Configuration["WhatsApp:ApiUrl"] = whatsappApiUrl;
if (!string.IsNullOrEmpty(whatsappToken))
    builder.Configuration["WhatsApp:Token"] = whatsappToken;
if (!string.IsNullOrEmpty(redisConnectionString))
    builder.Configuration["Redis:ConnectionString"] = redisConnectionString;
if (!string.IsNullOrEmpty(mysqlConnectionString))
    builder.Configuration["MySQL:ConnectionString"] = mysqlConnectionString;

// ========== Add Core Services ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== HTTP Clients ==========
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

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

// ========== Singleton Services ==========
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
builder.Services.AddSingleton<IConversationHistoryService, ConversationHistoryService>();
builder.Services.AddSingleton<IMenuRepository, MenuRepository>();
builder.Services.AddSingleton<IBookingRepository, BookingRepository>();

// ========== Scoped Services ==========
builder.Services.AddScoped<IIntentRouterService, IntentRouterService>();

// ========== Agents ==========
builder.Services.AddScoped<MainConversationAgent>();
builder.Services.AddScoped<RiceValidatorAgent>();
builder.Services.AddScoped<DateParserAgent>();
builder.Services.AddScoped<AvailabilityCheckerAgent>();

// ========== Handlers ==========
builder.Services.AddScoped<BookingHandler>();
builder.Services.AddScoped<CancellationHandler>();
builder.Services.AddScoped<ModificationHandler>();

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
