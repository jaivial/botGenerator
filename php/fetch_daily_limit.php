<?php
// Enable error reporting
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

// Include database connection
require_once 'conectaVILLACARMEN.php';

// Set content type to JSON
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
    // Fetch daily limit for the specified date
    $stmt = $conn->prepare("SELECT dailyLimit FROM reservation_manager WHERE reservationDate = ?");
    $stmt->bind_param("s", $date);
    $stmt->execute();
    $result = $stmt->get_result();

    // Default daily limit
    $dailyLimit = 45;

    // If there's a specific limit for this date, use it
    if ($result->num_rows > 0) {
        $row = $result->fetch_assoc();
        $dailyLimit = $row['dailyLimit'];
    }

    // Count existing bookings for this date
    $stmt = $conn->prepare("SELECT SUM(party_size) as total_people FROM bookings WHERE reservation_date = ?");
    $stmt->bind_param("s", $date);
    $stmt->execute();
    $result = $stmt->get_result();
    $row = $result->fetch_assoc();
    $totalPeople = $row['total_people'] ? $row['total_people'] : 0;

    // Calculate free booking seats
    $freeBookingSeats = $dailyLimit - $totalPeople;

    // Return JSON data
    echo json_encode([
        'success' => true,
        'date' => $date,
        'dailyLimit' => $dailyLimit,
        'totalPeople' => $totalPeople,
        'freeBookingSeats' => $freeBookingSeats
    ]);

    // Close the connection
    $stmt->close();
    $conn->close();
} catch (Exception $e) {
    // Log the error
    error_log('Error in fetch_daily_limit.php: ' . $e->getMessage());

    // Return error as JSON
    echo json_encode([
        'success' => false,
        'message' => 'Error al cargar el límite diario: ' . $e->getMessage()
    ]);
}
