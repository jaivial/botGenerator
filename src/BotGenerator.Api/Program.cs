using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// ========== Add Core Services ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== HTTP Clients ==========
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ========== Singleton Services ==========
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
builder.Services.AddSingleton<IConversationHistoryService, ConversationHistoryService>();

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
