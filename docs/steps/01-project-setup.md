# Step 01: Project Setup

In this step, we'll create the solution structure and install all necessary packages.

## 1.1 Create Solution and Projects

Open a terminal and run:

```bash
# Create solution directory
mkdir BotGenerator
cd BotGenerator

# Create solution file
dotnet new sln -n BotGenerator

# Create the API project (ASP.NET Core Web API)
dotnet new webapi -n BotGenerator.Api -o src/BotGenerator.Api

# Create the Core library (Class Library)
dotnet new classlib -n BotGenerator.Core -o src/BotGenerator.Core

# Create test projects
dotnet new xunit -n BotGenerator.Core.Tests -o tests/BotGenerator.Core.Tests
dotnet new xunit -n BotGenerator.Integration.Tests -o tests/BotGenerator.Integration.Tests

# Add projects to solution
dotnet sln add src/BotGenerator.Api/BotGenerator.Api.csproj
dotnet sln add src/BotGenerator.Core/BotGenerator.Core.csproj
dotnet sln add tests/BotGenerator.Core.Tests/BotGenerator.Core.Tests.csproj
dotnet sln add tests/BotGenerator.Integration.Tests/BotGenerator.Integration.Tests.csproj

# Add project references
dotnet add src/BotGenerator.Api/BotGenerator.Api.csproj reference src/BotGenerator.Core/BotGenerator.Core.csproj
dotnet add tests/BotGenerator.Core.Tests/BotGenerator.Core.Tests.csproj reference src/BotGenerator.Core/BotGenerator.Core.csproj
```

## 1.2 Install NuGet Packages

### For BotGenerator.Core:

```bash
cd src/BotGenerator.Core

# HTTP client for API calls
dotnet add package Microsoft.Extensions.Http

# Logging abstractions
dotnet add package Microsoft.Extensions.Logging.Abstractions

# Configuration abstractions
dotnet add package Microsoft.Extensions.Configuration.Abstractions

# JSON serialization
dotnet add package System.Text.Json

# Redis for chat memory (optional)
dotnet add package StackExchange.Redis

cd ../..
```

### For BotGenerator.Api:

```bash
cd src/BotGenerator.Api

# Reference to Core is already added
# Additional packages if needed:
dotnet add package Swashbuckle.AspNetCore  # Already included in webapi template

cd ../..
```

### For Tests:

```bash
cd tests/BotGenerator.Core.Tests

dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.Extensions.Logging.Abstractions

cd ../..
```

## 1.3 Create Folder Structure

```bash
# Create folders in Core project
mkdir -p src/BotGenerator.Core/Agents
mkdir -p src/BotGenerator.Core/Services
mkdir -p src/BotGenerator.Core/Handlers
mkdir -p src/BotGenerator.Core/Models

# Create prompts folder structure
mkdir -p src/BotGenerator.Prompts/restaurants/villacarmen
mkdir -p src/BotGenerator.Prompts/restaurants/example-restaurant
mkdir -p src/BotGenerator.Prompts/shared

# Create folders in API project
mkdir -p src/BotGenerator.Api/Controllers
```

## 1.4 Update Project Files

### src/BotGenerator.Core/BotGenerator.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.7.10" />
    <PackageReference Include="System.Text.Json" Version="8.0.4" />
  </ItemGroup>

</Project>
```

### src/BotGenerator.Api/BotGenerator.Api.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BotGenerator.Core\BotGenerator.Core.csproj" />
  </ItemGroup>

  <!-- Copy prompts folder to output -->
  <ItemGroup>
    <None Include="..\BotGenerator.Prompts\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>prompts\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </None>
  </ItemGroup>

</Project>
```

## 1.5 Configure appsettings.json

### src/BotGenerator.Api/appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "BotGenerator": "Debug"
    }
  },
  "AllowedHosts": "*",

  "GoogleAI": {
    "ApiKey": "YOUR_GOOGLE_AI_STUDIO_API_KEY_HERE",
    "Model": "gemini-2.5-flash-preview-05-20",
    "MaxOutputTokens": 2048,
    "Temperature": 0.7
  },

  "WhatsApp": {
    "Provider": "uazapi",
    "ApiUrl": "https://your-instance.uazapi.com",
    "Token": "YOUR_UAZAPI_TOKEN_HERE"
  },

  "Prompts": {
    "BasePath": "prompts",
    "CacheEnabled": true,
    "CacheDurationMinutes": 5
  },

  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "BotGenerator:",
    "DefaultDatabase": 0
  },

  "Restaurants": {
    "Default": "villacarmen",
    "Mapping": {
      "34638857294": "villacarmen"
    }
  }
}
```

### src/BotGenerator.Api/appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "BotGenerator": "Trace"
    }
  },

  "GoogleAI": {
    "ApiKey": "YOUR_DEV_API_KEY"
  },

  "WhatsApp": {
    "ApiUrl": "https://dev-instance.uazapi.com",
    "Token": "YOUR_DEV_TOKEN"
  }
}
```

## 1.6 Create User Secrets (for sensitive data)

Instead of putting API keys in appsettings.json, use User Secrets:

```bash
cd src/BotGenerator.Api

# Initialize user secrets
dotnet user-secrets init

# Set secrets
dotnet user-secrets set "GoogleAI:ApiKey" "your-actual-api-key"
dotnet user-secrets set "WhatsApp:Token" "your-actual-token"

cd ../..
```

## 1.7 Basic Program.cs Setup

### src/BotGenerator.Api/Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// We'll add more services in later steps
// builder.Services.AddHttpClient<IGeminiService, GeminiService>();
// builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
// etc.

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

## 1.8 Create Placeholder Controller

### src/BotGenerator.Api/Controllers/WebhookController.cs

```csharp
using Microsoft.AspNetCore.Mvc;

namespace BotGenerator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(ILogger<WebhookController> logger)
    {
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpPost("whatsapp-webhook")]
    public IActionResult HandleWhatsAppWebhook([FromBody] object body)
    {
        _logger.LogInformation("Received webhook: {Body}", body);

        // Placeholder - we'll implement this in Step 09
        return Ok(new { received = true });
    }
}
```

## 1.9 Verify Setup

Run the following commands to verify everything is set up correctly:

```bash
# Build the solution
dotnet build

# Run tests (they should pass, even if empty)
dotnet test

# Run the API
cd src/BotGenerator.Api
dotnet run
```

Visit `https://localhost:5001/swagger` (or the URL shown in console) to see the Swagger UI.

Test the health endpoint:
```bash
curl https://localhost:5001/api/webhook/health
```

Expected response:
```json
{"status":"healthy","timestamp":"2025-11-25T10:00:00.000Z"}
```

## 1.10 Git Setup (Optional)

```bash
# Initialize git
git init

# Create .gitignore
cat > .gitignore << 'EOF'
## .NET
bin/
obj/
*.user
*.suo
*.cache
*.dll
*.pdb

## IDE
.vs/
.vscode/
*.swp
.idea/

## User secrets
secrets.json

## Build
publish/
out/

## Logs
logs/
*.log

## Environment
.env
.env.local
appsettings.*.json
!appsettings.json
!appsettings.Development.json

## OS
.DS_Store
Thumbs.db
EOF

# Initial commit
git add .
git commit -m "Initial project setup"
```

## Summary

In this step, we:

1. Created the solution structure with API and Core projects
2. Installed necessary NuGet packages
3. Set up the folder structure
4. Configured appsettings.json with placeholders
5. Created a basic Program.cs
6. Added a placeholder webhook controller
7. Verified the setup works

## Next Step

Continue to [Step 02: Core Models](./02-core-models.md) where we'll define all the data models and enums used throughout the application.
