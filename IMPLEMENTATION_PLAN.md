# MindAtlas 구현 계획

> 작성일: 2026-04-15
> 기반 문서: PRD.md, FEASIBILITY_REPORT.md, IMPLEMENTATION_CHECKLIST.md
> 이 문서는 개발자가 단계별로 따라갈 수 있는 **즉시 실행 가능한** 구현 계획이다.

---

## 구현 목표

Karpathy의 LLM Wiki 패턴(3-Layer, 3-Operation)을 기반으로 한 **개인 지식백과 데스크톱 앱**을 만든다.
raw 소스를 넣으면 AI Agent(GPT-5 mini, 0 PR)가 자동으로 지식을 정제·분류·연결하고, MCP로 외부 도구와 연동한다.

---

## 기술 스택

| 구성 요소 | 기술/라이브러리 | 버전/요구사항 | 비고 |
|-----------|----------------|--------------|------|
| 런타임 | .NET 9.0 | SDK 9.0+ | LTS |
| 백엔드 | ASP.NET Core | .NET 9 내장 | Web API + SignalR |
| AI Agent | GitHub.Copilot.SDK | 0.2.2 | NuGet, Public Preview |
| AI 모델 | GPT-5 mini | 0× 승수 | Copilot Pro($10/mo)에서 무제한 |
| MCP 서버 | ModelContextProtocol.AspNetCore | 1.2.0 | NuGet, stdio + HTTP |
| 프론트엔드 | Blazor WebAssembly | .NET 9 내장 | SPA |
| 데스크톱 셸 | Avalonia | 12.0.0 | 크로스플랫폼 |
| WebView | Avalonia.Controls.WebView | 12.0.0 | NativeWebView (WebView2) |
| 마크다운 | Markdig | 1.1.2 | 59M+ 다운로드 |
| 실시간 통신 | SignalR | .NET 9 내장 | 위키 변경 알림 |
| 파일 감시 | FileSystemWatcher | .NET 내장 | 디바운싱 직접 구현 |
| 테스트 | xUnit + FluentAssertions | 최신 | 단위/통합 테스트 |
| 로깅 | Serilog | 최신 | 구조화 로깅 (Phase 6) |

---

## 아키텍처 개요

```
┌─────────────────────────────────────────────────────────────────┐
│                    Avalonia Desktop Shell                        │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  NativeWebView (WebView2)                                 │  │
│  │  → http://localhost:5001                                  │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │        Blazor WebAssembly (SPA)                     │  │  │
│  │  │  WikiList | WikiPage | Search | AIQuery | Log       │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
│  TrayIcon | GlobalHotkey(Ctrl+Shift+Space) | DragDrop          │
└────────────────────────┬────────────────────────────────────────┘
                         │ HTTP / SignalR
┌────────────────────────┴────────────────────────────────────────┐
│              ASP.NET Core (self-hosted, :5001)                   │
│  ┌──────────────┐ ┌──────────────┐ ┌─────────────────────────┐ │
│  │ Wiki API     │ │ SignalR Hub  │ │ MCP Server (stdio+HTTP) │ │
│  │ REST CRUD    │ │ 실시간 알림  │ │ 5 Tools                 │ │
│  └──────┬───────┘ └──────┬───────┘ └────────────┬────────────┘ │
│         └────────────────┼──────────────────────┘              │
│                          │                                      │
│  ┌───────────────────────┴──────────────────────────────────┐  │
│  │            LLM Wiki Engine (Core)                         │  │
│  │  Ingest ← FileWatcher(raw/)                               │  │
│  │  Query  ← FastIndex(index.md) + CopilotAgent(GPT-5 mini) │  │
│  │  Lint   ← 고아/깨진링크/불일치 검출                        │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                         │
┌────────────────────────┴────────────────────────────────────────┐
│                 로컬 파일시스템 (~/.mindatlas/)                   │
│  raw/ (Layer 1)  │  wiki/ (Layer 2)  │  schema/ (Layer 3)      │
└─────────────────────────────────────────────────────────────────┘
```

**핵심 데이터 흐름:**
1. raw/에 파일 추가 → FileWatcher → IngestPipeline → CopilotAgent(GPT-5 mini) → wiki/ 생성 + index.md 갱신
2. 검색 요청 → FastIndex(키워드 O(ms)) 또는 CopilotAgent(자연어) → 결과 반환
3. MCP 호출 → 동일 Engine 사용 → 외부 도구 연동

---

## 구현 단계

### 1단계: 솔루션 스캐폴딩 및 Core 도메인

- **목표**: 솔루션 구조와 공유 도메인 모델/인터페이스를 생성한다
- **태스크**:

  1. **Avalonia 템플릿 설치**
     ```powershell
     dotnet new install Avalonia.Templates::12.0.1
     ```

  2. **솔루션 및 프로젝트 생성**
     ```powershell
     cd P:\MyWorks\MindAtlas
     dotnet new sln -n MindAtlas

     # Core — 공유 도메인 모델, 인터페이스
     dotnet new classlib -n MindAtlas.Core -o src/MindAtlas.Core -f net9.0
     
     # Engine — LLM Wiki 엔진 (Ingest/Query/Lint)
     dotnet new classlib -n MindAtlas.Engine -o src/MindAtlas.Engine -f net9.0
     
     # Server — ASP.NET Core 백엔드
     dotnet new web -n MindAtlas.Server -o src/MindAtlas.Server -f net9.0
     
     # Web — Blazor WASM
     dotnet new blazorwasm -n MindAtlas.Web -o src/MindAtlas.Web -f net9.0 --empty
     
     # Desktop — Avalonia 셸
     dotnet new avalonia.mvvm -n MindAtlas.Desktop -o src/MindAtlas.Desktop -f net9.0
     
     # Tests
     dotnet new xunit -n MindAtlas.Engine.Tests -o tests/MindAtlas.Engine.Tests -f net9.0
     dotnet new xunit -n MindAtlas.Server.Tests -o tests/MindAtlas.Server.Tests -f net9.0
     ```

  3. **솔루션에 프로젝트 추가 및 참조 설정**
     ```powershell
     dotnet sln add src/MindAtlas.Core src/MindAtlas.Engine src/MindAtlas.Server src/MindAtlas.Web src/MindAtlas.Desktop tests/MindAtlas.Engine.Tests tests/MindAtlas.Server.Tests
     
     # 참조 체인: Core ← Engine ← Server, Core ← Web, Engine ← Desktop
     dotnet add src/MindAtlas.Engine reference src/MindAtlas.Core
     dotnet add src/MindAtlas.Server reference src/MindAtlas.Engine
     dotnet add src/MindAtlas.Server reference src/MindAtlas.Core
     dotnet add src/MindAtlas.Web reference src/MindAtlas.Core
     dotnet add src/MindAtlas.Desktop reference src/MindAtlas.Engine
     dotnet add src/MindAtlas.Desktop reference src/MindAtlas.Server
     dotnet add tests/MindAtlas.Engine.Tests reference src/MindAtlas.Engine
     dotnet add tests/MindAtlas.Server.Tests reference src/MindAtlas.Server
     ```

  4. **Directory.Build.props 생성** (루트)
     ```xml
     <Project>
       <PropertyGroup>
         <TargetFramework>net9.0</TargetFramework>
         <Nullable>enable</Nullable>
         <ImplicitUsings>enable</ImplicitUsings>
         <LangVersion>latest</LangVersion>
       </PropertyGroup>
     </Project>
     ```

  5. **NuGet 패키지 설치** (FEASIBILITY_REPORT.md §8 기준)
     ```powershell
     # Core
     dotnet add src/MindAtlas.Core package Markdig --version 1.1.2
     
     # Engine
     dotnet add src/MindAtlas.Engine package GitHub.Copilot.SDK --version 0.2.2
     dotnet add src/MindAtlas.Engine package Markdig --version 1.1.2
     dotnet add src/MindAtlas.Engine package Microsoft.Extensions.Hosting.Abstractions
     
     # Server
     dotnet add src/MindAtlas.Server package ModelContextProtocol.AspNetCore --version 1.2.0
     
     # Desktop
     dotnet add src/MindAtlas.Desktop package Avalonia.Controls.WebView --version 12.0.0
     ```

  6. **Core 도메인 모델 생성** (src/MindAtlas.Core/Models/)
     - `WikiPage` — Title, Summary, Content, Tags, WikiLinks, CreatedAt, UpdatedAt
     - `RawSource` — FileName, FilePath, ContentType, AddedAt, ProcessingStatus(enum)
     - `IndexEntry` — PageName, Summary, Tags, Keywords
     - `LogEntry` — Timestamp, Operation(enum: Ingest/Query/Lint), Description, AffectedPages
     - `LintResult` — OrphanPages, BrokenLinks, MissingIndex, Conflicts
     - `QueryResult` — Answer, SourcePages, NewInsights
     - `VibeCodingAsset` — AssetType(enum), Name, Content, Tags

  7. **Core 인터페이스 생성** (src/MindAtlas.Core/Interfaces/)
     - `IWikiRepository` — GetAll, GetByName, Save, Delete, GetIndex, GetLog
     - `IRawRepository` — GetAll, GetByName, Save, GetUnprocessed
     - `IWikiEngine` — IngestAsync, QueryAsync, LintAsync
     - `IIndexService` — Search(keyword), Rebuild, GetAll
     - `ICopilotAgentService` — SendAsync, SendStreamingAsync, CreateSession

  8. **schema/AGENTS.md 기본 템플릿 생성**
     - Wiki 규칙: 페이지 형식, [[wikilink]] 규칙, 태그 규칙
     - Ingest 지침: raw → wiki 변환 규칙
     - Query 지침: 답변 형식, 인사이트 기록 규칙
     - Lint 지침: 검진 항목 목록

  9. **솔루션 빌드 확인**
     ```powershell
     dotnet build MindAtlas.sln
     ```

- **검증 기준**:
  - `dotnet build` 성공, 0 에러
  - 7개 프로젝트 모두 참조 해결 완료
  - Core 모델/인터페이스 컴파일 통과
- **예상 리스크**: Avalonia 12 템플릿이 .NET 9를 기본 지원하지 않을 수 있음 → TargetFramework 수동 변경

---

### 2단계: LLM Wiki Engine 구현

- **목표**: Ingest/Query/Lint 핵심 파이프라인과 Copilot SDK Agent 연동을 구현한다
- **태스크**:

  1. **파일 기반 WikiRepository 구현** (src/MindAtlas.Engine/)
     - wiki/ 디렉토리 CRUD (마크다운 파일 읽기/쓰기)
     - index.md 파싱/업데이트
     - log.md 추가(append-only)
     - RawRepository — raw/ 디렉토리 파일 관리

  2. **CopilotAgentService 구현** (src/MindAtlas.Engine/Agent/)
     ```csharp
     // FEASIBILITY_REPORT.md §1 코드 패턴 기반
     await using var client = new CopilotClient();
     await client.StartAsync();
     await using var session = await client.CreateSessionAsync(new SessionConfig
     {
         Model = "gpt-5-mini",  // 0× 승수
         Streaming = true,
         SystemMessage = new SystemMessageConfig
         {
             Mode = SystemMessageMode.Append,
             Content = schemaContent  // schema/AGENTS.md
         },
         Tools = [ingestTool, queryTool, lintTool]
     });
     ```
     - IDisposable/IAsyncDisposable 구현
     - 세션 풀링 또는 싱글 세션 재사용 전략
     - BYOK 폴백: `ProviderConfig`로 OpenAI 직접 연결 옵션

  3. **IngestPipeline 구현** (src/MindAtlas.Engine/Ingest/)
     - raw 파일 읽기 (텍스트: UTF-8 로드, 이미지: Copilot SDK Attachment)
     - Agent에 raw 내용 + 기존 wiki 컨텍스트 전달
     - Agent 응답 파싱 → wiki/ 마크다운 파일 생성/업데이트
     - `[[wikilinks]]` 교차참조 자동 삽입
     - index.md 항목 추가/갱신
     - log.md에 Ingest 기록 추가
     - 처리 상태 추적: Pending → Processing → Done/Failed

  4. **QueryEngine 구현** (src/MindAtlas.Engine/Query/)
     - **Fast Path**: IndexService.Search(keyword) → 즉시 반환
     - **AI Path**: Agent에 위키 컨텍스트 + 질문 전달 → 스트리밍 응답
     - 새 인사이트 추출 → wiki 갱신 판단
     - log.md에 Query 기록 추가

  5. **LintEngine 구현** (src/MindAtlas.Engine/Lint/)
     - 고아 페이지 탐지: wiki/ 전체 스캔 → [[link]]가 없는 페이지
     - 깨진 wikilink 탐지: [[link]] 대상 파일 존재 확인
     - index.md 불일치: 등록 안 된 페이지, 삭제된 페이지 항목
     - `LintResult` 구조화 반환

  6. **IndexService 구현** (src/MindAtlas.Engine/Index/)
     - index.md 파싱 → `List<IndexEntry>` 인메모리 캐시
     - wiki/ 파일 H1, 태그 메타데이터 추출 보조
     - 키워드 검색: 접두사/부분 매치, Dictionary 기반
     - 파일 변경 시 자동 리로드 (FileSystemWatcher on wiki/)

  7. **RawDirectoryWatcher 구현** (src/MindAtlas.Engine/)
     ```csharp
     // FEASIBILITY_REPORT.md §6 코드 패턴 기반
     // FileSystemWatcher + Channel<string> + 500ms 디바운싱
     ```
     - raw/ Created 이벤트 감시
     - 디바운싱 (500ms 내 중복 이벤트 병합)
     - IngestPipeline 자동 트리거
     - 처리 상태를 Channel로 관리

  8. **단위 테스트 작성** (tests/MindAtlas.Engine.Tests/)
     - IndexService: 키워드 검색 정확도 + < 50ms 성능
     - LintEngine: 고아/깨진링크 탐지 정확도
     - WikiRepository: 파일 CRUD 동작
     - IngestPipeline: raw → wiki 변환 (Agent mocking)

- **검증 기준**:
  - `dotnet test` 전체 통과
  - raw/에 텍스트 파일 추가 → FileWatcher 감지 → Ingest → wiki/ 마크다운 생성 (통합 테스트)
  - IndexService 키워드 검색 < 50ms (1,000 항목 기준)
  - LintEngine 고아/깨진링크 정확히 탐지
- **예상 리스크**: Copilot SDK Public Preview 불안정 → IWikiEngine 추상화로 Agent 구현 교체 가능하게 설계

---

### 3단계: Backend API

- **목표**: ASP.NET Core Web API + SignalR Hub을 구축하여 프론트엔드/MCP에 서비스를 제공한다
- **태스크**:

  1. **Program.cs DI 구성**
     - Engine, Repository, IndexService, AgentService 등록
     - CORS 설정 (localhost:5001 허용)
     - Swagger/OpenAPI 설정

  2. **Wiki REST API** (src/MindAtlas.Server/Controllers/WikiController.cs)
     - `GET /api/wiki` — 전체 페이지 목록 (IndexService 기반)
     - `GET /api/wiki/{pageName}` — 특정 페이지 조회 (마크다운 원문 + HTML)
     - `GET /api/wiki/search?q={keyword}` — 키워드 검색
     - `GET /api/wiki/log` — 활동 로그 조회
     - `GET /api/wiki/tags` — 태그 목록

  3. **Ingest/Query/Lint API**
     - `POST /api/ingest` — 텍스트/파일 업로드 → raw/ 저장 + Ingest 트리거
     - `POST /api/query` — 자연어 질문 → SSE 스트리밍 응답
     - `POST /api/lint` — Lint 실행 → 결과 JSON
     - `GET /api/ingest/status` — 현재 Ingest 대기열 상태

  4. **바이브코딩 자산 API**
     - `GET /api/assets?type={agent|rule|skill|command|policy}` — 자산 목록
     - `GET /api/assets/{id}` — 자산 조회
     - `GET /api/assets/search?q={query}` — 자산 검색

  5. **SignalR Hub** (src/MindAtlas.Server/Hubs/WikiHub.cs)
     - `OnWikiUpdated` — 위키 페이지 변경 알림 (Ingest 완료 시)
     - `OnIngestStarted` / `OnIngestCompleted` — Ingest 진행 상태
     - `OnLogAppended` — 새 로그 항목

  6. **서버 단독 실행 확인**
     ```powershell
     dotnet run --project src/MindAtlas.Server
     # → http://localhost:5001/swagger 접근
     ```

- **검증 기준**:
  - Swagger UI에서 모든 API 엔드포인트 호출 성공
  - `POST /api/ingest` → raw/ 저장 → wiki/ 생성 확인
  - `POST /api/query` → SSE 스트리밍 응답 수신
  - SignalR 연결 및 실시간 알림 동작
- **예상 리스크**: SSE 스트리밍 + Copilot SDK 스트리밍 병합 복잡도 → Agent 이벤트를 IAsyncEnumerable로 래핑

---

### 4단계: MCP Server

- **목표**: MCP 프로토콜(stdio + HTTP)로 5개 도구를 외부에 제공한다
- **태스크**:

  1. **MCP 도구 클래스 정의** (src/MindAtlas.Server/Mcp/MindAtlasTools.cs)
     ```csharp
     // FEASIBILITY_REPORT.md §2 코드 패턴 기반
     [McpServerToolType]
     public class MindAtlasTools
     {
         [McpServerTool(Name = "mindatlas_ingest")]
         [Description("텍스트를 MindAtlas 지식으로 저장합니다")]
         public async Task<string> Ingest(
             [Description("저장할 텍스트")] string content,
             [Description("제목")] string? title = null) { ... }
         
         [McpServerTool(Name = "mindatlas_search")]
         [McpServerTool(Name = "mindatlas_query")]
         [McpServerTool(Name = "mindatlas_get_asset")]
         [McpServerTool(Name = "mindatlas_lint")]
     }
     ```

  2. **HTTP MCP 엔드포인트 통합** (ASP.NET Core 앱에 추가)
     ```csharp
     builder.Services.AddMcpServer().WithTools<MindAtlasTools>();
     app.MapMcp();  // → /mcp 엔드포인트
     ```

  3. **stdio MCP CLI 엔트리포인트** (별도 실행 모드)
     - `dotnet run --project src/MindAtlas.Server -- --mcp-stdio` 모드 감지
     - `WithStdioServerTransport()` 전환
     - VS Code mcp.json 설정 파일 작성

  4. **VS Code MCP 설정 파일 생성** (.vscode/mcp.json)
     ```json
     {
       "servers": {
         "mindatlas": {
           "type": "stdio",
           "command": "dotnet",
           "args": ["run", "--project", "src/MindAtlas.Server", "--", "--mcp-stdio"]
         }
       }
     }
     ```

  5. **MCP 도구별 통합 테스트**
     - mindatlas_ingest → raw 저장 → wiki 생성
     - mindatlas_search → 키워드 검색 결과
     - mindatlas_query → AI 답변
     - mindatlas_get_asset → 자산 조회
     - mindatlas_lint → Lint 결과

- **검증 기준**:
  - MCP Inspector(`npx @modelcontextprotocol/inspector`)로 stdio 모드 연결 테스트
  - VS Code에서 MCP 서버 연결 → `mindatlas_ingest` 도구 호출 성공
  - VS Code Copilot Chat에서 `mindatlas_search`로 위키 검색 성공
- **예상 리스크**: stdio와 HTTP 모드 동시 지원 시 Program.cs 분기 복잡도 → 커맨드라인 인자로 모드 선택

---

### 5단계: Frontend (Blazor WebAssembly)

- **목표**: 위키 뷰어, 검색, AI Query 대화 UI를 Blazor WASM으로 구현한다
- **태스크**:

  1. **프로젝트 구성**
     - HttpClient DI 등록 (base: `http://localhost:5001`)
     - SignalR HubConnection DI 등록
     - 라우팅: `/` (목록), `/wiki/{pageName}` (조회), `/search`, `/query`, `/log`
     - 공통 레이아웃: 사이드바(네비게이션) + 헤더(검색바) + 메인 콘텐츠

  2. **위키 목록/조회 페이지**
     - `WikiList.razor` — `GET /api/wiki` → 카드/리스트 뷰, 태그 필터
     - `WikiPage.razor` — `GET /api/wiki/{name}` → 마크다운 HTML 렌더링
     - `[[wikilinks]]` → `<a href="/wiki/{name}">` 변환 (서버 사이드 Markdig 또는 클라이언트)
     - Breadcrumb 네비게이션

  3. **검색 UI**
     - 헤더 상단 검색바 (모든 페이지에 고정)
     - 타이핑 시 디바운싱(300ms) → `GET /api/wiki/search?q=` 호출
     - 검색 결과 드롭다운/페이지 (키워드 하이라이트)

  4. **AI Query UI**
     - 채팅형 인터페이스 (메시지 목록 + 입력창)
     - `POST /api/query` → SSE 스트리밍 수신 → 청크별 실시간 렌더링
     - 답변 내 `[[wikilinks]]` 클릭 → 위키 페이지 이동
     - Query 히스토리 (세션 유지)

  5. **활동 로그**
     - `GET /api/wiki/log` → 타임라인 UI
     - Ingest/Query/Lint 구분 아이콘/색상
     - SignalR `OnLogAppended` → 실시간 새 항목 추가

  6. **Blazor WASM 단독 테스트**
     ```powershell
     # Server 실행
     dotnet run --project src/MindAtlas.Server
     # 별도 터미널에서 Web 실행
     dotnet run --project src/MindAtlas.Web
     # → 브라우저에서 확인
     ```

- **검증 기준**:
  - 브라우저에서 위키 목록 → 페이지 조회 → wikilink 네비게이션 동작
  - 키워드 검색 입력 → < 100ms 응답 → 결과 표시
  - AI Query 채팅 → 스트리밍 응답 실시간 표시
  - SignalR 실시간 알림 동작 (Ingest 완료 시 로그 자동 갱신)
- **예상 리스크**: Blazor WASM 초기 로딩 크기 → AOT 미적용 시 ~5MB → 로컬 앱이므로 허용 범위

---

### 6단계: Desktop Shell (Avalonia)

- **목표**: Avalonia + WebView로 Blazor를 래핑하고, 트레이/단축키/D&D를 구현한다
- **태스크**:

  1. **Self-hosted Kestrel 통합**
     - MindAtlas.Desktop에서 ASP.NET Core 서버를 백그라운드로 시작
     - 서버 Health Check 후 WebView Source 설정
     ```csharp
     // FEASIBILITY_REPORT.md §3 코드 패턴 기반
     _host = Host.CreateDefaultBuilder()
         .ConfigureWebHostDefaults(web => {
             web.UseStartup<Startup>();
             web.UseUrls("http://localhost:5001");
         }).Build();
     _ = _host.StartAsync();
     // Health check 후
     WebView.Source = "http://localhost:5001";
     ```

  2. **NativeWebView 설정** (Views/MainWindow.axaml)
     ```xml
     <web:NativeWebView x:Name="WebView" Source="http://localhost:5001" />
     ```

  3. **시스템 트레이** (App.axaml)
     ```xml
     <!-- FEASIBILITY_REPORT.md §3-2 패턴 -->
     <TrayIcon Icon="/Assets/mindatlas.ico" ToolTipText="MindAtlas">
       <TrayIcon.Menu>
         <NativeMenu>
           <NativeMenuItem Header="열기" />
           <NativeMenuItem Header="설정" />
           <NativeMenuItem Header="종료" />
         </NativeMenu>
       </TrayIcon.Menu>
     </TrayIcon>
     ```
     - 창 닫기 → 트레이 최소화 (종료 아님)
     - 트레이 더블클릭 → 창 복원

  4. **전역 단축키** (Windows P/Invoke)
     ```csharp
     // FEASIBILITY_REPORT.md §3-3 패턴
     // RegisterHotKey/UnregisterHotKey — Ctrl+Shift+Space
     ```
     - 단축키로 창 토글 (표시 ↔ 숨김)

  5. **드래그 앤 드롭**
     ```csharp
     // FEASIBILITY_REPORT.md §3-4 패턴
     // DragDrop.Drop 이벤트 → 파일 → raw/ 복사 → Ingest 트리거
     ```
     - 타임스탬프 접두사로 파일명 충돌 방지
     - 지원: .md, .txt, .pdf, .png, .jpg, .jpeg, .gif, .webp

  6. **클립보드 입력**
     - `Ctrl+V` → 텍스트: `raw/{timestamp}_clipboard.md`
     - `Ctrl+V` → 이미지: `raw/{timestamp}_clipboard.png`
     - 붙여넣기 후 Ingest 자동 트리거

  7. **빠른 입력 창**
     - 텍스트 입력 + "저장" 버튼
     - Enter로 빠른 저장 → raw/ → Ingest

  8. **OS 시작 시 자동 실행** (선택)
     - Windows Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

- **검증 기준**:
  - 앱 시작 → 트레이 아이콘 표시
  - `Ctrl+Shift+Space` → 앱 창 토글
  - 파일 D&D → raw/ 복사 → wiki/ 자동 생성
  - 클립보드 텍스트 붙여넣기 → wiki 자동 생성
  - 앱 종료(X) → 트레이 최소화, 트레이 "종료" → 실제 종료
- **예상 리스크**: Avalonia.Controls.WebView v12.0.0 신규 → Self-hosted 서버 + WebView 초기화 순서 이슈 → Health Check 재시도 로직

---

### 7단계: Polish & Integration

- **목표**: 고급 UI, 에러 핸들링, 배포 패키징으로 완성도를 높인다
- **태스크**:

  1. **Lint 대시보드** — 카테고리별 결과 카드, 자동 수정 제안
  2. **지식 그래프** — [[wikilinks]] 기반 노드-엣지 시각화 (D3.js via JS interop)
  3. **바이브코딩 자산 뷰** — 유형별 필터, 미리보기, 클립보드 복사, 파일 내보내기
  4. **설정 UI** — 데이터 경로, 단축키, LLM 모델, 테마(라이트/다크)
  5. **에러 핸들링** — 전역 예외 핸들러, Serilog 로깅, Ingest 재시도 큐
  6. **배포 패키징** — self-contained publish, Windows MSIX 또는 InnoSetup

- **검증 기준**:
  - E2E: 앱 시작 → D&D → Ingest → 검색 → Query → MCP 연동
  - E2E: MCP ingest → wiki 생성 → MCP search 확인
  - 10,000 페이지 인덱스 성능 < 50ms
  - Windows Installer 설치/제거 정상
- **예상 리스크**: D3.js Blazor interop 복잡도 → 라이브러리(Blazor.Diagrams 등) 검토

---

## 의존성 및 사전 준비

| 항목 | 요구사항 | 확인 방법 |
|------|----------|-----------|
| .NET 9 SDK | 9.0+ | `dotnet --version` |
| GitHub Copilot CLI | 최신 | `copilot --version` |
| GitHub Copilot 구독 | Pro($10/mo) 이상 권장 | GitHub Settings → Copilot |
| Node.js | 18+ (MCP Inspector용) | `node --version` |
| WebView2 Runtime | Windows 10/11 내장 | Edge 기반 자동 포함 |
| Git | 최신 | `git --version` |
| Avalonia 템플릿 | 12.0.1 | `dotnet new install Avalonia.Templates::12.0.1` |

---

## 리스크 맵

| 리스크 | 영향도 | 발생 확률 | 대응 전략 |
|--------|--------|----------|----------|
| Copilot SDK Breaking Change | 높음 | 중간 | IWikiEngine 추상화 레이어, BYOK 폴백, SDK 버전 고정 |
| Avalonia WebView 초기화 실패 | 높음 | 낮음 | Health Check 재시도, CheapAvaloniaBlazor 대안, 순수 Avalonia UI 대안 |
| GPT-5 mini 0-승수 정책 변경 | 중간 | 낮음 | BYOK 옵션 유지, GPT-4.1(동일 0×) 자동 전환 |
| FileSystemWatcher 다중 이벤트 | 낮음 | 높음 | Channel + 500ms 디바운싱 패턴 적용 (FEASIBILITY_REPORT.md §6) |
| Blazor WASM 초기 로딩 크기 | 낮음 | 중간 | 로컬 앱이므로 허용, 필요 시 Lazy Loading 적용 |
| MCP stdio/HTTP 동시 지원 | 낮음 | 중간 | 커맨드라인 인자로 모드 분기, 별도 엔트리포인트 |
| SSE 스트리밍 + Agent 스트리밍 병합 | 중간 | 중간 | IAsyncEnumerable로 래핑, 통합 테스트 필수 |

---

## 참고 자료

| 리소스 | URL |
|--------|-----|
| FEASIBILITY_REPORT.md | 로컬 — 패키지명, 버전, API 패턴, 코드 샘플 |
| IMPLEMENTATION_CHECKLIST.md | 로컬 — Phase별 세부 태스크 체크리스트 |
| PRD.md | 로컬 — 전체 요구사항, 아키텍처, 기능 명세 |
| Copilot SDK Getting Started | https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md |
| Copilot SDK .NET Reference | https://github.com/github/copilot-sdk/blob/main/dotnet/README.md |
| MCP C# SDK 문서 | https://csharp.sdk.modelcontextprotocol.io/ |
| Avalonia Getting Started | https://docs.avaloniaui.net/docs/get-started/getting-started |
| Avalonia WebView QuickStart | https://docs.avaloniaui.net/accelerate/components/webview/quickstart |
| Markdig GitHub | https://github.com/xoofx/markdig |
| Copilot Premium Requests | https://docs.github.com/en/copilot/concepts/billing/copilot-requests |
| OpenAI GPT-5 mini | https://developers.openai.com/api/docs/models/gpt-5-mini |
| Karpathy LLM Wiki | https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f |
