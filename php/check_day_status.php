<?php
// Enable error reporting
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

// Include database connection
require_once 'conectaVILLACARMEN.php';

// Set headers for JSON response
header('Content-Type: application/json');

// Check if date is provided
if (!isset($_POST['date']) || empty($_POST['date'])) {
    echo json_encode([
        'success' => false,
        'message' => 'Date parameter is required'
    ]);
    exit;
}

// Get the date from the request
$date = $_POST['date'];

// Validate date format (YYYY-MM-DD)
if (!preg_match('/^\d{4}-\d{2}-\d{2}$/', $date)) {
    echo json_encode([
        'success' => false,
        'message' => 'Invalid date format. Use YYYY-MM-DD'
    ]);
    exit;
}

try {
    // Convert date to DateTime object to get day of week
    $dateObj = new DateTime($date);
    $dayOfWeek = $dateObj->format('N'); // 1 (Monday) to 7 (Sunday)
    $weekdayNames = [
        1 => 'Lunes',
        2 => 'Martes',
        3 => 'Miércoles',
        4 => 'Jueves',
        5 => 'Viernes',
        6 => 'Sábado',
        7 => 'Domingo'
    ];
    $weekday = $weekdayNames[$dayOfWeek];

    // Check if the day is a default closed day (Monday=1, Tuesday=2, Wednesday=3)
    $isDefaultClosedDay = in_array($dayOfWeek, [1, 2, 3]);

    // Check if there's an entry in the restaurant_days table for this date
    $stmt = $conn->prepare("SELECT is_open FROM restaurant_days WHERE date = ?");
    $stmt->bind_param("s", $date);
    $stmt->execute();
    $result = $stmt->get_result();

    // Default status based on day of week
    $isOpen = !$isDefaultClosedDay;

    // If there's an entry in the database, use that value
    if ($result->num_rows > 0) {
        $row = $result->fetch_assoc();
        $isOpen = (bool)$row['is_open'];
    }

    // Return the status
    echo json_encode([
        'success' => true,
        'date' => $date,
        'weekday' => $weekday,
        'is_open' => $isOpen,
        'is_default_closed_day' => $isDefaultClosedDay
    ]);

    // Close the connection
    $stmt->close();
    $conn->close();
} catch (Exception $e) {
    // Log the error
    error_log('Error in check_day_status.php: ' . $e->getMessage());

    // Return error message
    echo json_encode([
        'success' => false,
        'message' => 'Error checking day status: ' . $e->getMessage()
    ]);
}
