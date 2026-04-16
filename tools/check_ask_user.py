#!/usr/bin/env python3
"""
Simple pre-commit check: verify that staged agent-like source files reference `ask_user(` at least once.
Heuristic: for staged files (added/modified), if filename or file content contains 'Agent' or 'agent', require 'ask_user(' to appear in the file.
Exit non-zero to abort commit on violations.
"""
import sys
import subprocess
from pathlib import Path

def get_staged_files():
    p = subprocess.run(["git","diff","--cached","--name-only","--diff-filter=ACM"], capture_output=True, text=True)
    if p.returncode != 0:
        print("Failed to get staged files", file=sys.stderr)
        sys.exit(1)
    return [Path(x) for x in p.stdout.splitlines() if x]


def file_needs_check(path: Path) -> bool:
    if not path.exists():
        return False
    if path.suffix.lower() not in {'.cs', '.py', '.js', '.ts'}:
        return False
    txt = path.read_text(encoding='utf-8', errors='ignore')
    if 'agent' in path.name.lower():
        return True
    if 'class ' in txt and ('Agent' in txt or 'agent' in txt):
        return True
    return False


def has_ask_user(path: Path) -> bool:
    txt = path.read_text(encoding='utf-8', errors='ignore')
    return 'ask_user(' in txt or 'askUser(' in txt or 'ask_user ' in txt


def main():
    files = get_staged_files()
    violations = []
    for f in files:
        p = Path(f)
        if file_needs_check(p):
            if not has_ask_user(p):
                violations.append(str(p))
    if violations:
        print("ERROR: The following staged files look like agent code but do not contain 'ask_user(':")
        for v in violations:
            print(" - ", v)
        print("\nPolicy: agent code must call ask_user before ending a conversation. Add a call or mark the file as not-applicable.")
        sys.exit(2)
    return 0

if __name__ == '__main__':
    sys.exit(main())
