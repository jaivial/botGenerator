"""
Log Quality Analyzer for WhatsApp Bot Conversation Tests

This module analyzes conversation logs to detect:
1. Message duplications (bot repeating itself)
2. Non-human patterns (robotic responses, repetitive phrases)
3. Conversation flow issues (missing responses, out-of-order messages)
4. Strange patterns that indicate bot malfunction
"""

import re
from pathlib import Path
from dataclasses import dataclass, field
from typing import Optional
from datetime import datetime


@dataclass
class LogMessage:
    """Represents a single message in the log"""
    timestamp: str
    sender: str  # 'client' or 'bot'
    text: str
    line_number: int


@dataclass
class QualityIssue:
    """Represents a quality issue found in the log"""
    issue_type: str
    severity: str  # 'warning', 'error', 'critical'
    description: str
    line_number: Optional[int] = None
    details: dict = field(default_factory=dict)


@dataclass
class LogAnalysisResult:
    """Result of analyzing a conversation log"""
    log_path: str
    test_name: str
    total_messages: int
    client_messages: int
    bot_messages: int
    issues: list[QualityIssue] = field(default_factory=list)
    passed: bool = True

    @property
    def has_errors(self) -> bool:
        return any(i.severity in ('error', 'critical') for i in self.issues)

    @property
    def has_warnings(self) -> bool:
        return any(i.severity == 'warning' for i in self.issues)


class LogQualityAnalyzer:
    """Analyzes conversation logs for quality issues"""

    # Patterns that indicate non-human/robotic responses
    NON_HUMAN_PATTERNS = [
        # System/debug messages leaked
        (r'\[DEBUG\]', 'Debug message leaked into response'),
        (r'\[ERROR\]', 'Error message leaked into response'),
        (r'\[INFO\]', 'Info message leaked into response'),
        (r'Exception:', 'Exception message leaked into response'),
        (r'StackTrace', 'Stack trace leaked into response'),
        (r'NullReferenceException', 'Null reference exception leaked'),

        # JSON/code leaked
        (r'\{"[^"]+"\s*:', 'JSON structure leaked into response'),
        (r'<[a-z]+>', 'HTML/XML tags in response'),

        # Prompt/instruction leakage
        (r'you are a', 'Prompt instructions leaked'),
        (r'your role is', 'Role instructions leaked'),
        (r'as an AI', 'AI self-reference leaked'),
        (r'as a language model', 'AI self-reference leaked'),

        # Placeholder text
        (r'\[INSERT_', 'Placeholder text in response'),
        (r'\{placeholder\}', 'Placeholder text in response'),
        (r'TODO:', 'TODO marker in response'),

        # Repeated punctuation (unusual)
        (r'\.{4,}', 'Excessive periods'),
        (r'\?{3,}', 'Excessive question marks'),
        (r'!{4,}', 'Excessive exclamation marks'),
    ]

    # Patterns for Spanish bot responses that might indicate problems
    SPANISH_QUALITY_PATTERNS = [
        # Empty or meaningless responses
        (r'^[\s\.\,\?\!]*$', 'Empty or meaningless response'),

        # Broken encoding
        (r'Ã©|Ã¡|Ã­|Ã³|Ã±', 'Broken UTF-8 encoding'),
    ]

    def __init__(self, logs_dir: str = "logs"):
        self.logs_dir = Path(logs_dir)

    def parse_log_file(self, log_path: str | Path) -> tuple[list[LogMessage], dict]:
        """
        Parse a conversation log file.

        Returns:
            Tuple of (messages list, metadata dict)
        """
        log_path = Path(log_path)
        messages = []
        metadata = {}

        with open(log_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        in_conversation = False
        line_num = 0

        for line in lines:
            line_num += 1
            line = line.rstrip('\n')

            # Parse metadata from header
            if line.startswith('Test Name:'):
                metadata['test_name'] = line.split(':', 1)[1].strip()
            elif line.startswith('Phone:'):
                metadata['phone'] = line.split(':', 1)[1].strip()
            elif line.startswith('Duration:'):
                metadata['duration'] = line.split(':', 1)[1].strip()
            elif line.startswith('Result:'):
                metadata['result'] = line.split(':', 1)[1].strip()
            elif line.startswith('CONVERSATION (timestamped):'):
                in_conversation = True
                continue
            elif line.startswith('SUMMARY:'):
                in_conversation = False

            # Parse conversation messages
            if in_conversation and line and not line.startswith('-' * 10):
                # Format: dd-mm-yy hh-mm -sender message
                match = re.match(r'^(\d{2}-\d{2}-\d{2}\s+\d{2}-\d{2})\s+-(\w+)\s+(.*)$', line)
                if match:
                    messages.append(LogMessage(
                        timestamp=match.group(1),
                        sender=match.group(2),
                        text=match.group(3),
                        line_number=line_num
                    ))

        return messages, metadata

    def check_duplications(self, messages: list[LogMessage]) -> list[QualityIssue]:
        """Check for duplicated bot messages"""
        issues = []
        bot_messages = [m for m in messages if m.sender == 'bot']

        # Check for exact duplications
        seen_messages = {}
        for msg in bot_messages:
            normalized = msg.text.lower().strip()
            if normalized in seen_messages:
                issues.append(QualityIssue(
                    issue_type='duplication',
                    severity='error',
                    description=f'Bot message duplicated exactly',
                    line_number=msg.line_number,
                    details={
                        'original_line': seen_messages[normalized],
                        'duplicate_line': msg.line_number,
                        'text': msg.text[:100]
                    }
                ))
            else:
                seen_messages[normalized] = msg.line_number

        # Check for near-duplications (very similar messages)
        for i, msg1 in enumerate(bot_messages):
            for j, msg2 in enumerate(bot_messages[i+1:], i+1):
                similarity = self._calculate_similarity(msg1.text, msg2.text)
                if 0.85 < similarity < 1.0:  # Very similar but not exact
                    issues.append(QualityIssue(
                        issue_type='near_duplication',
                        severity='warning',
                        description=f'Bot messages very similar ({similarity:.0%})',
                        line_number=msg2.line_number,
                        details={
                            'message1': msg1.text[:80],
                            'message2': msg2.text[:80],
                            'similarity': similarity
                        }
                    ))

        # Check for consecutive identical responses
        for i in range(1, len(bot_messages)):
            if bot_messages[i].text == bot_messages[i-1].text:
                issues.append(QualityIssue(
                    issue_type='consecutive_duplication',
                    severity='critical',
                    description='Bot sent identical consecutive messages',
                    line_number=bot_messages[i].line_number,
                    details={'text': bot_messages[i].text[:100]}
                ))

        return issues

    def check_non_human_patterns(self, messages: list[LogMessage]) -> list[QualityIssue]:
        """Check for non-human patterns in bot responses"""
        issues = []

        for msg in messages:
            if msg.sender != 'bot':
                continue

            # Check against non-human patterns
            for pattern, description in self.NON_HUMAN_PATTERNS:
                if re.search(pattern, msg.text, re.IGNORECASE):
                    issues.append(QualityIssue(
                        issue_type='non_human_pattern',
                        severity='error',
                        description=description,
                        line_number=msg.line_number,
                        details={'matched_pattern': pattern, 'text': msg.text[:100]}
                    ))

            # Check Spanish quality patterns
            for pattern, description in self.SPANISH_QUALITY_PATTERNS:
                if re.search(pattern, msg.text):
                    issues.append(QualityIssue(
                        issue_type='quality_issue',
                        severity='error',
                        description=description,
                        line_number=msg.line_number,
                        details={'text': msg.text[:100]}
                    ))

            # Check for excessively long response (might indicate runaway generation)
            if len(msg.text) > 2000:
                issues.append(QualityIssue(
                    issue_type='excessive_length',
                    severity='warning',
                    description=f'Bot response very long ({len(msg.text)} chars)',
                    line_number=msg.line_number,
                    details={'length': len(msg.text)}
                ))

            # Check for repeated words/phrases (robotic)
            repeated = self._find_repeated_phrases(msg.text)
            if repeated:
                issues.append(QualityIssue(
                    issue_type='repetitive_text',
                    severity='warning',
                    description=f'Repeated phrases in response: {repeated[:3]}',
                    line_number=msg.line_number,
                    details={'repeated_phrases': repeated}
                ))

        return issues

    def check_conversation_flow(self, messages: list[LogMessage]) -> list[QualityIssue]:
        """Check for conversation flow issues"""
        issues = []

        if not messages:
            issues.append(QualityIssue(
                issue_type='empty_conversation',
                severity='critical',
                description='No messages found in conversation'
            ))
            return issues

        # Check alternation (client-bot-client-bot pattern)
        prev_sender = None
        for msg in messages:
            if prev_sender == msg.sender == 'client':
                # Client sent two messages without bot response - might be ok
                pass
            elif prev_sender == msg.sender == 'bot':
                # Bot sent two consecutive messages - check if problem
                issues.append(QualityIssue(
                    issue_type='flow_issue',
                    severity='warning',
                    description='Bot sent consecutive messages without client input',
                    line_number=msg.line_number,
                    details={'text': msg.text[:80]}
                ))
            prev_sender = msg.sender

        # Check for [NO RESPONSE RECEIVED] markers
        for msg in messages:
            if msg.sender == 'bot' and '[NO RESPONSE RECEIVED]' in msg.text:
                issues.append(QualityIssue(
                    issue_type='missing_response',
                    severity='critical',
                    description='Bot failed to respond to client message',
                    line_number=msg.line_number
                ))

        # Check that conversation starts with client
        if messages and messages[0].sender != 'client':
            issues.append(QualityIssue(
                issue_type='flow_issue',
                severity='warning',
                description='Conversation does not start with client message'
            ))

        return issues

    def check_response_coherence(self, messages: list[LogMessage]) -> list[QualityIssue]:
        """Check for response coherence issues"""
        issues = []

        # Build client-bot pairs
        pairs = []
        for i, msg in enumerate(messages):
            if msg.sender == 'client' and i + 1 < len(messages):
                next_msg = messages[i + 1]
                if next_msg.sender == 'bot':
                    pairs.append((msg, next_msg))

        # Check for responses that seem unrelated to questions
        greeting_keywords = ['hola', 'buenos', 'buenas', 'hello', 'hi']
        booking_keywords = ['reserva', 'reservar', 'booking', 'book']

        for client_msg, bot_msg in pairs:
            client_lower = client_msg.text.lower()
            bot_lower = bot_msg.text.lower()

            # If client greets, bot should acknowledge or greet back
            if any(kw in client_lower for kw in greeting_keywords):
                if not any(kw in bot_lower for kw in ['hola', 'bienvenid', 'buenos', 'buenas']):
                    # Not necessarily an error, but worth checking
                    pass

            # If client asks a question, bot should not ignore it
            if '?' in client_msg.text and len(bot_msg.text) < 10:
                issues.append(QualityIssue(
                    issue_type='possibly_ignored_question',
                    severity='warning',
                    description='Client question got very short response',
                    line_number=bot_msg.line_number,
                    details={
                        'question': client_msg.text[:80],
                        'response': bot_msg.text
                    }
                ))

        return issues

    def analyze_log(self, log_path: str | Path) -> LogAnalysisResult:
        """
        Perform full analysis of a conversation log.

        Args:
            log_path: Path to the log file

        Returns:
            LogAnalysisResult with all findings
        """
        log_path = Path(log_path)
        messages, metadata = self.parse_log_file(log_path)

        all_issues = []
        all_issues.extend(self.check_duplications(messages))
        all_issues.extend(self.check_non_human_patterns(messages))
        all_issues.extend(self.check_conversation_flow(messages))
        all_issues.extend(self.check_response_coherence(messages))

        client_count = sum(1 for m in messages if m.sender == 'client')
        bot_count = sum(1 for m in messages if m.sender == 'bot')

        result = LogAnalysisResult(
            log_path=str(log_path),
            test_name=metadata.get('test_name', 'Unknown'),
            total_messages=len(messages),
            client_messages=client_count,
            bot_messages=bot_count,
            issues=all_issues,
            passed=not any(i.severity in ('error', 'critical') for i in all_issues)
        )

        return result

    def find_latest_log(self, test_name_pattern: str) -> Optional[Path]:
        """
        Find the most recent log file matching a test name pattern.

        Args:
            test_name_pattern: Substring to match in filename

        Returns:
            Path to the most recent matching log, or None
        """
        matching_logs = []
        pattern = test_name_pattern.lower().replace(' ', '_')

        for log_file in self.logs_dir.glob('*.log'):
            if pattern in log_file.name.lower():
                matching_logs.append(log_file)

        if not matching_logs:
            return None

        # Sort by modification time, return newest
        return max(matching_logs, key=lambda p: p.stat().st_mtime)

    def _calculate_similarity(self, text1: str, text2: str) -> float:
        """Calculate similarity ratio between two texts"""
        # Simple word-based Jaccard similarity
        words1 = set(text1.lower().split())
        words2 = set(text2.lower().split())

        if not words1 or not words2:
            return 0.0

        intersection = words1 & words2
        union = words1 | words2

        return len(intersection) / len(union)

    def _find_repeated_phrases(self, text: str, min_words: int = 3, min_occurrences: int = 2) -> list[str]:
        """Find phrases that are repeated in the text"""
        words = text.lower().split()
        repeated = []

        if len(words) < min_words * min_occurrences:
            return repeated

        # Check for repeated n-grams
        for n in range(min_words, min(6, len(words) // 2)):
            ngrams = {}
            for i in range(len(words) - n + 1):
                phrase = ' '.join(words[i:i+n])
                ngrams[phrase] = ngrams.get(phrase, 0) + 1

            for phrase, count in ngrams.items():
                if count >= min_occurrences:
                    repeated.append(phrase)

        return repeated


def format_analysis_report(result: LogAnalysisResult) -> str:
    """Format an analysis result as a readable report"""
    lines = [
        "=" * 70,
        f"LOG QUALITY ANALYSIS REPORT",
        "=" * 70,
        f"Log File:   {result.log_path}",
        f"Test Name:  {result.test_name}",
        f"Messages:   {result.total_messages} total ({result.client_messages} client, {result.bot_messages} bot)",
        f"Status:     {'PASSED' if result.passed else 'FAILED'}",
        "=" * 70,
    ]

    if result.issues:
        lines.append("\nISSUES FOUND:")
        lines.append("-" * 70)

        # Group by severity
        for severity in ['critical', 'error', 'warning']:
            severity_issues = [i for i in result.issues if i.severity == severity]
            if severity_issues:
                lines.append(f"\n[{severity.upper()}] ({len(severity_issues)} issues)")
                for issue in severity_issues:
                    lines.append(f"  - {issue.description}")
                    if issue.line_number:
                        lines.append(f"    Line: {issue.line_number}")
                    if issue.details:
                        for k, v in issue.details.items():
                            lines.append(f"    {k}: {v}")
    else:
        lines.append("\nNo issues found - conversation quality is good!")

    lines.append("\n" + "=" * 70)
    return "\n".join(lines)


# CLI usage
if __name__ == "__main__":
    import sys

    if len(sys.argv) < 2:
        print("Usage: python log_quality_analyzer.py <log_file>")
        print("       python log_quality_analyzer.py --latest <test_name_pattern>")
        sys.exit(1)

    analyzer = LogQualityAnalyzer()

    if sys.argv[1] == '--latest':
        pattern = sys.argv[2] if len(sys.argv) > 2 else ''
        log_path = analyzer.find_latest_log(pattern)
        if not log_path:
            print(f"No log found matching pattern: {pattern}")
            sys.exit(1)
    else:
        log_path = sys.argv[1]

    result = analyzer.analyze_log(log_path)
    print(format_analysis_report(result))
    sys.exit(0 if result.passed else 1)
