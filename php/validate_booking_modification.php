<?php
// Enable error reporting
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

// Allow CORS for direct calls
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');
header('Content-Type: application/json; charset=UTF-8');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit();
}

// Include database connection
require_once '../conectaVILLACARMEN.php';

/**
 * Validates a proposed modification to an existing booking.
 *
 * Input (POST):
 *   bookingId   (int,    required)  - id of the booking being edited
 *   new_date    (string, optional)  - yyyy-MM-dd
 *   new_time    (string, optional)  - HH:mm
 *   new_people  (int,    optional)  - new party size
 *
 * The booking being edited is always excluded from current totals
 * (its seats free up before the new values are evaluated).
 *
 * Combinations handled:
 *   - time change (same date)     -> check hour capacity for that date
 *   - party increase (same date)  -> check daily capacity for that date
 *   - date change                 -> check if new date is closed;
 *                                    if open, check daily capacity;
 *                                    then check hour capacity for the
 *                                    effective (new?) time
 */

function jsonResponse(array $payload, int $code = 200): void {
    http_response_code($code);
    echo json_encode($payload, JSON_UNESCAPED_UNICODE);
    if (isset($GLOBALS['conn']) && $GLOBALS['conn'] instanceof mysqli) {
        $GLOBALS['conn']->close();
    }
    exit;
}

function isValidDate(string $date): bool {
    return (bool)preg_match('/^\d{4}-\d{2}-\d{2}$/', $date);
}

function isValidTime(string $time): bool {
    return (bool)preg_match('/^([01]\d|2[0-3]):[0-5]\d$/', $time);
}

function isDayClosed(mysqli $conn, string $date, DateTime $dateObj): array {
    $phpDayNum = (int)$dateObj->format('N'); // 1=Mon..7=Sun
    $isDefaultClosed = in_array($phpDayNum, [1, 2, 3], true);

    $stmt = $conn->prepare('SELECT is_open FROM restaurant_days WHERE date = ?');
    $stmt->bind_param('s', $date);
    $stmt->execute();
    $result = $stmt->get_result();

    if ($result->num_rows > 0) {
        $row = $result->fetch_assoc();
        $stmt->close();
        $isOpen = (bool)$row['is_open'];
        $explicit = true;
    } else {
        $stmt->close();
        $isOpen = !$isDefaultClosed;
        $explicit = false;
    }

    return [
        'is_open' => $isOpen,
        'explicit' => $explicit,
        'default_closed' => $isDefaultClosed,
    ];
}

function getDailyLimit(mysqli $conn, string $date): int {
    $stmt = $conn->prepare('SELECT dailyLimit FROM reservation_manager WHERE reservationDate = ?');
    $stmt->bind_param('s', $date);
    $stmt->execute();
    $result = $stmt->get_result();

    $limit = 45;
    if ($result->num_rows > 0) {
        $row = $result->fetch_assoc();
        if ($row['dailyLimit'] !== null) {
            $limit = (int)$row['dailyLimit'];
        }
    }
    $stmt->close();
    return $limit;
}

function getHourConfiguration(mysqli $conn, string $date): array {
    $stmt = $conn->prepare('SELECT hourData FROM hour_configuration WHERE date = ?');
    $stmt->bind_param('s', $date);
    $stmt->execute();
    $result = $stmt->get_result();
    $row = $result->fetch_assoc();
    $stmt->close();

    if (!$row || empty($row['hourData'])) {
        return [];
    }

    $decoded = json_decode($row['hourData'], true);
    return is_array($decoded) ? $decoded : [];
}

function getHourBookedTotal(mysqli $conn, string $date, string $hour, int $excludeBookingId): int {
    $stmt = $conn->prepare(
        'SELECT COALESCE(SUM(party_size), 0) AS total
         FROM bookings
         WHERE reservation_date = ?
           AND status IN (\'pending\', \'confirmed\')
           AND id <> ?
           AND TIME_FORMAT(reservation_time, \'%H:%i\') = ?'
    );
    $stmt->bind_param('sis', $date, $excludeBookingId, $hour);
    $stmt->execute();
    $result = $stmt->get_result();
    $row = $result->fetch_assoc();
    $stmt->close();
    return (int)($row['total'] ?? 0);
}

function getDailyBookedTotal(mysqli $conn, string $date, int $excludeBookingId): int {
    $stmt = $conn->prepare(
        'SELECT COALESCE(SUM(party_size), 0) AS total
         FROM bookings
         WHERE reservation_date = ?
           AND status IN (\'pending\', \'confirmed\')
           AND id <> ?'
    );
    $stmt->bind_param('si', $date, $excludeBookingId);
    $stmt->execute();
    $result = $stmt->get_result();
    $row = $result->fetch_assoc();
    $stmt->close();
    return (int)($row['total'] ?? 0);
}

function spanishDayName(DateTime $date): string {
    $names = [
        1 => 'lunes', 2 => 'martes', 3 => 'miércoles', 4 => 'jueves',
        5 => 'viernes', 6 => 'sábado', 7 => 'domingo',
    ];
    return $names[(int)$date->format('N')];
}

try {
    // --- Read & validate input ---
    if (!isset($_POST['bookingId']) || $_POST['bookingId'] === '') {
        jsonResponse([
            'valid' => false,
            'errors' => ['Falta el parámetro bookingId.'],
        ], 400);
    }

    $bookingId = (int)$_POST['bookingId'];
    if ($bookingId <= 0) {
        jsonResponse([
            'valid' => false,
            'errors' => ['bookingId inválido.'],
        ], 400);
    }

    $hasNewDate = isset($_POST['new_date']) && $_POST['new_date'] !== '';
    $hasNewTime = isset($_POST['new_time']) && $_POST['new_time'] !== '';
    $hasNewPeople = isset($_POST['new_people']) && $_POST['new_people'] !== '';

    $newDate = $hasNewDate ? (string)$_POST['new_date'] : null;
    $newTime = $hasNewTime ? (string)$_POST['new_time'] : null;
    $newPeople = $hasNewPeople ? (int)$_POST['new_people'] : null;

    if ($newDate !== null && !isValidDate($newDate)) {
        jsonResponse([
            'valid' => false,
            'errors' => ['Formato de new_date inválido. Use yyyy-MM-dd.'],
        ], 400);
    }
    if ($newTime !== null && !isValidTime($newTime)) {
        jsonResponse([
            'valid' => false,
            'errors' => ['Formato de new_time inválido. Use HH:mm.'],
        ], 400);
    }
    if ($newPeople !== null && $newPeople < 1) {
        jsonResponse([
            'valid' => false,
            'errors' => ['new_people debe ser >= 1.'],
        ], 400);
    }

    // --- Load current booking ---
    $stmt = $conn->prepare(
        'SELECT id, reservation_date, reservation_time, party_size, status
         FROM bookings
         WHERE id = ?'
    );
    $stmt->bind_param('i', $bookingId);
    $stmt->execute();
    $result = $stmt->get_result();

    if ($result->num_rows === 0) {
        $stmt->close();
        jsonResponse([
            'valid' => false,
            'errors' => ["Reserva {$bookingId} no encontrada."],
        ], 404);
    }

    $row = $result->fetch_assoc();
    $stmt->close();

    $currentStatus = (string)$row['status'];
    $currentDate = (string)$row['reservation_date']; // yyyy-MM-dd
    $currentTime = substr((string)$row['reservation_time'], 0, 5); // HH:mm
    $currentPeople = (int)$row['party_size'];

    if ($currentStatus === 'cancelled') {
        jsonResponse([
            'valid' => false,
            'errors' => ['La reserva está cancelada y no se puede modificar.'],
        ], 400);
    }

    // Effective values: use the new value if provided, otherwise current.
    $effectiveDate = $newDate ?? $currentDate;
    $effectiveTime = $newTime ?? $currentTime;
    $effectivePeople = $newPeople ?? $currentPeople;

    $effectiveDateObj = new DateTime($effectiveDate);
    $today = new DateTime('today');
    $tomorrow = (new DateTime('today'))->modify('+1 day');
    $isSameDate = $effectiveDate === $currentDate;
    $isSameTime = $effectiveTime === $currentTime;
    $isSamePeople = $effectivePeople === $currentPeople;

    $errors = [];
    $diagnostics = [
        'bookingId' => $bookingId,
        'current' => [
            'date' => $currentDate,
            'time' => $currentTime,
            'people' => $currentPeople,
        ],
        'proposed' => [
            'date' => $effectiveDate,
            'time' => $effectiveTime,
            'people' => $effectivePeople,
        ],
        'isSameDate' => $isSameDate,
        'isSameTime' => $isSameTime,
        'isSamePeople' => $isSamePeople,
    ];

    // --- Check 1: cannot move to today or tomorrow (matches the C# guard) ---
    if ($effectiveDateObj->format('Y-m-d') === $today->format('Y-m-d')) {
        $errors[] = 'No se puede mover la reserva a hoy.';
    } elseif ($effectiveDateObj->format('Y-m-d') === $tomorrow->format('Y-m-d')) {
        $errors[] = 'No se puede mover la reserva a mañana.';
    }

    // --- Check 2: target date must be open ---
    $dayStatus = isDayClosed($conn, $effectiveDate, $effectiveDateObj);
    $diagnostics['dayStatus'] = $dayStatus;
    if (!$dayStatus['is_open']) {
        $errors[] = sprintf(
            'El restaurante está cerrado el %s (%s).',
            spanishDayName($effectiveDateObj),
            $effectiveDate
        );
    }

    // --- Check 3: daily capacity for the target date (excluding self) ---
    $dailyLimit = getDailyLimit($conn, $effectiveDate);
    $alreadyBookedOnTargetDate = getDailyBookedTotal($conn, $effectiveDate, $bookingId);
    $freeSeatsOnTargetDate = $dailyLimit - $alreadyBookedOnTargetDate;

    $diagnostics['dailyCapacity'] = [
        'dailyLimit' => $dailyLimit,
        'alreadyBooked' => $alreadyBookedOnTargetDate,
        'freeSeats' => $freeSeatsOnTargetDate,
    ];

    // We only check daily capacity if the change is meaningful:
    //   - the date is different, OR
    //   - the party size is different (and is an increase)
    // Decreasing party size can never violate daily capacity.
    $partySizeIncreasing = $effectivePeople > $currentPeople;
    $dateIsChanging = !$isSameDate;

    if ($dateIsChanging || $partySizeIncreasing) {
        if ($effectivePeople > $freeSeatsOnTargetDate) {
            $errors[] = sprintf(
                'No hay capacidad suficiente para %d personas el %s. Plazas libres: %d.',
                $effectivePeople,
                $effectiveDate,
                $freeSeatsOnTargetDate
            );
        }
    }

    // --- Check 4: hour capacity for the target hour on the target date ---
    // Skip when the hour and date are unchanged AND the party size is
    // not increasing - in that case the hour is obviously already fine.
    $hourOrDateChanging = !$isSameTime || $dateIsChanging;
    if ($hourOrDateChanging || $partySizeIncreasing) {
        $hourConfig = getHourConfiguration($conn, $effectiveDate);
        if (!empty($hourConfig) && isset($hourConfig[$effectiveTime])) {
            $hourEntry = $hourConfig[$effectiveTime];
            if (is_array($hourEntry) && !empty($hourEntry['isClosed'])) {
                $errors[] = sprintf(
                    'La franja horaria %s está cerrada el %s.',
                    $effectiveTime,
                    $effectiveDate
                );
            } else {
                $hourTotalCapacity = isset($hourEntry['totalCapacity'])
                    ? (int)$hourEntry['totalCapacity']
                    : (int)ceil(((float)($hourEntry['percentage'] ?? 0) / 100) * $dailyLimit);

                $hourBooked = getHourBookedTotal($conn, $effectiveDate, $effectiveTime, $bookingId);
                $hourFree = $hourTotalCapacity - $hourBooked;

                $diagnostics['hourCapacity'] = [
                    'hour' => $effectiveTime,
                    'totalCapacity' => $hourTotalCapacity,
                    'booked' => $hourBooked,
                    'free' => $hourFree,
                ];

                if ($effectivePeople > $hourFree) {
                    $errors[] = sprintf(
                        'No hay capacidad para %d personas a las %s del %s. Plazas libres a esa hora: %d.',
                        $effectivePeople,
                        $effectiveTime,
                        $effectiveDate,
                        $hourFree
                    );
                }
            }
        } else {
            // No explicit hour_configuration for this date/hour.
            // Fall back to the daily total. We have already validated
            // daily capacity above, so we just record the absence.
            $diagnostics['hourCapacity'] = [
                'hour' => $effectiveTime,
                'totalCapacity' => null,
                'booked' => null,
                'free' => null,
                'note' => 'No hour_configuration entry; falling back to daily capacity.',
            ];
        }
    }

    // --- Check 5: modification count (max 3) ---
    $modCountStmt = $conn->prepare('SELECT COUNT(*) AS c FROM modification_history WHERE booking_id = ?');
    $modCountStmt->bind_param('i', $bookingId);
    $modCountStmt->execute();
    $modCountResult = $modCountStmt->get_result();
    $modCountRow = $modCountResult->fetch_assoc();
    $modCountStmt->close();
    $modCount = (int)($modCountRow['c'] ?? 0);
    $diagnostics['modificationCount'] = $modCount;
    $diagnostics['modificationsRemaining'] = max(0, 3 - $modCount);

    if ($modCount >= 3) {
        $errors[] = 'Has alcanzado el límite máximo de 3 modificaciones para esta reserva.';
    }

    $valid = count($errors) === 0;

    jsonResponse([
        'valid' => $valid,
        'errors' => $errors,
        'diagnostics' => $diagnostics,
    ]);
} catch (Throwable $e) {
    error_log('Error in validate_booking_modification.php: ' . $e->getMessage());
    jsonResponse([
        'valid' => false,
        'errors' => ['Error al validar la modificación: ' . $e->getMessage()],
    ], 500);
}
