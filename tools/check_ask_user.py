from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AGENTS_FILE = ROOT / "AGENTS.md"
PROMPTS_DIR = ROOT / ".github" / "prompts"
PROMPT_FILES = sorted(PROMPTS_DIR.glob("*.prompt.md"))

REQUIRED_AGENT_SNIPPETS = (
    "`ask_user`는 Git 커밋 여부와 무관하게, 바이브 코딩 세션 자체에 적용되는 **응답 하네스(response harness)** 로 간주한다.",
    "사용자에게 보여주는 최종 응답은 반드시 `ask_user`를 통해 전달한다. 일반 텍스트만 남기고 턴을 종료하지 않는다.",
    "첫 `ask_user` 호출은 `session_id: \"auto\"`를 사용하고, 이후 모든 호출은 직전 응답에서 받은 동일한 `session_id`를 그대로 재사용한다.",
    '"이 턴의 마지막 동작이 `ask_user`인가?"',
)

REQUIRED_PROMPT_SNIPPETS = (
    "## 응답 하네스",
    "- 이 프롬프트의 사용자 가시 응답은 모두 `ask_user`를 통해 전달한다.",
    "- `ask_user`는 Git 커밋 여부와 무관하게, 바이브 코딩 세션 자체에 적용되는 **응답 하네스(response harness)** 로 간주한다.",
    "- 첫 `ask_user` 호출은 `session_id: \"auto\"`를 사용하고, 이후에는 직전 응답에서 받은 동일한 `session_id`를 그대로 재사용한다.",
    "- 일반 텍스트만 남기고 턴을 종료하지 않는다. 사용자가 `종료`라고 말하기 전까지 대화를 계속 이어간다.",
)


def collect_missing_snippets(path: Path, required_snippets: tuple[str, ...]) -> list[str]:
    if not path.exists():
        return ["<file missing>"]

    text = path.read_text(encoding="utf-8")
    return [snippet for snippet in required_snippets if snippet not in text]



def main() -> int:
    failures: list[str] = []

    missing_agent_snippets = collect_missing_snippets(AGENTS_FILE, REQUIRED_AGENT_SNIPPETS)
    if missing_agent_snippets:
        failures.append(f"[AGENTS] {AGENTS_FILE.relative_to(ROOT)}")
        for snippet in missing_agent_snippets:
            failures.append(f"  - missing: {snippet}")

    if not PROMPT_FILES:
        failures.append("[PROMPTS] .github/prompts/*.prompt.md files were not found")
    else:
        for prompt_file in PROMPT_FILES:
            missing_prompt_snippets = collect_missing_snippets(prompt_file, REQUIRED_PROMPT_SNIPPETS)
            if missing_prompt_snippets:
                failures.append(f"[PROMPT] {prompt_file.relative_to(ROOT)}")
                for snippet in missing_prompt_snippets:
                    failures.append(f"  - missing: {snippet}")

    if failures:
        print("ask_user harness policy check failed.", file=sys.stderr)
        print("Please restore the required harness rules before continuing.", file=sys.stderr)
        for line in failures:
            print(line, file=sys.stderr)
        return 1

    print("ask_user harness policy check passed.")
    print(f"Validated AGENTS.md and {len(PROMPT_FILES)} prompt file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
