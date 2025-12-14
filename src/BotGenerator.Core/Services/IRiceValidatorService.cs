using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

public interface IRiceValidatorService
{
    Task<RiceValidationResult> ValidateAsync(
        string userRiceRequest,
        string restaurantId,
        CancellationToken cancellationToken = default);
}

