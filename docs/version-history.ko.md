# Version History

## 0.1.0 — LLM Wiki 관계 그래프 및 유지보수 (2026-05-01)

### Major Features

- Typed Relationship Wikilinks — `@supports`, `@contradicts`, `@supersedes`, `@depends_on` 등 의미 기반 관계 타입 지원 추가
- Relationship Retag Maintenance — 기존 일반 위키링크를 근거 기반 typed relationship 후보로 제안하고 선택 적용하는 유지보수 흐름 추가
- Relationship Retag Review UI — Lint 화면에서 관계 제안 확인, 개별 선택, 적용 전 확인 모달을 통한 안전 적용 지원
- Typed Knowledge Graph Controls — 그래프에서 관계 타입별 색상, 라벨, 범례, 필터 토글 지원

### Major Improvements

- LLM Wiki Ingest Quality — raw source를 불변 근거로 두고 source, confidence, stale, contradiction, supersession 정보를 생성하도록 인제스트 지침 강화
- Wikilink Normalization — alias, heading, folder path, local heading, relationship marker를 정규화해 페이지 연결 안정성 개선
- Broken Link Maintenance — 코드 블록 내부 예시 링크를 무시하고 근접 일치 repair 및 미해결 링크 plain text 전환 지원
- MCP Relationship Tools — 관계 retag proposal 생성 및 confidence 기반 적용을 MCP 도구로 노출
- Localization Freshness — 새 UI 문자열이 브라우저/WebView 캐시에 묻히지 않도록 locale cache-busting 강화

### Major Bug Fixes

- Graph Render Lifecycle — 그래프 컨테이너가 DOM에 생성된 뒤 렌더링하도록 Blazor 렌더링 순서 문제 수정
- Graph Offline Reliability — D3 CDN 의존성을 제거하고 로컬 SVG force layout으로 그래프 렌더링 안정화
- Code Link Lint Accuracy — fenced code 및 inline code 내부 위키링크가 broken link로 오탐되는 문제 수정

### Quality / Validation

- Relationship Regression Tests — typed link parsing, graph edge type extraction, selected retag apply, broken-link repair 회귀 테스트 추가
- End-to-End Validation — 전체 테스트 52개 통과, 전체 빌드 성공, Graph/Lint UI 브라우저 시각 검증 완료
