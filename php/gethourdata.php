<?php
// Allow from any origin
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');
header('Content-Type: application/json; charset=UTF-8');

// Handle preflight requests
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit();
}

require_once '../conectaVILLACARMEN.php';

/**
 * Helper function to get opening hours from the database
 * This replicates the logic from getopeninghours.php
 */
function getOpeningHours($conn, $date) {
    $stmt = mysqli_prepare($conn, "SELECT hoursarray FROM openinghours WHERE dateselected = ?");
    if (!$stmt) {
        throw new Exception('Failed to prepare opening hours statement: ' . mysqli_error($conn));
    }

    mysqli_stmt_bind_param($stmt, "s", $date);
    if (!mysqli_stmt_execute($stmt)) {
        throw new Exception('Failed to execute opening hours statement: ' . mysqli_stmt_error($stmt));
    }

    $result = mysqli_stmt_get_result($stmt);
    $row = mysqli_fetch_assoc($result);
    mysqli_stmt_close($stmt);

    if ($row && !empty($row['hoursarray'])) {
        $hours = json_decode($row['hoursarray'], true);
        if (json_last_error() !== JSON_ERROR_NONE) {
            throw new Exception('Invalid hours data in database');
        }
        return $hours;
    }

    // Return default hours if no configuration found
    return ["13:30", "14:00", "14:30", "15:00"];
}

try {
    if (!isset($_GET['date'])) {
        throw new Exception('Date parameter is required');
    }

    $date = $_GET['date'];

    // Validate date format
    if (!preg_match('/^\d{4}-\d{2}-\d{2}$/', $date)) {
        throw new Exception('Invalid date format. Use YYYY-MM-DD');
    }

    // Obtener el límite diario para la fecha (o valor por defecto 45)
    $dailyLimit = 45; // Valor por defecto
    $stmt = mysqli_prepare($conn, "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = ?");
    if (!$stmt) {
        throw new Exception('Failed to prepare dailyLimit statement: ' . mysqli_error($conn));
    }

    mysqli_stmt_bind_param($stmt, "s", $date);
    if (!mysqli_stmt_execute($stmt)) {
        throw new Exception('Failed to execute dailyLimit statement: ' . mysqli_stmt_error($stmt));
    }

    $result = mysqli_stmt_get_result($stmt);
    $row = mysqli_fetch_assoc($result);
    if ($row && $row['dailyLimit'] !== null) {
        $dailyLimit = (int)$row['dailyLimit'];
    }
    mysqli_stmt_close($stmt);

    // Obtener todas las reservas para la fecha
    $bookingsByHour = [];
    $stmt = mysqli_prepare($conn, "SELECT reservation_time, SUM(party_size) as total_people FROM bookings WHERE reservation_date = ? GROUP BY reservation_time");
    if (!$stmt) {
        throw new Exception('Failed to prepare bookings statement: ' . mysqli_error($conn));
    }

    mysqli_stmt_bind_param($stmt, "s", $date);
    if (!mysqli_stmt_execute($stmt)) {
        throw new Exception('Failed to execute bookings statement: ' . mysqli_stmt_error($stmt));
    }

    $result = mysqli_stmt_get_result($stmt);
    while ($row = mysqli_fetch_assoc($result)) {
        // Trim the time to HH:MM format (removing seconds if present)
        $time = substr($row['reservation_time'], 0, 5);
        $bookingsByHour[$time] = (int)$row['total_people'];
    }
    mysqli_stmt_close($stmt);

    // Calcular total de personas reservadas
    $totalPeople = array_sum($bookingsByHour);

    // Obtener la configuración de horas para la fecha especificada
    $stmt = mysqli_prepare($conn, "SELECT hourData FROM hour_configuration WHERE date = ?");
    if (!$stmt) {
        throw new Exception('Failed to prepare statement: ' . mysqli_error($conn));
    }

    mysqli_stmt_bind_param($stmt, "s", $date);
    if (!mysqli_stmt_execute($stmt)) {
        throw new Exception('Failed to execute statement: ' . mysqli_stmt_error($stmt));
    }

    $result = mysqli_stmt_get_result($stmt);
    $configRow = mysqli_fetch_assoc($result);
    mysqli_stmt_close($stmt);

    // Si no hay datos para esta fecha, generar configuración por defecto
    if (!$configRow) {
        // STEP 1: Get opening hours for this date (only needed for default generation)
        $activeHours = getOpeningHours($conn, $date);

        // Calcular porcentaje equitativo
        $equalPercentage = 100 / count($activeHours);

        // Construir datos de horas por defecto
        $hourData = [];
        foreach ($activeHours as $hour) {
            // Verificar si hay reservas para esta hora
            $bookings = isset($bookingsByHour[$hour]) ? $bookingsByHour[$hour] : 0;

            // Calcular capacidad total basada en el porcentaje y límite diario
            $totalCapacity = ceil(($equalPercentage / 100) * $dailyLimit);

            // Calcular capacidad disponible (total - reservas)
            $availableCapacity = $totalCapacity - $bookings;

            // Calcular porcentaje de completitud
            $completion = ($totalCapacity > 0) ? ($bookings / $totalCapacity) * 100 : 0;

            // Determinar estado basado en el porcentaje de completitud
            $status = 'available';
            if ($completion > 90) {
                $status = 'full';
            } elseif ($completion > 70) {
                $status = 'limited';
            }

            $hourData[$hour] = [
                'status' => $status,
                'capacity' => $availableCapacity, // Capacidad disponible en lugar de capacidad total
                'totalCapacity' => $totalCapacity, // Guardar también la capacidad total
                'bookings' => $bookings,
                'percentage' => $equalPercentage,
                'completion' => $completion,
                'isClosed' => false
            ];
        }

        // Sort activeHours in ascending order
        sort($activeHours);

        // Responder con los datos generados
        echo json_encode([
            'success' => true,
            'hourData' => $hourData,
            'activeHours' => $activeHours,
            'isDefaultData' => true,
            'dailyLimit' => $dailyLimit,
            'totalPeople' => $totalPeople,
            'date' => $date
        ]);
    } else {
        // Obtener datos existentes de hour_configuration
        // hour_configuration is authoritative - use ALL hours from it, don't filter
        $hourData = json_decode($configRow['hourData'], true);

        // Update booking data and recalculate capacities for all hours in hour_configuration
        foreach ($hourData as $hour => &$data) {
            // Actualizar número real de reservas
            $data['bookings'] = isset($bookingsByHour[$hour]) ? $bookingsByHour[$hour] : 0;

            // Recalcular capacidad total basada en el porcentaje y límite diario
            $totalCapacity = ceil(($data['percentage'] / 100) * $dailyLimit);

            // Guardar la capacidad total
            $data['totalCapacity'] = $totalCapacity;

            // Calcular capacidad disponible (total - reservas)
            $data['capacity'] = $totalCapacity - $data['bookings'];

            // Recalcular porcentaje de completitud
            $data['completion'] = ($totalCapacity > 0) ? ($data['bookings'] / $totalCapacity) * 100 : 0;

            // Actualizar estado si no está cerrado
            if (!$data['isClosed'] && $data['status'] !== 'closed') {
                if ($data['completion'] > 90) {
                    $data['status'] = 'full';
                } elseif ($data['completion'] > 70) {
                    $data['status'] = 'limited';
                } else {
                    $data['status'] = 'available';
                }
            }
        }

        // Extract active hours from the hour_configuration data
        $activeHours = array_keys($hourData);

        // Sort activeHours in ascending order
        sort($activeHours);

        echo json_encode([
            'success' => true,
            'hourData' => $hourData,
            'activeHours' => $activeHours,
            'isDefaultData' => false,
            'dailyLimit' => $dailyLimit,
            'totalPeople' => $totalPeople,
            'date' => $date
        ]);
    }
} catch (Exception $e) {
    error_log("Error in gethourdata.php: " . $e->getMessage());
    http_response_code(500);
    echo json_encode([
        'success' => false,
        'message' => 'Error al obtener la configuración de horas',
        'debug' => $e->getMessage()
    ]);
}

if (isset($conn)) {
    mysqli_close($conn);
}
