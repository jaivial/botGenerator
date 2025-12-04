"""
Test Scenarios for WhatsApp Restaurant Booking Bot

This file contains test scenarios organized by category.
Run with: python run_tests.py
"""

# === GREETING TESTS ===
GREETING_TESTS = [
    {
        "name": "Spanish Greeting - Hola",
        "turns": [
            {
                "input": "Hola",
                "expected_contains": ["hola"],
                "expected_not_contains": ["error"]
            }
        ]
    },
    {
        "name": "Spanish Greeting - Buenos dias",
        "turns": [
            {
                "input": "Buenos dias",
                "expected_contains": []  # Just check no error
            }
        ]
    },
    {
        "name": "English Greeting - Hello",
        "turns": [
            {
                "input": "Hello",
                "expected_contains": []
            }
        ]
    }
]

# === BOOKING FLOW TESTS ===
BOOKING_FLOW_TESTS = [
    {
        "name": "Complete Booking - All Info at Once",
        "turns": [
            {
                "input": "Quiero reservar para 4 personas manana a las 20:00",
                "expected_contains": ["reserva"]
            }
        ]
    },
    {
        "name": "Booking - Step by Step",
        "turns": [
            {
                "input": "Hola, quiero hacer una reserva",
                "expected_contains": []
            },
            {
                "input": "Para 2 personas",
                "expected_contains": []
            },
            {
                "input": "El sabado",
                "expected_contains": []
            },
            {
                "input": "A las 9 de la noche",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Booking with Name",
        "turns": [
            {
                "input": "Reserva para Juan Garcia, 3 personas, viernes a las 21:00",
                "expected_contains": []
            }
        ]
    }
]

# === DATE/TIME PARSING TESTS ===
DATE_TIME_TESTS = [
    {
        "name": "Relative Date - Tomorrow",
        "turns": [
            {
                "input": "Quiero reservar para manana",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Relative Date - This Weekend",
        "turns": [
            {
                "input": "Quiero reservar para este fin de semana",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Specific Date - DD/MM format",
        "turns": [
            {
                "input": "Reserva para el 25/12",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Time - 24h format",
        "turns": [
            {
                "input": "Quiero reservar a las 20:30",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Time - 12h format with PM",
        "turns": [
            {
                "input": "Reserva a las 8 de la noche",
                "expected_contains": []
            }
        ]
    }
]

# === MODIFICATION TESTS ===
MODIFICATION_TESTS = [
    {
        "name": "Modify Booking - Change Date",
        "turns": [
            {
                "input": "Quiero modificar mi reserva",
                "expected_contains": []
            },
            {
                "input": "Cambiar la fecha al lunes",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Modify Booking - Change Party Size",
        "turns": [
            {
                "input": "Necesito cambiar mi reserva, ahora somos 6 personas",
                "expected_contains": []
            }
        ]
    }
]

# === CANCELLATION TESTS ===
CANCELLATION_TESTS = [
    {
        "name": "Cancel Booking - Direct",
        "turns": [
            {
                "input": "Quiero cancelar mi reserva",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Cancel Booking - Confirmation Flow",
        "turns": [
            {
                "input": "Cancelar reserva",
                "expected_contains": []
            },
            {
                "input": "Si, confirmo la cancelacion",
                "expected_contains": []
            }
        ]
    }
]

# === EDGE CASE TESTS ===
EDGE_CASE_TESTS = [
    {
        "name": "Empty Message Handling",
        "turns": [
            {
                "input": "   ",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Very Long Message",
        "turns": [
            {
                "input": "Hola, quiero hacer una reserva para mi familia, somos muchas personas, " * 10,
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Special Characters",
        "turns": [
            {
                "input": "Reserva!!! @#$% ???",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Numbers Only",
        "turns": [
            {
                "input": "4",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Mixed Language",
        "turns": [
            {
                "input": "I want to book una reserva for tomorrow",
                "expected_contains": []
            }
        ]
    }
]

# === MULTI-TURN CONVERSATION TESTS ===
MULTI_TURN_TESTS = [
    {
        "name": "Full Booking Journey",
        "turns": [
            {
                "input": "Hola",
                "expected_contains": []
            },
            {
                "input": "Quiero reservar",
                "expected_contains": []
            },
            {
                "input": "4 personas",
                "expected_contains": []
            },
            {
                "input": "Manana",
                "expected_contains": []
            },
            {
                "input": "20:00",
                "expected_contains": []
            },
            {
                "input": "Juan Garcia",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Booking with Questions",
        "turns": [
            {
                "input": "Hola",
                "expected_contains": []
            },
            {
                "input": "Tienen terraza?",
                "expected_contains": []
            },
            {
                "input": "OK, quiero reservar para 2",
                "expected_contains": []
            },
            {
                "input": "Este viernes a las 21:00",
                "expected_contains": []
            }
        ]
    }
]

# === SPECIAL REQUESTS TESTS ===
SPECIAL_REQUESTS_TESTS = [
    {
        "name": "Request with Rice/Paella",
        "turns": [
            {
                "input": "Quiero reservar y pedir arroz",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Request with High Chair",
        "turns": [
            {
                "input": "Reserva para 2 adultos y 1 bebe, necesitamos trona",
                "expected_contains": []
            }
        ]
    },
    {
        "name": "Request with Dietary Requirements",
        "turns": [
            {
                "input": "Reserva para 4, uno es vegetariano",
                "expected_contains": []
            }
        ]
    }
]


# === COMBINE ALL TESTS ===
def get_all_tests():
    """Returns all test scenarios combined"""
    return (
        GREETING_TESTS +
        BOOKING_FLOW_TESTS +
        DATE_TIME_TESTS +
        MODIFICATION_TESTS +
        CANCELLATION_TESTS +
        EDGE_CASE_TESTS +
        MULTI_TURN_TESTS +
        SPECIAL_REQUESTS_TESTS
    )


def get_tests_by_category(category: str):
    """Get tests by category name"""
    categories = {
        "greeting": GREETING_TESTS,
        "booking": BOOKING_FLOW_TESTS,
        "datetime": DATE_TIME_TESTS,
        "modification": MODIFICATION_TESTS,
        "cancellation": CANCELLATION_TESTS,
        "edge": EDGE_CASE_TESTS,
        "multiturn": MULTI_TURN_TESTS,
        "special": SPECIAL_REQUESTS_TESTS
    }
    return categories.get(category.lower(), [])


def list_categories():
    """List all available test categories"""
    return [
        "greeting",
        "booking",
        "datetime",
        "modification",
        "cancellation",
        "edge",
        "multiturn",
        "special"
    ]
