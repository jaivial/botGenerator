using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for accumulating partial field values across multiple conversation turns.
/// Enables natural language modification flow where users can provide information incrementally.
/// </summary>
public interface IFieldAccumulatorService
{
    /// <summary>
    /// Accumulates a field value into the modification state.
    /// </summary>
    /// <param name="state">Current modification state</param>
    /// <param name="fieldName">Name of the field (date, time, party_size, etc.)</param>
    /// <param name="value">Extracted value</param>
    /// <returns>New state with accumulated field</returns>
    ModificationState AccumulateField(ModificationState state, string fieldName, object value);

    /// <summary>
    /// Gets an accumulated field value from the state.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="state">Current modification state</param>
    /// <param name="fieldName">Name of the field</param>
    /// <returns>Accumulated value or default</returns>
    T? GetAccumulatedValue<T>(ModificationState state, string fieldName);

    /// <summary>
    /// Checks if all required fields have been accumulated.
    /// </summary>
    /// <param name="state">Current modification state</param>
    /// <param name="requiredFields">List of required field names</param>
    /// <returns>True if all required fields are present</returns>
    bool HasAllRequiredFields(ModificationState state, IEnumerable<string> requiredFields);

    /// <summary>
    /// Clears a specific accumulated field from the state.
    /// </summary>
    /// <param name="state">Current modification state</param>
    /// <param name="fieldName">Name of the field to clear</param>
    /// <returns>New state with field cleared</returns>
    ModificationState ClearAccumulatedField(ModificationState state, string fieldName);

    /// <summary>
    /// Gets the list of fields that have been accumulated.
    /// </summary>
    /// <param name="state">Current modification state</param>
    /// <returns>List of accumulated field names</returns>
    List<string> GetAccumulatedFieldNames(ModificationState state);

    /// <summary>
    /// Converts accumulated changes to BookingUpdateData for persistence.
    /// </summary>
    /// <param name="state">Current modification state</param>
    /// <returns>BookingUpdateData with accumulated changes</returns>
    BookingUpdateData ConvertToBookingUpdateData(ModificationState state);
}

/// <summary>
/// Implementation of field accumulator service.
/// </summary>
public class FieldAccumulatorService : IFieldAccumulatorService
{
    private readonly ILogger<FieldAccumulatorService> _logger;

    public FieldAccumulatorService(ILogger<FieldAccumulatorService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ModificationState AccumulateField(ModificationState state, string fieldName, object value)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
        }

        _logger.LogDebug(
            "Accumulating field {FieldName} with value {Value} for phone {Phone}",
            fieldName, value, state.PhoneNumber);

        var accumulatedChanges = state.AccumulatedChanges != null
            ? new Dictionary<string, object>(state.AccumulatedChanges)
            : new Dictionary<string, object>();

        accumulatedChanges[fieldName] = value;

        var extractedFields = state.ExtractedFields != null
            ? new List<string>(state.ExtractedFields)
            : new List<string>();

        if (!extractedFields.Contains(fieldName))
        {
            extractedFields.Add(fieldName);
        }

        return state with
        {
            AccumulatedChanges = accumulatedChanges,
            ExtractedFields = extractedFields,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public T? GetAccumulatedValue<T>(ModificationState state, string fieldName)
    {
        if (state?.AccumulatedChanges == null || !state.AccumulatedChanges.ContainsKey(fieldName))
        {
            return default;
        }

        try
        {
            var value = state.AccumulatedChanges[fieldName];
            
            if (value is T typedValue)
            {
                return typedValue;
            }

            // Try to convert the value
            var convertedValue = Convert.ChangeType(value, typeof(T));
            return (T?)convertedValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to convert accumulated field {FieldName} to type {Type}",
                fieldName,
                typeof(T).Name);
            return default;
        }
    }

    public bool HasAllRequiredFields(ModificationState state, IEnumerable<string> requiredFields)
    {
        if (state?.AccumulatedChanges == null)
        {
            return false;
        }

        foreach (var field in requiredFields)
        {
            if (!state.AccumulatedChanges.ContainsKey(field))
            {
                _logger.LogDebug(
                    "Missing required field {Field} for phone {Phone}",
                    field,
                    state.PhoneNumber);
                return false;
            }
        }

        return true;
    }

    public ModificationState ClearAccumulatedField(ModificationState state, string fieldName)
    {
        if (state?.AccumulatedChanges == null)
        {
            return state ?? throw new ArgumentNullException(nameof(state));
        }

        if (!state.AccumulatedChanges.ContainsKey(fieldName))
        {
            return state;
        }

        _logger.LogDebug(
            "Clearing accumulated field {FieldName} for phone {Phone}",
            fieldName,
            state.PhoneNumber);

        var accumulatedChanges = new Dictionary<string, object>(state.AccumulatedChanges);
        accumulatedChanges.Remove(fieldName);

        var extractedFields = state.ExtractedFields != null
            ? new List<string>(state.ExtractedFields)
            : new List<string>();

        extractedFields.Remove(fieldName);

        return state with
        {
            AccumulatedChanges = accumulatedChanges,
            ExtractedFields = extractedFields,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public List<string> GetAccumulatedFieldNames(ModificationState state)
    {
        if (state?.AccumulatedChanges == null)
        {
            return new List<string>();
        }

        return new List<string>(state.AccumulatedChanges.Keys);
    }

    public BookingUpdateData ConvertToBookingUpdateData(ModificationState state)
    {
        if (state?.AccumulatedChanges == null)
        {
            return new BookingUpdateData();
        }

        // Variables to build BookingUpdateData
        string? reservationDate = null;
        string? reservationTime = null;
        int? partySize = null;
        string? arrozType = null;
        int? arrozServings = null;
        int? highChairs = null;
        int? babyStrollers = null;
        bool clearRice = false;

        // Map accumulated fields to variables
        if (state.AccumulatedChanges.TryGetValue("date", out var dateValue))
        {
            var dateStr = dateValue?.ToString();
            if (!string.IsNullOrEmpty(dateStr))
            {
                // Try to parse and format as yyyy-MM-dd
                if (DateTime.TryParse(dateStr, out var date))
                {
                    reservationDate = date.ToString("yyyy-MM-dd");
                }
                else
                {
                    reservationDate = dateStr;
                }
            }
        }

        if (state.AccumulatedChanges.TryGetValue("time", out var timeValue))
        {
            var timeStr = timeValue?.ToString();
            if (!string.IsNullOrEmpty(timeStr))
            {
                // Try to parse and format as HH:mm:ss
                if (TimeSpan.TryParse(timeStr, out var time))
                {
                    reservationTime = $"{time.Hours:D2}:{time.Minutes:D2}:00";
                }
                else
                {
                    reservationTime = timeStr;
                }
            }
        }

        if (state.AccumulatedChanges.TryGetValue("party_size", out var partySizeValue))
        {
            if (int.TryParse(partySizeValue?.ToString(), out var pSize))
            {
                partySize = pSize;
            }
        }

        if (state.AccumulatedChanges.TryGetValue("rice_type", out var riceTypeValue))
        {
            arrozType = riceTypeValue?.ToString();
        }

        if (state.AccumulatedChanges.TryGetValue("rice_servings", out var riceServingsValue))
        {
            if (int.TryParse(riceServingsValue?.ToString(), out var servings))
            {
                arrozServings = servings;
            }
        }

        if (state.AccumulatedChanges.TryGetValue("clear_rice", out var clearRiceValue))
        {
            if (clearRiceValue is bool clear && clear)
            {
                clearRice = true;
            }
        }

        if (state.AccumulatedChanges.TryGetValue("tronas", out var tronasValue))
        {
            if (int.TryParse(tronasValue?.ToString(), out var tronas))
            {
                highChairs = tronas;
            }
        }

        if (state.AccumulatedChanges.TryGetValue("carritos", out var carritosValue))
        {
            if (int.TryParse(carritosValue?.ToString(), out var carritos))
            {
                babyStrollers = carritos;
            }
        }

        // Build BookingUpdateData with object initializer
        var updateData = new BookingUpdateData
        {
            ReservationDate = reservationDate,
            ReservationTime = reservationTime,
            PartySize = partySize,
            ArrozType = arrozType,
            ArrozServings = arrozServings,
            HighChairs = highChairs,
            BabyStrollers = babyStrollers,
            ClearRice = clearRice
        };

        _logger.LogDebug(
            "Converted accumulated changes to BookingUpdateData for phone {Phone}: {@UpdateData}",
            state.PhoneNumber,
            updateData);

        return updateData;
    }
}
