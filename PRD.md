# PRD: MindAtlas — LLM Wiki 기반 개인 지식백과 + 바이브코딩 허브

## 1. 개요

### 1.1 비전
MindAtlas는 Karpathy의 **LLM Wiki 패턴**(3-Layer, 3-Operation)을 기반으로 한 **개인 지식백과 애플리케이션**이다. 단순한 지식 관리를 넘어, 바이브 코딩에 필요한 에이전트 설정·룰·스킬·명령어·정책까지 자동 문서화하고 필요할 때 꺼내 쓸 수 있는 **개발자 지식 허브**이다.

### 1.2 핵심 가치
- **LLM이 위키를 쓴다** — raw 소스를 넣으면 AI Agent가 자동으로 지식을 정제·분류·연결
- **빠름 + 정확함** — 정형 검색(index.md 키워드)은 즉시, 비정형 처리(Ingest/Query/Lint)는 AI Agent가 수행
- **MCP로 열린 입구** — 외부 도구(VS Code, ChatGPT 등)에서 MCP를 통해 지식을 바로 저장
- **원클릭 데스크톱 앱** — 단축키로 띄워서 드래그 앤 드롭만으로 지식 입력

### 1.3 참고 자료
| 자료 | 역할 |
|------|------|
| [Karpathy LLM Wiki Gist](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f) | LLM Wiki 패턴 원본 |
| [desmarchris/llm-wiki](https://github.com/desmarchris/llm-wiki) | 3레이어 템플릿 구현체 |
| P:\문서\260414_LLM위키 | LLM Wiki 워크숍 자료 (슬라이드, 안내서) |
| [GitHub Copilot SDK](https://github.com/github/copilot-sdk) (공식) | 백엔드 AI Agent 엔진 (.NET SDK v0.2.2) |
| [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) | MCP 서버 구현 (v1.2.0) |
| **FEASIBILITY_REPORT.md** | **실현가능성 조사 보고서 — 구현 시 반드시 참조** |
| **IMPLEMENTATION_PLAN.md** | **구현 계획 — 단계별 태스크, 커맨드, 검증 기준, 리스크 맵** |

---

## 2. 아키텍처

### 2.1 전체 구조

```
┌──────────────────────────────────────────────────────────────┐
│  Frontend: Blazor WebAssembly + Avalonia Shell               │
│  ┌─────────────┐  ┌────────────┐  ┌───────────────────────┐ │
│  │  Quick Input │  │  Wiki View │  │  Search & Browse      │ │
│  │  (D&D/Clip)  │  │  (Reader)  │  │  (keyword + semantic) │ │
│  └──────┬───────┘  └─────┬──────┘  └──────────┬────────────┘ │
│         │                │                     │              │
│         └────────────────┼─────────────────────┘              │
│                          │ HTTP/SignalR                       │
├──────────────────────────┼───────────────────────────────────┤
│  Backend: .NET/C# ASP.NET Core                               │
│  ┌─────────────┐  ┌─────┴──────┐  ┌───────────────────────┐ │
│  │ FileWatcher  │  │ Wiki API   │  │ MCP Server            │ │
│  │ (raw/ 감지)  │  │ (CRUD)     │  │ (외부 지식 입력)      │ │
│  └──────┬───────┘  └─────┬──────┘  └──────────┬────────────┘ │
│         │                │                     │              │
│         └────────────────┼─────────────────────┘              │
│                          │                                    │
│  ┌───────────────────────┴──────────────────────────────────┐ │
│  │  LLM Wiki Engine                                         │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐               │ │
│  │  │ Ingest   │  │ Query    │  │ Lint     │               │ │
│  │  └──────────┘  └──────────┘  └──────────┘               │ │
│  │  ┌──────────────────────┐  ┌───────────────────────────┐ │ │
│  │  │ Fast Index Search    │  │ GitHub Copilot SDK Agent  │ │ │
│  │  │ (keyword, O(ms))     │  │ (LLM reasoning)          │ │ │
│  │  └──────────────────────┘  └───────────────────────────┘ │ │
│  └──────────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────┤
│  Storage: 로컬 파일시스템                                     │
│  ┌────────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ raw/           │  │ wiki/        │  │ schema/          │  │
│  │ (불변 원본)     │  │ (LLM 위키)   │  │ (AGENTS.md 등)  │  │
│  └────────────────┘  └──────────────┘  └──────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### 2.2 LLM Wiki 3-Layer 매핑

| Layer | 역할 | MindAtlas 구현 |
|-------|------|---------------|
| **Layer 1: Raw Sources** | 불변 원본 저장소 | `raw/` 디렉토리 — 문서, 이미지, 클립보드 텍스트 등 |
| **Layer 2: Wiki** | LLM이 생성·유지하는 마크다운 위키 | `wiki/` 디렉토리 — `[[wikilinks]]`, `index.md`, `log.md` |
| **Layer 3: Schema** | 위키 규칙·컨벤션 정의 | `schema/AGENTS.md` — Ingest/Query/Lint 규칙, 페이지 형식 |

### 2.3 3-Operation 매핑

| Operation | 설명 | 트리거 |
|-----------|------|--------|
| **Ingest** | raw 소스 → 위키 페이지 생성/업데이트 | raw/ 파일 추가 감지 (FileWatcher) 또는 MCP 호출 |
| **Query** | 위키 기반 질문 답변 + 인사이트 기록 | 프론트엔드 검색 또는 MCP 호출 |
| **Lint** | 위키 건강 검진 (고아페이지, 모순, 누락 링크) | 수동 실행 또는 스케줄 |

---

## 3. 기술 스택

| 구성요소 | 기술 | 비고 |
|----------|------|------|
| **백엔드** | .NET 9 / C# / ASP.NET Core | Web API + SignalR |
| **AI Agent** | GitHub Copilot SDK (.NET) | Ingest/Query/Lint 수행 |
| **프론트엔드 UI** | Blazor WebAssembly | SPA, 마크다운 렌더링 |
| **데스크톱 셸** | Avalonia UI | 트레이 상주, 전역 단축키, D&D |
| **데스크톱 통합** | Avalonia + WebView | Blazor WASM을 Avalonia 안에 래핑 |
| **MCP 서버** | ASP.NET Core (stdio/SSE) | Model Context Protocol 구현 |
| **파일 감시** | FileSystemWatcher (.NET) | raw/ 디렉토리 변경 감지 |
| **빠른 검색** | 인메모리 인덱스 (index.md 파싱) | 키워드 기반 O(ms) 검색 |
| **마크다운 처리** | Markdig | 파싱, 렌더링, wikilink 처리 |
| **로컬 저장소** | 파일시스템 (raw/, wiki/, schema/) | Git 연동 권장 |

---

## 4. 기능 요구사항

### 4.1 데스크톱 앱 (Avalonia Shell)

| ID | 기능 | 설명 | 우선순위 |
|----|------|------|----------|
| F-APP-01 | 전역 단축키 실행 | 시스템 전역 단축키(기본: `Ctrl+Shift+Space`)로 앱 활성화/토글 | P0 |
| F-APP-02 | 시스템 트레이 상주 | 최소화 시 트레이 아이콘, 우클릭 메뉴 (열기/설정/종료) | P0 |
| F-APP-03 | 드래그 앤 드롭 입력 | 문서(md, txt, pdf), 이미지(png, jpg), 파일을 앱에 드롭 → raw/로 복사 | P0 |
| F-APP-04 | 클립보드 입력 | 클립보드 텍스트/이미지를 붙여넣기로 raw/에 저장 | P1 |
| F-APP-05 | 빠른 입력 창 | 텍스트를 직접 입력하여 raw/에 메모로 저장 | P1 |
| F-APP-06 | 시작 시 자동 실행 | OS 시작 시 트레이에 자동 상주 (설정 가능) | P2 |

### 4.2 위키 뷰어 (Blazor WebAssembly)

| ID | 기능 | 설명 | 우선순위 |
|----|------|------|----------|
| F-VIEW-01 | 위키 페이지 목록 | index.md 기반 전체 페이지 카탈로그 표시 | P0 |
| F-VIEW-02 | 마크다운 렌더링 | wiki/ 페이지를 HTML로 렌더링, `[[wikilinks]]` 클릭 네비게이션 | P0 |
| F-VIEW-03 | 키워드 검색 | index.md의 키워드·태그 기반 실시간 필터링 (인메모리, 밀리초) | P0 |
| F-VIEW-04 | AI Query | 자연어 질문 → GitHub Copilot SDK Agent가 위키 기반 답변 생성 | P1 |
| F-VIEW-05 | 지식 그래프 | `[[wikilinks]]` 기반 페이지 관계 시각화 | P2 |
| F-VIEW-06 | 활동 로그 | log.md 기반 최근 활동 타임라인 표시 | P1 |
| F-VIEW-07 | Lint 대시보드 | Lint 결과 (고아페이지, 깨진 링크, 모순 등) 시각화 | P2 |

### 4.3 백엔드 — LLM Wiki Engine

| ID | 기능 | 설명 | 우선순위 |
|----|------|------|----------|
| F-ENG-01 | raw/ FileWatcher | raw/ 디렉토리 변경 감지 → Ingest 파이프라인 트리거 | P0 |
| F-ENG-02 | Ingest 파이프라인 | raw 소스 분석 → 위키 페이지 생성/업데이트 → index.md·log.md 갱신 | P0 |
| F-ENG-03 | Query 엔진 | 빠른 키워드 검색(index.md) + AI Agent 시맨틱 답변 혼합 | P0 |
| F-ENG-04 | Lint 엔진 | 고아페이지, 깨진 wikilink, 모순, index.md 불일치 탐지 | P1 |
| F-ENG-05 | GitHub Copilot SDK Agent | Ingest/Query/Lint를 수행하는 AI Agent 인스턴스 관리 | P0 |
| F-ENG-06 | Fast Index | index.md + wiki 메타데이터 인메모리 캐시, 파일 변경 시 자동 리로드 | P0 |
| F-ENG-07 | Schema 관리 | schema/AGENTS.md 로드 및 Agent에 주입 | P0 |
| F-ENG-08 | 바이브코딩 자산 관리 | 에이전트 설정, 룰, 스킬, 명령어, 정책을 위키에서 추출·제공 | P1 |

### 4.4 MCP 서버

| ID | 기능 | 설명 | 우선순위 |
|----|------|------|----------|
| F-MCP-01 | ingest 도구 | 외부에서 텍스트/파일을 raw/에 저장하고 Ingest 트리거 | P0 |
| F-MCP-02 | query 도구 | 외부에서 위키에 질문하고 답변 수신 | P1 |
| F-MCP-03 | search 도구 | 키워드 기반 빠른 위키 검색 | P1 |
| F-MCP-04 | lint 도구 | 외부에서 Lint 실행 및 결과 수신 | P2 |
| F-MCP-05 | get-asset 도구 | 바이브코딩 자산(에이전트/룰/스킬/정책) 검색·조회 | P1 |
| F-MCP-06 | stdio/SSE 전송 | 로컬 stdio + 네트워크 SSE 양방향 지원 | P0 |

---

## 5. 디렉토리 구조 (데이터)

```
~/.mindatlas/                      # 사용자 데이터 루트 (설정 가능)
├── raw/                            # Layer 1: 불변 원본
│   ├── 2026-04-15_drag_rust.md
│   ├── 2026-04-15_clip_memo.md
│   ├── 2026-04-15_photo.png
│   └── ...
├── wiki/                           # Layer 2: LLM이 유지하는 위키
│   ├── index.md                    # 전체 페이지 카탈로그 (빠른 검색 키)
│   ├── log.md                      # 활동 기록
│   ├── rust-ownership.md
│   ├── borrow-checker.md
│   ├── copilot-agent-setup.md      # 바이브코딩 자산 예시
│   └── ...
├── schema/                         # Layer 3: Schema
│   └── AGENTS.md                   # 위키 규칙 정의
└── config.json                     # 앱 설정 (단축키, 경로, LLM 모델 등)
```

---

## 6. 프로젝트 구조 (소스코드)

```
MindAtlas/
├── src/
│   ├── MindAtlas.Core/              # 공유 도메인 모델, 인터페이스
│   │   ├── Models/
│   │   ├── Interfaces/
│   │   └── MindAtlas.Core.csproj
│   ├── MindAtlas.Engine/            # LLM Wiki 엔진 (Ingest/Query/Lint)
│   │   ├── Ingest/
│   │   ├── Query/
│   │   ├── Lint/
│   │   ├── Index/                   # Fast Index 관리
│   │   ├── Agent/                   # GitHub Copilot SDK 래핑
│   │   └── MindAtlas.Engine.csproj
│   ├── MindAtlas.Server/            # ASP.NET Core 백엔드
│   │   ├── Controllers/
│   │   ├── Hubs/                    # SignalR 허브
│   │   ├── Mcp/                     # MCP 서버 구현
│   │   ├── Watchers/                # FileSystemWatcher
│   │   └── MindAtlas.Server.csproj
│   ├── MindAtlas.Web/               # Blazor WebAssembly 프론트엔드
│   │   ├── Pages/
│   │   ├── Components/
│   │   ├── Services/
│   │   └── MindAtlas.Web.csproj
│   └── MindAtlas.Desktop/           # Avalonia 데스크톱 셸
│       ├── Views/
│       ├── ViewModels/
│       ├── Services/                # 트레이, 단축키, D&D
│       └── MindAtlas.Desktop.csproj
├── tests/
│   ├── MindAtlas.Engine.Tests/
│   └── MindAtlas.Server.Tests/
├── schema/                          # 기본 Schema 템플릿
│   └── AGENTS.md
├── MindAtlas.sln
├── PRD.md
├── IMPLEMENTATION_CHECKLIST.md
└── AGENTS.md
```

---

## 7. 하이브리드 검색 전략

MindAtlas의 핵심 차별점은 **Fast Path + AI Path** 이중 동작이다.

### 7.1 Fast Path (정형 검색, O(ms))
1. `index.md`를 파싱하여 인메모리 인덱스 구축 (페이지명, 요약, 태그, 키워드)
2. 사용자 검색어를 키워드 매칭 (접두사/부분 매치)
3. `wiki/` 파일의 H1, wikilink 메타데이터로 보조 검색
4. **결과 즉시 반환** — UI 실시간 필터링

### 7.2 AI Path (비정형 처리, 수 초)
1. GitHub Copilot SDK Agent에 Schema(AGENTS.md) 주입
2. Ingest: raw 소스 분석 → 위키 페이지 생성/업데이트/교차참조
3. Query: 위키 컨텍스트 기반 자연어 답변 + 새 인사이트 기록
4. Lint: 위키 전체 일관성 검증

### 7.3 동작 흐름

```
사용자 입력
    ├─→ [키워드 감지] → Fast Path → 즉시 결과 표시
    └─→ [자연어/복잡 질문] → AI Path → Agent 처리 → 결과 표시 + 위키 갱신
```

---

## 8. MCP 서버 상세

### 8.1 제공 도구 (Tools)

```json
{
  "tools": [
    {
      "name": "mindatlas_ingest",
      "description": "텍스트 또는 파일을 MindAtlas 지식으로 저장합니다. raw/에 저장 후 LLM Wiki Ingest를 트리거합니다.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "content": { "type": "string", "description": "저장할 텍스트 내용" },
          "title": { "type": "string", "description": "제목 (파일명 생성에 사용)" },
          "tags": { "type": "array", "items": { "type": "string" }, "description": "태그 목록" }
        },
        "required": ["content"]
      }
    },
    {
      "name": "mindatlas_query",
      "description": "MindAtlas 위키에 자연어로 질문합니다.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "question": { "type": "string", "description": "질문 내용" }
        },
        "required": ["question"]
      }
    },
    {
      "name": "mindatlas_search",
      "description": "MindAtlas 위키를 키워드로 빠르게 검색합니다.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "keyword": { "type": "string", "description": "검색 키워드" },
          "limit": { "type": "integer", "description": "최대 결과 수", "default": 10 }
        },
        "required": ["keyword"]
      }
    },
    {
      "name": "mindatlas_get_asset",
      "description": "바이브코딩 자산(에이전트 설정, 룰, 스킬, 명령어, 정책)을 검색·조회합니다.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "assetType": { "type": "string", "enum": ["agent", "rule", "skill", "command", "policy"], "description": "자산 유형" },
          "query": { "type": "string", "description": "검색어" }
        },
        "required": ["query"]
      }
    },
    {
      "name": "mindatlas_lint",
      "description": "MindAtlas 위키 건강 검진을 실행합니다.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "scope": { "type": "string", "enum": ["full", "links", "orphans", "conflicts"], "description": "검진 범위", "default": "full" }
        }
      }
    }
  ]
}
```

### 8.2 전송 방식
- **stdio**: 로컬 프로세스 간 통신 (VS Code, Claude Desktop 등)
- **SSE (Server-Sent Events)**: HTTP 기반 원격 접속

---

## 9. 바이브코딩 자산 관리

MindAtlas가 LLM Wiki로 자동 문서화하는 **바이브코딩 자산** 유형:

| 자산 유형 | 설명 | 위키 태그 | 활용 예시 |
|-----------|------|-----------|-----------|
| **에이전트 설정** | .agent.md 파일 내용, 에이전트 역할·도구 정의 | `#agent-config` | VS Code Custom Agent 생성 시 참조 |
| **룰** | copilot-instructions.md, .instructions.md 규칙 | `#rule` | 프로젝트 코딩 규칙 일괄 적용 |
| **스킬** | SKILL.md, 도메인별 전문 워크플로우 | `#skill` | 테스트/성능/API 설계 스킬 재사용 |
| **명령어** | .prompt.md 슬래시 커맨드 | `#command` | 커스텀 프롬프트 라이브러리 |
| **정책** | AGENTS.md의 정책 항목 | `#policy` | 프로젝트 정책 빠른 복사 |

### 동작 흐름
1. 사용자가 에이전트 설정 파일 등을 raw/에 드롭 (또는 MCP로 전달)
2. Ingest Agent가 내용을 분석하여 자산 유형 자동 분류
3. 위키 페이지로 정제 (적절한 태그, 교차참조 포함)
4. MCP `mindatlas_get_asset` 도구로 필요 시 즉시 조회

---

## 10. 비기능 요구사항

| 항목 | 요구사항 |
|------|----------|
| **성능** | 키워드 검색 < 50ms, Ingest 완료 < 30s (일반 문서 기준) |
| **용량** | 위키 10,000페이지까지 인메모리 인덱스 유지 |
| **플랫폼** | Windows 10/11 (1차), macOS/Linux (2차) |
| **보안** | 로컬 전용 — 데이터가 사용자 머신 밖으로 나가지 않음 (LLM API 호출 제외) |
| **오프라인** | Fast Path 검색 및 위키 조회는 오프라인에서도 동작 |
| **확장성** | 플러그인/MCP 도구 추가로 기능 확장 가능 |

---

## 11. 구현 단계 (7단계)

> 상세 구현 계획은 `IMPLEMENTATION_PLAN.md`, 세부 태스크는 `IMPLEMENTATION_CHECKLIST.md` 참조.

### 1단계: 솔루션 스캐폴딩 및 Core 도메인
- 솔루션 스캐폴딩 (.NET 9, 7개 프로젝트 구조 생성)
- Core 도메인 모델 정의 (WikiPage, RawSource, IndexEntry 등)
- Core 인터페이스 정의 (IWikiEngine, IIndexService 등)
- schema/AGENTS.md 기본 템플릿
- **검증**: `dotnet build` 성공, 모든 프로젝트 참조 해결

### 2단계: LLM Wiki Engine 구현
- 파일 기반 WikiRepository, RawRepository
- LLM Wiki Engine — Ingest/Query/Lint 기본 파이프라인
- GitHub Copilot SDK Agent 연동 (GPT-5 mini, 0×승수)
- Fast Index (index.md 파싱, 인메모리 검색)
- FileSystemWatcher (raw/ 감지 → Ingest 트리거)
- **검증**: raw/에 파일 추가 시 wiki/ 페이지 자동 생성, `dotnet test` 통과

### 3단계: Backend API
- ASP.NET Core Web API 구축
- Wiki CRUD REST API (목록/조회/검색)
- SignalR Hub (실시간 위키 변경 알림)
- Ingest/Query/Lint API 엔드포인트 (SSE 스트리밍)
- 바이브코딩 자산 API
- **검증**: Swagger UI에서 API 호출, SignalR 실시간 알림 동작

### 4단계: MCP Server
- MCP 프로토콜 구현 (stdio + HTTP)
- 5개 도구: mindatlas_ingest / query / search / get_asset / lint
- VS Code MCP 설정 (.vscode/mcp.json)
- **검증**: VS Code Copilot에서 MCP 도구로 지식 저장·검색 성공

### 5단계: Frontend (Blazor WebAssembly)
- Blazor WASM 프로젝트 구성
- 위키 페이지 목록/조회 UI + 마크다운 렌더링 + `[[wikilinks]]`
- 키워드 검색 UI (실시간 필터링)
- AI Query 채팅 UI (SSE 스트리밍)
- 활동 로그 타임라인
- **검증**: 브라우저에서 위키 조회/검색/Query 정상 동작

### 6단계: Desktop Shell (Avalonia)
- Avalonia + WebView로 Blazor 래핑 (Self-hosted Kestrel)
- 시스템 트레이 상주 + 전역 단축키 (`Ctrl+Shift+Space`)
- 드래그 앤 드롭 (파일 → raw/) + 클립보드 붙여넣기
- 빠른 입력 창 + OS 시작 시 자동 실행
- **검증**: 단축키 토글, D&D → wiki 자동 생성, 트레이 최소화/종료

### 7단계: Polish & Integration
- Lint 대시보드, 지식 그래프 시각화
- 바이브코딩 자산 전용 뷰
- 설정 UI (단축키, 경로, 모델, 테마)
- 에러 핸들링, Serilog 로깅, 재시도 큐
- 배포 패키징 (Windows Installer)
- **검증**: E2E — D&D → Ingest → 검색 → Query → MCP 연동 통합 테스트
