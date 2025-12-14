#!/usr/bin/env python3
"""
Runner Script for Conversation Quality Tests

Usage:
    python run_quality_tests.py           # Run all 5 quality tests
    python run_quality_tests.py --test 1  # Run specific test (1-5)
    python run_quality_tests.py --analyze # Only analyze existing logs
"""

import sys
import argparse
from pathlib import Path

# Add testing directory to path
sys.path.insert(0, str(Path(__file__).parent))

from test_conversation_quality import (
    test_1_basic_booking_no_duplications,
    test_2_topic_switching_coherence,
    test_3_rapid_fire_messages,
    test_4_long_conversation_no_repetition,
    test_5_error_recovery_graceful,
    run_all_quality_tests
)
from log_quality_analyzer import LogQualityAnalyzer, format_analysis_report


def analyze_logs_only(pattern: str = ""):
    """Analyze existing logs without running tests"""
    print("=" * 70)
    print(" LOG ANALYSIS MODE")
    print("=" * 70)

    analyzer = LogQualityAnalyzer()
    logs_dir = Path("logs")

    if not logs_dir.exists():
        print("No logs directory found!")
        return

    if pattern:
        log_files = list(logs_dir.glob(f"*{pattern}*.log"))
    else:
        log_files = list(logs_dir.glob("*.log"))

    if not log_files:
        print(f"No log files found" + (f" matching '{pattern}'" if pattern else ""))
        return

    # Sort by modification time (newest first)
    log_files.sort(key=lambda p: p.stat().st_mtime, reverse=True)

    # Analyze the most recent logs
    print(f"\nAnalyzing {min(10, len(log_files))} most recent logs...\n")

    total_issues = 0
    critical_issues = 0

    for log_file in log_files[:10]:
        result = analyzer.analyze_log(log_file)

        status = "PASS" if result.passed else "FAIL"
        issue_count = len(result.issues)
        critical_count = sum(1 for i in result.issues if i.severity == 'critical')

        total_issues += issue_count
        critical_issues += critical_count

        # Summary line
        if result.passed:
            print(f"[{status}] {log_file.name}")
        else:
            print(f"[{status}] {log_file.name} - {issue_count} issues ({critical_count} critical)")
            for issue in result.issues:
                if issue.severity in ('error', 'critical'):
                    print(f"       {issue.severity.upper()}: {issue.description}")

    print("\n" + "-" * 70)
    print(f"Total: {len(log_files[:10])} logs analyzed")
    print(f"Issues: {total_issues} total, {critical_issues} critical")
    print("=" * 70)


def main():
    parser = argparse.ArgumentParser(
        description="Run WhatsApp bot conversation quality tests",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
    python run_quality_tests.py              # Run all 5 tests
    python run_quality_tests.py --test 1     # Run only test 1
    python run_quality_tests.py --test 3     # Run only test 3
    python run_quality_tests.py --analyze    # Only analyze existing logs
    python run_quality_tests.py --analyze QualityTest  # Analyze logs matching pattern

Test Descriptions:
    1. Basic Booking - No Duplications
    2. Topic Switching - Coherence
    3. Rapid-Fire - No Duplicate Responses
    4. Long Conversation - No Repetition
    5. Error Recovery - Graceful Handling
        """
    )

    parser.add_argument(
        "--test", "-t",
        type=int,
        choices=[1, 2, 3, 4, 5],
        help="Run specific test (1-5)"
    )

    parser.add_argument(
        "--analyze", "-a",
        nargs="?",
        const="",
        metavar="PATTERN",
        help="Only analyze existing logs (optionally matching pattern)"
    )

    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        help="Show detailed analysis for each log"
    )

    args = parser.parse_args()

    # Analysis-only mode
    if args.analyze is not None:
        analyze_logs_only(args.analyze)
        return 0

    # Run specific test
    if args.test:
        tests = {
            1: ("Test 1: Basic Booking - No Duplications", test_1_basic_booking_no_duplications),
            2: ("Test 2: Topic Switching - Coherence", test_2_topic_switching_coherence),
            3: ("Test 3: Rapid-Fire - No Duplicate Responses", test_3_rapid_fire_messages),
            4: ("Test 4: Long Conversation - No Repetition", test_4_long_conversation_no_repetition),
            5: ("Test 5: Error Recovery - Graceful Handling", test_5_error_recovery_graceful),
        }

        test_name, test_func = tests[args.test]
        print(f"\nRunning: {test_name}\n")

        try:
            passed = test_func()
            print(f"\nResult: {'PASSED' if passed else 'FAILED'}")
            return 0 if passed else 1
        except ConnectionError as e:
            print(f"\nConnection Error: {e}")
            print("Make sure both servers are running!")
            return 1
        except Exception as e:
            print(f"\nError: {e}")
            import traceback
            traceback.print_exc()
            return 1

    # Run all tests
    try:
        summary = run_all_quality_tests()
        return 0 if summary["failed"] == 0 else 1
    except ConnectionError as e:
        print(f"\nConnection Error: {e}")
        print("\nMake sure both servers are running:")
        print("  1. Mock UAZAPI: python mock_uazapi_server.py")
        print("  2. Bot: WHATSAPP_API_URL=http://localhost:8080 dotnet run")
        return 1


if __name__ == "__main__":
    sys.exit(main())
