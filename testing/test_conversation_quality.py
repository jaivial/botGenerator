"""
Conversation Quality Tests for WhatsApp Bot

This module contains 5 comprehensive tests that:
1. Run multi-turn conversations through the bot
2. Save conversation logs
3. Analyze logs for:
   - Message duplications
   - Non-human/robotic patterns
   - Conversation flow issues
   - Strange responses

Run with: python test_conversation_quality.py
"""

import sys
import time
import uuid
from datetime import datetime
from pathlib import Path

# Add testing directory to path
sys.path.insert(0, str(Path(__file__).parent))

from conversation_tester import ConversationTester, TestConfig, run_test_suite
from log_quality_analyzer import LogQualityAnalyzer, format_analysis_report, LogAnalysisResult


class ConversationQualityTester:
    """
    Tests conversation quality by running scenarios and analyzing the generated logs.
    """

    def __init__(self, logs_dir: str = "logs"):
        self.logs_dir = Path(logs_dir)
        # Use port 5082 which is the actual bot port
        self.config = TestConfig(
            bot_webhook_url="http://localhost:5082/api/webhook/whatsapp-webhook",
            logs_dir=str(self.logs_dir),
            enable_logging=True,
            response_timeout=30
        )
        self.tester = ConversationTester(self.config)
        self.analyzer = LogQualityAnalyzer(str(self.logs_dir))
        self.results = []

    def run_test_and_analyze(
        self,
        test_name: str,
        turns: list[dict],
        phone: str = None
    ) -> tuple[bool, LogAnalysisResult]:
        """
        Run a conversation test and analyze the resulting log.

        Args:
            test_name: Name of the test
            turns: List of turn definitions
            phone: Optional phone number

        Returns:
            Tuple of (overall_passed, analysis_result)
        """
        # Generate unique numeric phone for this test
        import random
        phone = phone or f"34699{random.randint(100000, 999999)}"

        print(f"\n{'#' * 70}")
        print(f"# QUALITY TEST: {test_name}")
        print(f"{'#' * 70}")

        # Run the conversation
        conv_result = self.tester.run_conversation(
            name=test_name,
            turns=turns,
            phone=phone,
            clear_before=True
        )

        # Small delay to ensure log is written
        time.sleep(0.5)

        # Find and analyze the log
        log_path = self.analyzer.find_latest_log(test_name.replace(' ', '_'))

        if not log_path:
            print(f"ERROR: Could not find log file for test: {test_name}")
            return False, None

        analysis = self.analyzer.analyze_log(log_path)

        # Print analysis report
        print(format_analysis_report(analysis))

        # Overall result: conversation passed AND no quality issues
        overall_passed = conv_result.passed and analysis.passed

        return overall_passed, analysis


def test_1_basic_booking_no_duplications():
    """
    TEST 1: Basic Booking Flow - Verify No Duplications

    This test runs a complete booking flow and verifies:
    - Bot does not repeat the same message
    - No consecutive identical responses
    - Each response is contextually appropriate
    """
    tester = ConversationQualityTester()

    test_name = "QualityTest1_BasicBooking_NoDuplications"
    turns = [
        {
            "input": "Hola buenas tardes",
            "expected_contains": ["hola"]
        },
        {
            "input": "Quiero hacer una reserva para 4 personas",
            "expected_contains": []
        },
        {
            "input": "Para el proximo sabado",
            "expected_contains": []
        },
        {
            "input": "A las 14:00",
            "expected_contains": []
        },
        {
            "input": "No queremos arroz gracias",
            "expected_contains": []
        },
        {
            "input": "Sin tronas",
            "expected_contains": []
        },
        {
            "input": "Sin carrito",
            "expected_contains": []
        },
        {
            "input": "Si, confirmo la reserva",
            "expected_contains": []
        }
    ]

    passed, analysis = tester.run_test_and_analyze(test_name, turns)

    # Additional checks
    if analysis:
        duplication_issues = [i for i in analysis.issues if 'duplication' in i.issue_type]
        if duplication_issues:
            print(f"\nFAILED: Found {len(duplication_issues)} duplication issues!")
            return False

    return passed


def test_2_topic_switching_coherence():
    """
    TEST 2: Topic Switching - Verify Response Coherence

    This test switches topics mid-conversation and verifies:
    - Bot handles topic changes gracefully
    - Responses remain coherent and human-like
    - No confused or robotic responses
    """
    tester = ConversationQualityTester()

    test_name = "QualityTest2_TopicSwitching_Coherence"
    turns = [
        {
            "input": "Hola, quiero info sobre el restaurante",
            "expected_contains": []
        },
        {
            "input": "Cual es la especialidad de la casa?",
            "expected_contains": []
        },
        {
            "input": "Mejor hagamos una reserva para 2 personas",
            "expected_contains": []
        },
        {
            "input": "Este viernes a las 21:00",
            "expected_contains": []
        },
        {
            "input": "Espera, teneis terraza?",
            "expected_contains": []
        },
        {
            "input": "Vale perfecto, continuemos con la reserva",
            "expected_contains": []
        },
        {
            "input": "No queremos arroz",
            "expected_contains": []
        },
        {
            "input": "Sin tronas ni carrito",
            "expected_contains": []
        },
        {
            "input": "Confirmo",
            "expected_contains": []
        }
    ]

    passed, analysis = tester.run_test_and_analyze(test_name, turns)

    # Check for non-human patterns
    if analysis:
        non_human_issues = [i for i in analysis.issues if i.issue_type == 'non_human_pattern']
        if non_human_issues:
            print(f"\nFAILED: Found {len(non_human_issues)} non-human response patterns!")
            return False

    return passed


def test_3_rapid_fire_messages():
    """
    TEST 3: Rapid-Fire Messages - Verify No Duplicate Responses

    This test sends multiple messages quickly to verify:
    - Bot handles rapid input without duplicating responses
    - Each response is unique and relevant
    - No message processing errors
    """
    tester = ConversationQualityTester()

    test_name = "QualityTest3_RapidFire_NoDuplicateResponses"
    turns = [
        {
            "input": "Hola",
            "expected_contains": []
        },
        {
            "input": "Reserva",
            "expected_contains": []
        },
        {
            "input": "6 personas",
            "expected_contains": []
        },
        {
            "input": "Manana",
            "expected_contains": []
        },
        {
            "input": "20:30",
            "expected_contains": []
        },
        {
            "input": "Sin arroz",
            "expected_contains": []
        },
        {
            "input": "Sin extras",
            "expected_contains": []
        },
        {
            "input": "Confirmo",
            "expected_contains": []
        }
    ]

    passed, analysis = tester.run_test_and_analyze(test_name, turns)

    # Specific check for consecutive duplications
    if analysis:
        consecutive_dups = [i for i in analysis.issues if i.issue_type == 'consecutive_duplication']
        if consecutive_dups:
            print(f"\nFAILED: Found {len(consecutive_dups)} consecutive duplicate responses!")
            return False

    return passed


def test_4_long_conversation_no_repetition():
    """
    TEST 4: Long Multi-Turn Conversation - Verify No Repetitive Patterns

    This test runs a longer conversation with various interactions:
    - Multiple questions and answers
    - Changes and corrections
    - Verifies bot doesn't fall into repetitive patterns
    """
    tester = ConversationQualityTester()

    test_name = "QualityTest4_LongConversation_NoRepetition"
    turns = [
        {
            "input": "Buenas tardes",
            "expected_contains": []
        },
        {
            "input": "Tengo algunas preguntas antes de reservar",
            "expected_contains": []
        },
        {
            "input": "Haceis arroces?",
            "expected_contains": []
        },
        {
            "input": "Y teneis menu infantil?",
            "expected_contains": []
        },
        {
            "input": "Perfecto, quiero reservar para 5 personas",
            "expected_contains": []
        },
        {
            "input": "El domingo que viene",
            "expected_contains": []
        },
        {
            "input": "A las 14:00",
            "expected_contains": []
        },
        {
            "input": "Espera, mejor a las 14:30",
            "expected_contains": []
        },
        {
            "input": "Y seremos 6 personas, no 5",
            "expected_contains": []
        },
        {
            "input": "Queremos arroz de senoret",
            "expected_contains": []
        },
        {
            "input": "6 raciones",
            "expected_contains": []
        },
        {
            "input": "Necesitamos una trona",
            "expected_contains": []
        },
        {
            "input": "Y un carrito",
            "expected_contains": []
        },
        {
            "input": "Si todo correcto, confirmo",
            "expected_contains": []
        }
    ]

    passed, analysis = tester.run_test_and_analyze(test_name, turns)

    # Check for repetitive text patterns
    if analysis:
        repetitive_issues = [i for i in analysis.issues if i.issue_type == 'repetitive_text']
        if repetitive_issues:
            print(f"\nWARNING: Found {len(repetitive_issues)} repetitive text patterns")
            # This might be acceptable in some cases, so just warn

    return passed


def test_5_error_recovery_graceful():
    """
    TEST 5: Error Recovery - Verify Graceful Handling

    This test includes ambiguous and potentially confusing inputs:
    - Invalid data
    - Contradictions
    - Strange requests
    Verifies bot handles these gracefully without robotic/error responses
    """
    tester = ConversationQualityTester()

    test_name = "QualityTest5_ErrorRecovery_GracefulHandling"
    turns = [
        {
            "input": "Hola",
            "expected_contains": []
        },
        {
            "input": "Quiero reservar para ayer",  # Invalid date
            "expected_contains": []
        },
        {
            "input": "Vale, para manana entonces",
            "expected_contains": []
        },
        {
            "input": "2 personas",
            "expected_contains": []
        },
        {
            "input": "A las 25:00",  # Invalid time
            "expected_contains": []
        },
        {
            "input": "Perdon, a las 14:00",  # Now a valid time
            "expected_contains": []
        },
        {
            "input": "Quiero arroz de unicornio",  # Invalid rice type
            "expected_contains": []
        },
        {
            "input": "Vale, sin arroz",
            "expected_contains": []
        },
        {
            "input": "Sin tronas ni carrito",
            "expected_contains": []
        },
        {
            "input": "Confirmo",
            "expected_contains": []
        }
    ]

    passed, analysis = tester.run_test_and_analyze(test_name, turns)

    # Check that no error messages leaked through
    if analysis:
        error_leaks = [i for i in analysis.issues
                       if i.issue_type == 'non_human_pattern'
                       and 'error' in i.description.lower()]
        if error_leaks:
            print(f"\nFAILED: Found {len(error_leaks)} error message leaks!")
            return False

    return passed


def run_all_quality_tests() -> dict:
    """
    Run all 5 quality tests and return summary.

    Returns:
        Dict with test results summary
    """
    print("\n" + "=" * 70)
    print(" CONVERSATION QUALITY TEST SUITE")
    print(" Running 5 tests with log analysis for duplications and non-human patterns")
    print("=" * 70)

    tests = [
        ("Test 1: Basic Booking - No Duplications", test_1_basic_booking_no_duplications),
        ("Test 2: Topic Switching - Coherence", test_2_topic_switching_coherence),
        ("Test 3: Rapid-Fire - No Duplicate Responses", test_3_rapid_fire_messages),
        ("Test 4: Long Conversation - No Repetition", test_4_long_conversation_no_repetition),
        ("Test 5: Error Recovery - Graceful Handling", test_5_error_recovery_graceful),
    ]

    results = []

    for test_name, test_func in tests:
        try:
            passed = test_func()
            results.append((test_name, passed, None))
        except Exception as e:
            print(f"\nERROR in {test_name}: {e}")
            results.append((test_name, False, str(e)))

    # Print summary
    print("\n" + "=" * 70)
    print(" QUALITY TEST SUMMARY")
    print("=" * 70)

    passed_count = 0
    for test_name, passed, error in results:
        status = "PASSED" if passed else "FAILED"
        if error:
            status += f" (Error: {error[:50]})"
        print(f"  [{status:8}] {test_name}")
        if passed:
            passed_count += 1

    print("-" * 70)
    print(f" TOTAL: {passed_count}/{len(results)} tests passed")
    print("=" * 70)

    return {
        "total": len(results),
        "passed": passed_count,
        "failed": len(results) - passed_count,
        "results": results
    }


if __name__ == "__main__":
    print("=" * 70)
    print(" WhatsApp Bot Conversation Quality Tests")
    print("=" * 70)
    print("\nPrerequisites:")
    print("  1. Mock UAZAPI server running: python mock_uazapi_server.py")
    print("  2. Bot running with: WHATSAPP_API_URL=http://localhost:8080 dotnet run")
    print()

    try:
        summary = run_all_quality_tests()

        # Analyze all recent logs in the logs directory
        print("\n" + "=" * 70)
        print(" POST-TEST LOG ANALYSIS")
        print("=" * 70)

        analyzer = LogQualityAnalyzer()
        quality_test_logs = list(Path("logs").glob("*QualityTest*.log"))

        if quality_test_logs:
            # Sort by modification time
            quality_test_logs.sort(key=lambda p: p.stat().st_mtime, reverse=True)

            total_issues = 0
            for log_path in quality_test_logs[:5]:  # Last 5 quality test logs
                result = analyzer.analyze_log(log_path)
                if result.issues:
                    total_issues += len(result.issues)
                    print(f"\n{log_path.name}: {len(result.issues)} issues found")
                    for issue in result.issues:
                        print(f"  [{issue.severity}] {issue.description}")

            if total_issues == 0:
                print("\nAll quality test logs passed analysis - no issues found!")

        print("\n" + "=" * 70)

        sys.exit(0 if summary["failed"] == 0 else 1)

    except ConnectionError as e:
        print(f"\nConnection Error: {e}")
        print("\nMake sure both servers are running!")
        sys.exit(1)
    except Exception as e:
        print(f"\nUnexpected Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
