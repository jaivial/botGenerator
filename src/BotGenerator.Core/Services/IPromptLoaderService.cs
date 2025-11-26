namespace BotGenerator.Core.Services;

/// <summary>
/// Service for loading and assembling prompts from external files.
/// </summary>
public interface IPromptLoaderService
{
    /// <summary>
    /// Loads a prompt file for a specific restaurant.
    /// </summary>
    /// <param name="restaurantId">Restaurant identifier (folder name).</param>
    /// <param name="promptName">Prompt file name (without extension).</param>
    /// <returns>The raw prompt content.</returns>
    Task<string> LoadPromptAsync(string restaurantId, string promptName);

    /// <summary>
    /// Loads a shared prompt file.
    /// </summary>
    /// <param name="promptName">Prompt file name (without extension).</param>
    /// <returns>The raw prompt content.</returns>
    Task<string> LoadSharedPromptAsync(string promptName);

    /// <summary>
    /// Assembles the complete system prompt for a restaurant.
    /// Loads all prompt modules, replaces tokens, and combines them.
    /// </summary>
    /// <param name="restaurantId">Restaurant identifier.</param>
    /// <param name="context">Dictionary of token values to replace.</param>
    /// <returns>The fully assembled system prompt.</returns>
    Task<string> AssembleSystemPromptAsync(
        string restaurantId,
        Dictionary<string, object> context);

    /// <summary>
    /// Loads a specialized prompt (e.g., for rice validation).
    /// </summary>
    /// <param name="restaurantId">Restaurant identifier.</param>
    /// <param name="promptName">Specialized prompt name.</param>
    /// <param name="context">Token values to replace.</param>
    /// <returns>The processed prompt.</returns>
    Task<string> LoadSpecializedPromptAsync(
        string restaurantId,
        string promptName,
        Dictionary<string, object> context);

    /// <summary>
    /// Clears the prompt cache (useful when files change).
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the list of available restaurants.
    /// </summary>
    IEnumerable<string> GetAvailableRestaurants();
}
