using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

public interface IRestaurantConfigRepository
{
    Task<RestaurantConfig?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
