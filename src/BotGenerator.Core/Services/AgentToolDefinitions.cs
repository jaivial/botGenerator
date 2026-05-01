using System.Text.Json;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Provides all tool definitions for the AI Agent in Anthropic API format.
/// </summary>
public static class AgentToolDefinitions
{
    /// <summary>
    /// Gets all available tools for the agent, including send_message.
    /// </summary>
    public static List<ToolDefinition> GetAllTools() => new()
    {
        // Core messaging
        GetSendMessageTool(),
        GetFetchHistoryTool(),
        
        // Restaurant info
        GetRestaurantInfoTool(),
        GetRiceMenuTool(),
        
        // Availability checking (simplified agent tools)
        GetCheckFutureBookingTool(),
        GetOpeningHoursWithCapacityTool(),
        GetCheckHourCapacityTool(),
        GetCheckDayCapacityTool(),
        GetCheckAvailabilityForPartyTool(),
        
        // Legacy tools (kept for compatibility)
        GetCheckAvailabilityTool(),
        GetOpeningHoursTool(),
        GetHourDataTool(),
        GetDayStatusTool(),
        
        // Booking management
        GetBookingsTool(),
        GetCreateBookingTool(),
        GetCancelBookingTool(),
        GetModifyBookingTool()
    };

    /// <summary>
    /// Tool for sending WhatsApp messages to the user.
    /// This is the PRIMARY tool for agent communication.
    /// </summary>
    public static ToolDefinition GetSendMessageTool() => new()
    {
        Name = "send_message",
        Description = "Envía un mensaje de WhatsApp al usuario. ESTA ES LA HERRAMIENTA PRINCIPAL PARA COMUNICARTE. Siempre debes usar esta herramienta para responder al usuario, nunca generes texto plano como respuesta final. El mensaje debe estar en español, ser amable, y usar emojis apropiados.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""message"": {
                    ""type"": ""string"",
                    ""description"": ""El mensaje de WhatsApp a enviar. Debe ser una respuesta completa y útil al usuario.""
                }
            },
            ""required"": [""message""]
        }").RootElement
    };

    /// <summary>
    /// Tool for fetching WhatsApp conversation history.
    /// </summary>
    public static ToolDefinition GetFetchHistoryTool() => new()
    {
        Name = "fetch_whatsapp_history",
        Description = "Obtiene el historial de conversación de WhatsApp para el usuario actual. Úsala al inicio para entender el contexto de la conversación.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""limit"": {
                    ""type"": ""integer"",
                    ""description"": ""Número máximo de mensajes a obtener (default: 30, máximo: 100)"",
                    ""default"": 30
                }
            }
        }").RootElement
    };

    /// <summary>
    /// Tool for getting restaurant information.
    /// </summary>
    public static ToolDefinition GetRestaurantInfoTool() => new()
    {
        Name = "get_restaurant_info",
        Description = "Obtiene información del restaurante: nombre, teléfono, email, dirección, web, menú.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {}
        }").RootElement
    };

    /// <summary>
    /// Tool for getting available rice types from the menu.
    /// </summary>
    public static ToolDefinition GetRiceMenuTool() => new()
    {
        Name = "get_rice_menu",
        Description = "Obtiene los tipos de arroz disponibles en el menú actual del restaurante.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {}
        }").RootElement
    };

    /// <summary>
    /// Tool for checking booking availability for a specific date/time/party size.
    /// </summary>
    public static ToolDefinition GetCheckAvailabilityTool() => new()
    {
        Name = "check_availability",
        Description = "Verifica si hay disponibilidad para una reserva en una fecha, hora y número de personas específicas.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 01/05/2026""
                },
                ""time"": {
                    ""type"": ""string"",
                    ""description"": ""Hora en formato HH:mm. Ejemplo: 14:00 o 21:00""
                },
                ""people"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de personas para la reserva""
                }
            },
            ""required"": [""date"", ""people""]
        }").RootElement
    };

    /// <summary>
    /// Tool for getting opening hours for a specific date.
    /// </summary>
    public static ToolDefinition GetOpeningHoursTool() => new()
    {
        Name = "get_opening_hours",
        Description = "Obtiene los horarios de apertura y slots disponibles para una fecha específica.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 01/05/2026""
                }
            },
            ""required"": [""date""]
        }").RootElement
    };

    /// <summary>
    /// Tool for getting detailed seat data per hour for a date.
    /// </summary>
    public static ToolDefinition GetHourDataTool() => new()
    {
        Name = "get_hour_data",
        Description = "Obtiene datos detallados de disponibilidad por hora para una fecha: plazas libres, plazas totales, estado de cada franja horaria.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 01/05/2026""
                }
            },
            ""required"": [""date""]
        }").RootElement
    };

    /// <summary>
    /// Tool for checking if a day is open and has booking availability.
    /// </summary>
    public static ToolDefinition GetDayStatusTool() => new()
    {
        Name = "get_day_status",
        Description = "Verifica si el restaurante está abierto un día específico y cuántas plazas libres hay para reservas.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 01/05/2026""
                }
            },
            ""required"": [""date""]
        }").RootElement
    };

    /// <summary>
    /// Tool for getting user's existing bookings.
    /// </summary>
    public static ToolDefinition GetBookingsTool() => new()
    {
        Name = "get_bookings",
        Description = "Obtiene las reservas activas del usuario actual. Úsala para consultar, modificar o cancelar reservas existentes.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""phone"": {
                    ""type"": ""string"",
                    ""description"": ""Número de teléfono del usuario (opcional, usa el actual si no se especifica)""
                }
            }
        }").RootElement
    };

    /// <summary>
    /// Tool for creating a new booking.
    /// </summary>
    public static ToolDefinition GetCreateBookingTool() => new()
    {
        Name = "create_booking",
        Description = "Crea una nueva reserva en el sistema. Requiere confirmacion (confirmed: true) para ejecutarse.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha de la reserva en formato YYYY-MM-DD. Ejemplo: 2026-05-15""
                },
                ""time"": {
                    ""type"": ""string"",
                    ""description"": ""Hora de la reserva en formato HH:MM. Ejemplo: 14:30""
                },
                ""people"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de personas para la reserva (default: 2)""
                },
                ""rice_type"": {
                    ""type"": ""string"",
                    ""description"": ""Tipo de arroz. Ejemplo: Paella Valenciana, Arroz Negro, Arroz a Banda""
                },
                ""rice_servings"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de porciones de arroz (opcional)""
                },
                ""name"": {
                    ""type"": ""string"",
                    ""description"": ""Nombre del cliente (opcional, default: Cliente WhatsApp)""
                },
                ""high_chairs"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de tronas necesarias (opcional)""
                },
                ""baby_strollers"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de espacios para carritos de bebé (opcional)""
                },
                ""confirmed"": {
                    ""type"": ""boolean"",
                    ""description"": ""DEBE ser TRUE para crear la reserva. Esto es una confirmacion de seguridad.""
                }
            },
            ""required"": [""date"", ""time"", ""confirmed""]
        }").RootElement
    };

    /// <summary>
    /// Tool for cancelling an existing booking.
    /// </summary>
    public static ToolDefinition GetCancelBookingTool() => new()
    {
        Name = "cancel_booking",
        Description = "Cancela una reserva existente. La reserva debe tener estado 'Confirmed' para poder cancelarse.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""booking_id"": {
                    ""type"": ""string"",
                    ""description"": ""ID de la reserva a cancelar. Ejemplo: 1234""
                },
                ""confirmed"": {
                    ""type"": ""boolean"",
                    ""description"": ""DEBE ser TRUE para confirmar la cancelacion. Esto es una confirmacion de seguridad.""
                }
            },
            ""required"": [""booking_id"", ""confirmed""]
        }").RootElement
    };

    /// <summary>
    /// Tool for modifying an existing booking.
    /// </summary>
    public static ToolDefinition GetModifyBookingTool() => new()
    {
        Name = "modify_booking",
        Description = "Modifica una reserva existente. Puede cambiar fecha, hora, numero de personas, tipo de arroz, etc.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""booking_id"": {
                    ""type"": ""string"",
                    ""description"": ""ID de la reserva a modificar. Ejemplo: 1234""
                },
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Nueva fecha en formato YYYY-MM-DD (opcional)""
                },
                ""time"": {
                    ""type"": ""string"",
                    ""description"": ""Nueva hora en formato HH:MM (opcional)""
                },
                ""people"": {
                    ""type"": ""integer"",
                    ""description"": ""Nuevo numero de personas (opcional)""
                },
                ""rice_type"": {
                    ""type"": ""string"",
                    ""description"": ""Nuevo tipo de arroz (opcional)""
                },
                ""rice_servings"": {
                    ""type"": ""integer"",
                    ""description"": ""Nuevo numero de porciones de arroz (opcional)""
                },
                ""high_chairs"": {
                    ""type"": ""integer"",
                    ""description"": ""Numero de tronas (opcional)""
                },
                ""baby_strollers"": {
                    ""type"": ""integer"",
                    ""description"": ""Numero de espacios para carritos (opcional)""
                },
                ""clear_rice"": {
                    ""type"": ""boolean"",
                    ""description"": ""TRUE si el cliente quiere sin arroz (opcional)""
                },
                ""confirmed"": {
                    ""type"": ""boolean"",
                    ""description"": ""DEBE ser TRUE para confirmar la modificacion. Esto es una confirmacion de seguridad.""
                }
            },
            ""required"": [""booking_id"", ""confirmed""]
        }").RootElement
    };

    // =========================================================================
    // NEW SIMPLIFIED AGENT TOOLS
    // =========================================================================

    /// <summary>
    /// Tool for checking if user has future bookings.
    /// </summary>
    public static ToolDefinition GetCheckFutureBookingTool() => new()
    {
        Name = "check_future_booking",
        Description = "Verifica si el usuario tiene alguna reserva futura confirmada o pendiente. Útil para saber si el cliente ya tiene una reserva upcoming.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {}
        }").RootElement
    };

    /// <summary>
    /// Tool for getting opening hours with capacity per hour.
    /// Combines openinghours table + hour_configuration table.
    /// </summary>
    public static ToolDefinition GetOpeningHoursWithCapacityTool() => new()
    {
        Name = "get_opening_hours_with_capacity",
        Description = "Obtiene los horarios disponibles para una fecha con información de capacidad por hora. 1) Consulta openinghours, si no hay usa defaults [13:30,14:00,14:30,15:00,15:30]. 2) Consulta hour_configuration para capacidad por hora.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 15/05/2026""
                },
                ""party_size"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de personas para verificar si caben (opcional)""
                }
            },
            ""required"": [""date""]
        }").RootElement
    };

    /// <summary>
    /// Tool for checking hour configuration only (independent).
    /// For when party_size is not yet known.
    /// </summary>
    public static ToolDefinition GetCheckHourCapacityTool() => new()
    {
        Name = "check_hour_capacity",
        Description = "Verifica la configuración de capacidad por hora para una fecha específica. Consulta solo hour_configuration, no openinghours. Útil cuando aún no se sabe el número de personas.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 15/05/2026""
                }
            },
            ""required"": [""date""]
        }").RootElement
    };

    /// <summary>
    /// Tool for quick day fullness check.
    /// </summary>
    public static ToolDefinition GetCheckDayCapacityTool() => new()
    {
        Name = "check_day_capacity",
        Description = "Verifica rápidamente si un día tiene disponibilidad general. Suma todas las reservas y compara con el límite diario (default 45). Devuelve si está abierto, completo o cerrado.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 15/05/2026""
                }
            },
            ""required"": [""date""]
        }").RootElement
    };

    /// <summary>
    /// Tool for checking if party size fits on a date.
    /// </summary>
    public static ToolDefinition GetCheckAvailabilityForPartyTool() => new()
    {
        Name = "check_availability_for_party",
        Description = "Verifica si un número específico de personas cabe en una fecha. Compara party_size con plazas libres del día.",
        InputSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""date"": {
                    ""type"": ""string"",
                    ""description"": ""Fecha en formato dd/MM/yyyy. Ejemplo: 15/05/2026""
                },
                ""party_size"": {
                    ""type"": ""integer"",
                    ""description"": ""Número de personas""
                }
            },
            ""required"": [""date"", ""party_size""]
        }").RootElement
    };
}
