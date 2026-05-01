using System.Data;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Dapper;

namespace BotGenerator.Core.Services;

public class RestaurantConfigRepository : IRestaurantConfigRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RestaurantConfigRepository> _logger;

    public RestaurantConfigRepository(IConfiguration configuration, ILogger<RestaurantConfigRepository> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<RestaurantConfig?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var row = await connection.QueryFirstOrDefaultAsync<RestaurantRow>(
            "SELECT id, slug, name, contact_phone, contact_email, location, website_url, menu_url FROM restaurants WHERE slug = @Slug",
            new { Slug = slug });

        if (row == null) return null;

        return new RestaurantConfig
        {
            Id = row.slug,
            Name = row.name,
            ContactPhone = row.contact_phone ?? "",
            ContactEmail = row.contact_email ?? "",
            Location = row.location ?? "",
            WebsiteUrl = row.website_url ?? "",
            MenuUrl = row.menu_url ?? ""
        };
    }

    private class RestaurantRow
    {
        public int id { get; set; }
        public string slug { get; set; } = "";
        public string name { get; set; } = "";
        public string? contact_phone { get; set; }
        public string? contact_email { get; set; }
        public string? location { get; set; }
        public string? website_url { get; set; }
        public string? menu_url { get; set; }
    }
}
