---
description: "PRD.md + FEASIBILITY_REPORT.md 기반으로 구현 계획 및 체크리스트 생성"
mode: "agent"
---

# /plan — 구현 계획 수립

## 역할

`PRD.md`와 `FEASIBILITY_REPORT.md`를 기반으로, 즉시 실행 가능한 `IMPLEMENTATION_PLAN.md`와 `IMPLEMENTATION_CHECKLIST.md`를 생성한다.

## 지침

1. **입력 문서 읽기**
   - `PRD.md` — 요구사항, 아키텍처, 기술 스택, 구현 단계 개요, 검증 기준
   - `FEASIBILITY_REPORT.md` — 정확한 패키지명, 버전, API 패턴, 코드 샘플, NuGet 목록, 리스크
   - 기존 `IMPLEMENTATION_PLAN.md` / `IMPLEMENTATION_CHECKLIST.md`가 있으면 읽고 갱신한다.

2. **`구현-계획관` 에이전트 활용**
   - 복잡한 의존성 분석이나 단계 분할은 `구현-계획관` 서브에이전트에 위임할 수 있다.

3. **IMPLEMENTATION_PLAN.md 작성 규칙**
   - 구조:
     ```
     ## 구현 목표 — 한 문단 요약
     ## 기술 스택 — 테이블 (구성요소, 기술, 버전, 비고)
     ## 아키텍처 개요 — ASCII 다이어그램 + 핵심 데이터 흐름
     ## 구현 단계 — N단계 (각 단계에 아래 포함):
       - 목표
       - 태스크 (번호 매김, 구체적 커맨드/코드 패턴 포함)
       - 검증 기준 (자동화 가능한 형태)
       - 예상 리스크
     ## 의존성 및 사전 준비 — 테이블
     ## 리스크 맵 — 테이블 (리스크, 영향도, 발생 확률, 대응 전략)
     ## 참고 자료 — 링크 테이블
     ```
   - 각 태스크에 실행 가능한 **구체적 커맨드** (`dotnet new`, `dotnet add`, NuGet 설치 등)를 포함한다.
   - `FEASIBILITY_REPORT.md`의 코드 패턴을 태스크에 직접 인용하거나 참조한다.
   - 단계 간 의존성을 명확히 한다 (N단계는 N-1단계 완료 전제).

4. **IMPLEMENTATION_CHECKLIST.md 작성 규칙**
   - PLAN의 각 단계 → CHECKLIST의 섹션으로 1:1 매핑한다.
   - 모든 태스크를 `- [ ]` 체크박스로 나열한다.
   - 각 단계 끝에 검증 태스크를 포함한다.
   - 헤더에 PLAN.md와 FEASIBILITY_REPORT.md 참조를 명시한다.

5. **단계 번호 일관성**
   - PLAN, CHECKLIST, PRD의 구현 단계 번호를 반드시 동일하게 맞춘다.
   - PRD.md의 구현 단계 섹션도 필요시 함께 갱신한다.

6. **완료 후 안내**
   - 계획 수립 완료 후, `/implement`로 구현을 시작할 수 있음을 안내한다.

## 사용 예시

- `/plan` — PRD.md + FEASIBILITY_REPORT.md 기반으로 전체 계획 생성
- `/plan 갱신` — 기존 PLAN/CHECKLIST를 PRD 변경사항에 맞춰 갱신
- `/plan {N}단계 상세화` — 특정 단계의 태스크를 더 세분화

## 산출물

- `IMPLEMENTATION_PLAN.md` — 단계별 구현 계획 (커맨드, 코드 패턴, 검증 기준, 리스크 맵)
- `IMPLEMENTATION_CHECKLIST.md` — 세부 태스크 체크리스트 (`[ ]` / `[x]` 추적)

## 다음 단계

PLAN + CHECKLIST 완성 후 → `/implement`로 단계별 구현 시작
