# MindAtlas

[English README](README.md) · [버전 히스토리](docs/version-history.ko.md) · [라이선스](LICENSE)

MindAtlas는 raw note, 리서치, 대화, 프로젝트 지식을 영구적인 위키 페이지, 위키링크, 검색 인덱스, 관계 그래프로 변환하는 local-first **LLM Wiki**입니다.

원본 raw source를 진실의 원천으로 유지하고, 그 위에 사람이 읽고 AI 에이전트가 사용할 수 있는 generated wiki layer를 구축·유지합니다.

## 주요 기능

- **LLM Wiki ingest** — Markdown/text 원문을 지속 가능한 위키 지식 페이지로 변환
- **Source-aware knowledge maintenance** — 생성 페이지에 source, confidence, stale, contradiction, supersession 신호 반영 가능
- **Obsidian-style wikilinks** — alias, heading/path normalization, typed relationship marker 지원
- **Typed relationship graph** — `@supports`, `@contradicts`, `@supersedes`, `@depends_on` 등 관계를 색상, 라벨, 범례, 필터로 시각화
- **Relationship retag workflow** — 관계 타입 제안을 검토하고 개별 선택 후 확인 모달을 거쳐 안전 적용
- **Lint and repair** — 고아 페이지, 깨진 링크, 누락 인덱스를 감지하고 안전한 위키링크 문제 수리
- **MCP integration** — ingest, search, query, lint, asset lookup, relationship retag 도구를 MCP 호환 AI 클라이언트에 제공
- **Desktop + Web** — Avalonia desktop shell, ASP.NET Core server, Blazor WebAssembly UI 구성

## 아키텍처

```text
Desktop (Avalonia)        Web UI (Blazor WASM)        MCP clients
        │                         │                       │
        └──────────────┬──────────┴──────────────┬────────┘
                       ▼                         ▼
              MindAtlas.Server (ASP.NET Core, REST, SignalR, MCP)
                       │
                       ▼
              MindAtlas.Engine (Ingest, Query, Lint, Maintenance)
                       │
                       ▼
              MindAtlas.Core (Models and Interfaces)
```

## 요구사항

- 기본 개발 환경: Windows
- .NET SDK 10 이상
- Copilot 기반 ingest/query 흐름에는 app settings에 설정하지 않은 경우 GitHub token 필요

## 빠른 시작

데스크톱 앱 실행:

```powershell
./run.ps1
```

서버/웹 앱 직접 실행:

```powershell
dotnet run --project src/MindAtlas.Server
```

브라우저에서 열기:

```text
http://localhost:5001
```

테스트 실행:

```powershell
dotnet test MindAtlas.slnx
```

전체 빌드:

```powershell
dotnet build MindAtlas.slnx
```

## 설정

MindAtlas는 server settings, environment variables, runtime settings를 통해 설정을 읽습니다.

주요 로컬 설정:

- `MindAtlas:DataRoot` — 데이터 디렉터리, 기본값 `./data`
- `MindAtlas:GitHubToken` 또는 `GITHUB_TOKEN` — Copilot 기반 작업용 token
- `MindAtlas:UiLanguage` — UI 언어
- `MindAtlas:Theme` — `auto`, light, dark theme 설정

개발 편의를 위해 `.env.example` 파일을 제공합니다. 로컬 secret은 `.env`로 복사해 사용하세요. 단, `.env` 파일은 Git에서 제외되며 필요하면 shell/environment에 명시적으로 로드해야 합니다.

## MCP 사용

MindAtlas는 MCP stdio server로 실행할 수 있습니다.

```powershell
dotnet run --project src/MindAtlas.Server -- --mcp-stdio
```

제공 MCP 기능:

- 텍스트 ingest
- 키워드 검색
- 자연어 위키 질의
- vibe-coding asset 조회
- lint health check
- relationship retag proposal 생성
- relationship retag 적용

## 저장소 구조

```text
src/MindAtlas.Core      공유 모델 및 인터페이스
src/MindAtlas.Engine    Ingest, query, lint, repository, maintenance logic
src/MindAtlas.Server    ASP.NET Core REST/SignalR/MCP host
src/MindAtlas.Web       Blazor WebAssembly UI
src/MindAtlas.Desktop   Avalonia desktop shell
tests/                  xUnit test projects
docs/                   문서 및 버전 히스토리
```

## LLM Wiki workflow

1. immutable raw source text 저장
2. raw source 기반 위키 페이지 생성 또는 갱신
3. 위키링크 정규화 및 유지보수
4. lint로 깨진 링크, 고아 페이지, 인덱스 문제 확인
5. relationship retag proposal 검토
6. typed graph로 지식 관계 시각화
7. Web UI, Desktop app, MCP client에서 위키 질의

## 라이선스

MindAtlas는 [Apache License 2.0](LICENSE)으로 배포됩니다.
