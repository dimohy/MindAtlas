# MindAtlas 구현 체크리스트

> `IMPLEMENTATION_PLAN.md`의 7단계 구현 순서에 따른 세부 태스크 목록.
> `/implement` 프롬프트로 단계별 구현 시 이 체크리스트를 따른다.
> 
> **⚠️ 구현 전 필독**:
> - `IMPLEMENTATION_PLAN.md` — 단계별 구현 계획, 구체적 커맨드, 검증 기준, 리스크 맵
> - `FEASIBILITY_REPORT.md` — 각 기술의 정확한 패키지명, 버전, API 패턴, 코드 샘플 (§8 NuGet 목록)

---

## 1단계: 솔루션 스캐폴딩 및 Core 도메인

### 1.1 솔루션 스캐폴딩
- [x] `MindAtlas.slnx` 솔루션 생성 (.NET 9 slnx 형식)
- [x] `src/MindAtlas.Core/` 클래스 라이브러리 프로젝트 생성
- [x] `src/MindAtlas.Engine/` 클래스 라이브러리 프로젝트 생성
- [x] `src/MindAtlas.Server/` ASP.NET Core 프로젝트 템플릿 생성
- [x] `src/MindAtlas.Web/` Blazor WASM 프로젝트 템플릿 생성
- [x] `src/MindAtlas.Desktop/` Avalonia 프로젝트 템플릿 생성
- [x] `tests/MindAtlas.Engine.Tests/` xUnit 프로젝트 생성
- [x] `tests/MindAtlas.Server.Tests/` xUnit 프로젝트 생성
- [x] 프로젝트 간 참조 설정 (Core → Engine → Server, Core → Web 등)
- [x] `schema/AGENTS.md` 기본 Schema 템플릿 생성
- [x] `.gitignore`, `Directory.Build.props` 등 공통 설정

### 1.2 Core 도메인 모델
- [x] `WikiPage` 모델 (Title, Summary, Content, Tags, WikiLinks, CreatedAt, UpdatedAt)
- [x] `RawSource` 모델 (FileName, FilePath, ContentType, AddedAt, Status)
- [x] `IndexEntry` 모델 (PageName, Summary, Tags, Keywords)
- [x] `LogEntry` 모델 (Timestamp, Operation, Description, AffectedPages)
- [x] `LintResult` 모델 (OrphanPages, BrokenLinks, Conflicts, MissingIndex)
- [x] `QueryResult` 모델 (Answer, SourcePages, NewInsights)
- [x] `VibeCodingAsset` 모델 (AssetType enum, Name, Content, Tags)
- [x] `IWikiRepository` 인터페이스 (위키 CRUD)
- [x] `IRawRepository` 인터페이스 (raw 파일 관리)
- [x] `IWikiEngine` 인터페이스 (Ingest/Query/Lint)
- [x] `IIndexService` 인터페이스 (빠른 검색)

### 1.3 1단계 검증
- [x] `dotnet build MindAtlas.slnx` 성공, 0 에러
- [x] 7개 프로젝트 모두 참조 해결 완료
- [x] Core 모델/인터페이스 컴파일 통과

---

## 2단계: LLM Wiki Engine 구현

### 2.1 파일 기반 Repository
- [x] `WikiRepository` 구현 — wiki/ 디렉토리 CRUD (마크다운 읽기/쓰기)
- [x] `RawRepository` 구현 — raw/ 디렉토리 파일 관리
- [x] index.md 파싱/업데이트 로직
- [x] log.md append-only 기록 로직

### 2.2 LLM Wiki Engine — Ingest
- [x] `IngestPipeline` 클래스 구현
  - [x] raw 파일 읽기 (텍스트/마크다운)
  - [x] GitHub Copilot SDK Agent로 내용 분석 및 위키 페이지 생성 요청
  - [x] Agent 응답 파싱 → wiki/ 파일 생성/업데이트
  - [x] `[[wikilinks]]` 교차참조 자동 삽입
  - [x] `index.md` 업데이트
  - [x] `log.md`에 Ingest 활동 기록
- [x] 이미지/바이너리 파일 처리 (메타데이터만 위키에 기록)
- [x] 기존 위키 페이지와 병합 로직

### 2.3 LLM Wiki Engine — Query
- [x] `QueryEngine` 클래스 구현
  - [x] Fast Path: index.md 키워드 매칭
  - [x] AI Path: Agent에 위키 컨텍스트 + 질문 전달
  - [x] 답변에서 새 인사이트 추출 → 위키 갱신
  - [x] `log.md`에 Query 활동 기록

### 2.4 LLM Wiki Engine — Lint
- [x] `LintEngine` 클래스 구현
  - [x] 고아 페이지 탐지 (아무데서도 `[[link]]` 안 되는 페이지)
  - [x] 깨진 wikilink 탐지 (존재하지 않는 페이지 링크)
  - [x] `index.md` 불일치 탐지 (등록 안 된 페이지, 삭제된 페이지)
  - [x] 구조화된 `LintResult` 반환

### 2.5 GitHub Copilot SDK Agent 연동
- [x] Copilot SDK NuGet 패키지 추가
- [x] `CopilotAgentService` 클래스 — Agent 생성/관리
- [x] Schema(AGENTS.md) 내용을 Agent 시스템 프롬프트에 주입
- [x] wiki/ 컨텍스트를 Agent에 전달하는 전략 구현
- [x] Agent 응답 스트리밍 처리

### 2.6 Fast Index
- [x] `IndexService` 클래스 구현
  - [x] `index.md` 파싱 → `IndexEntry` 목록
  - [x] wiki/ 파일 H1, wikilink 메타데이터 추출
  - [x] 인메모리 인덱스 구축 (Dictionary/Trie 기반)
  - [x] 키워드 검색 (접두사/부분 매치, < 50ms)
  - [x] 파일 변경 시 인덱스 자동 리로드

### 2.7 FileSystemWatcher
- [x] `RawDirectoryWatcher` 클래스 구현
  - [x] `raw/` 디렉토리 감시 (Created, Changed 이벤트)
  - [x] 디바운싱 (짧은 시간 내 다수 이벤트 병합)
  - [x] Ingest 파이프라인 트리거
  - [x] 처리 상태 추적 (대기→처리중→완료/실패)

### 2.8 2단계 검증
- [x] 단위 테스트: IndexService 키워드 검색 < 50ms (1,000 항목)
- [x] 단위 테스트: LintEngine 고아/깨진링크 탐지
- [x] 단위 테스트: WikiRepository 파일 CRUD
- [x] 단위 테스트: IngestPipeline 응답 파싱 (마커/폴백/위키링크/태그)
- [ ] 통합 테스트: raw/에 파일 추가 → wiki/ 페이지 자동 생성 (3단계 이후 실행)
- [ ] 통합 테스트: Query 엔진 Fast Path + AI Path (3단계 이후 실행)
- [x] `dotnet test` 전체 통과 — 24 tests, 0 failures

---

## 3단계: Backend API

### 3.1 ASP.NET Core 기본 구성
- [x] Program.cs DI 설정 (Engine, Repository, Index 등 서비스 등록)
- [x] appsettings.json 구성 (데이터 경로, Copilot SDK 설정)
- [x] CORS 설정 (Blazor WASM 허용)
- [x] OpenAPI 설정 (MapOpenApi)

### 3.2 Wiki REST API
- [x] `GET /api/wiki` — 전체 페이지 목록 (index.md 기반)
- [x] `GET /api/wiki/{pageName}` — 특정 위키 페이지 조회
- [x] `GET /api/wiki/search?q={keyword}` — 키워드 검색
- [x] `GET /api/wiki/log` — 활동 로그 조회
- [x] `GET /api/wiki/tags` — 태그 목록

### 3.3 Ingest/Query/Lint API
- [x] `POST /api/ingest` — 텍스트/파일 업로드 → raw/ 저장 + Ingest 트리거
- [x] `POST /api/query` — 자연어 질문 → 답변 스트리밍 (SSE)
- [x] `POST /api/lint` — Lint 실행 → 결과 반환
- [x] `GET /api/ingest/status` — 현재 Ingest 대기열 상태

### 3.4 SignalR Hub
- [x] `WikiHub` 구현
  - [x] `OnWikiUpdated` — 위키 페이지 변경 알림
  - [x] `OnIngestStarted` / `OnIngestCompleted` — Ingest 진행 상태
  - [x] `OnLogAppended` — 새 로그 항목 알림

### 3.5 바이브코딩 자산 API
- [x] `GET /api/assets?type={agent|rule|skill|command|policy}` — 자산 목록
- [x] `GET /api/assets/{id}` — 특정 자산 조회
- [x] `GET /api/assets/search?q={query}` — 자산 검색

### 3.6 3단계 검증
- [x] OpenAPI 엔드포인트 등록 확인 (`/openapi/v1.json`)
- [ ] `POST /api/ingest` → raw/ 저장 → wiki/ 생성 확인 (Copilot SDK 필요)
- [ ] `POST /api/query` → SSE 스트리밍 응답 수신 (Copilot SDK 필요)
- [ ] SignalR 연결 및 실시간 알림 테스트 (5단계 이후 실행)
- [x] `dotnet build` 7개 프로젝트 성공, `dotnet test` 25 tests 통과

---

## 4단계: MCP Server

### 4.1 MCP 프로토콜 기반
- [x] MCP JSON-RPC 메시지 핸들러 구현 (ModelContextProtocol SDK 사용)
- [x] `initialize` / `initialized` 핸드셰이크 (SDK 내장)
- [x] `tools/list` — 사용 가능한 도구 목록 반환 (SDK 내장)
- [x] `tools/call` — 도구 실행 디스패쳐 (SDK 내장)

### 4.2 stdio 전송
- [x] stdin/stdout 기반 JSON-RPC 리더/라이터 (WithStdioServerTransport)
- [x] MCP 서버 프로세스 엔트리포인트 (`--mcp-stdio` CLI 인자)

### 4.3 SSE 전송
- [x] ASP.NET Core HTTP MCP 엔드포인트 (`/mcp`, MapMcp)
- [x] Streamable HTTP 전송 (ModelContextProtocol.AspNetCore 내장)
- [x] 세션 관리 (SDK 내장)

### 4.4 MCP 도구 구현
- [x] `mindatlas_ingest` — 텍스트 → raw/ 저장 + Ingest
- [x] `mindatlas_query` — 자연어 질문 → 답변
- [x] `mindatlas_search` — 키워드 빠른 검색
- [x] `mindatlas_get_asset` — 바이브코딩 자산 조회
- [x] `mindatlas_lint` — Lint 실행

### 4.5 4단계 검증
- [ ] MCP Inspector(`npx @modelcontextprotocol/inspector`)로 stdio 모드 연결 테스트 (Copilot SDK 필요)
- [ ] VS Code MCP 설정 → mindatlas_ingest 도구 호출 성공 (Copilot SDK 필요)
- [ ] VS Code Copilot Chat에서 mindatlas_search로 위키 검색 성공 (Copilot SDK 필요)
- [ ] mindatlas_get_asset으로 에이전트 설정 조회 성공 (Copilot SDK 필요)
- [x] `dotnet build` 7개 프로젝트 성공, `dotnet test` 25 tests 통과
- [x] `.vscode/mcp.json` 설정 파일 생성

---

## 5단계: Frontend (Blazor WebAssembly)

### 5.1 프로젝트 구성
- [ ] Blazor WASM Standalone 프로젝트 초기 설정
- [ ] HttpClient / SignalR 클라이언트 DI 등록
- [ ] 라우팅 설정
- [ ] 공통 레이아웃 (사이드바 + 메인 콘텐츠)

### 5.2 위키 목록/조회
- [ ] `WikiList` 페이지 — index.md 기반 카드/리스트 뷰
- [ ] `WikiPage` 페이지 — 마크다운 렌더링 (Markdig WASM 또는 JS interop)
- [ ] `[[wikilinks]]` 클릭 → 내부 네비게이션
- [ ] Breadcrumb 네비게이션

### 5.3 검색
- [ ] 검색 바 (헤더 상단 고정)
- [ ] 실시간 키워드 필터링 (타이핑 시 즉시 결과)
- [ ] 검색 결과 하이라이트

### 5.4 AI Query
- [ ] Query 입력 UI (채팅형 또는 검색창 통합)
- [ ] Agent 응답 스트리밍 표시 (SSE)
- [ ] 답변 내 위키 링크 클릭 가능
- [ ] Query 히스토리

### 5.5 활동 로그
- [ ] 타임라인 UI — log.md 기반
- [ ] Ingest/Query/Lint 구분 아이콘
- [ ] 실시간 업데이트 (SignalR)

### 5.6 5단계 검증
- [ ] 브라우저에서 위키 목록 → 페이지 조회 → wikilink 네비게이션 동작
- [ ] 키워드 검색 입력 → < 100ms 응답 → 결과 표시
- [ ] AI Query 채팅 → 스트리밍 응답 실시간 표시
- [ ] SignalR 실시간 알림 동작 (Ingest 완료 시 로그 자동 갱신)

---

## 6단계: Desktop Shell (Avalonia)

### 6.1 Avalonia 프로젝트 구성
- [ ] Avalonia Desktop 프로젝트 생성
- [ ] WebView2 컨트롤 설정 (Blazor WASM 호스팅)
- [ ] 백엔드 서버 임베드 (self-hosted ASP.NET Core)
- [ ] 앱 시작 시 서버 + WebView 동시 초기화

### 6.2 시스템 트레이
- [ ] 트레이 아이콘 등록
- [ ] 우클릭 컨텍스트 메뉴 (열기 / 설정 / Ingest 상태 / 종료)
- [ ] 창 닫기 시 트레이로 최소화 (종료 아님)
- [ ] 트레이 아이콘 더블클릭 → 창 복원

### 6.3 전역 단축키
- [ ] 글로벌 핫키 등록 (`Ctrl+Shift+Space` 기본값)
- [ ] 단축키 누르면: 창 숨김 → 표시, 표시 → 숨김 토글
- [ ] 단축키 변경 가능 (설정)
- [ ] Windows API (RegisterHotKey) 사용

### 6.4 드래그 앤 드롭
- [ ] 앱 창에 파일 드롭 존 UI
- [ ] 드롭된 파일 → raw/ 디렉토리로 복사
  - [ ] 파일명에 타임스탬프 접두사 추가 (충돌 방지)
  - [ ] 지원 형식: `.md`, `.txt`, `.pdf`, `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`
- [ ] 드롭 후 Ingest 자동 트리거
- [ ] 드롭 진행 상태 표시 (복사 중 → Ingest 중 → 완료)

### 6.5 클립보드 입력
- [ ] `Ctrl+V` 붙여넣기 핸들링
  - [ ] 텍스트 → `raw/{timestamp}_clipboard.md`로 저장
  - [ ] 이미지 → `raw/{timestamp}_clipboard.png`로 저장
- [ ] 붙여넣기 후 Ingest 자동 트리거

### 6.6 빠른 입력 창
- [ ] 텍스트 입력 영역 + "저장" 버튼
- [ ] 입력한 텍스트 → `raw/{timestamp}_quicknote.md`로 저장
- [ ] 저장 후 Ingest 자동 트리거
- [ ] Enter 키로 빠른 저장

### 6.7 OS 시작 시 자동 실행
- [ ] Windows Registry / Startup 폴더 자동 실행 등록
- [ ] 설정에서 활성화/비활성화 토글

### 6.8 6단계 검증
- [ ] 앱 시작 → 트레이 아이콘 표시
- [ ] `Ctrl+Shift+Space` → 앱 창 토글
- [ ] 파일 D&D → raw/ 복사 → wiki/ 자동 생성
- [ ] 클립보드 텍스트 붙여넣기 → wiki 자동 생성
- [ ] 빠른 입력 → wiki 자동 생성
- [ ] 앱 종료(X) → 트레이 최소화, 트레이 "종료" → 실제 종료

---

## 7단계: Polish & Integration

### 7.1 Lint 대시보드
- [ ] Lint 결과 카드 UI (고아/깨진링크/모순/누락 카테고리별)
- [ ] 자동 수정 제안 (Fix 버튼 → Agent가 수정)
- [ ] 주기적 Lint (설정 가능한 스케줄)

### 7.2 지식 그래프
- [ ] `[[wikilinks]]` 기반 노드-엣지 그래프 데이터 생성
- [ ] 인터랙티브 그래프 시각화 (D3.js 또는 Sigma.js via JS interop)
- [ ] 노드 클릭 → 위키 페이지 조회

### 7.3 바이브코딩 자산 뷰
- [ ] 자산 유형별 필터 탭 (Agent / Rule / Skill / Command / Policy)
- [ ] 자산 내용 미리보기 + 클립보드 복사 버튼
- [ ] 자산에서 파일 내보내기 (.agent.md, .prompt.md 등)

### 7.4 설정 UI
- [ ] 데이터 디렉토리 경로 설정
- [ ] 전역 단축키 변경
- [ ] LLM 모델/API 설정 (GitHub Copilot 토큰)
- [ ] 자동 Lint 주기 설정
- [ ] 시작 시 자동 실행 토글
- [ ] 테마 (라이트/다크)

### 7.5 에러 핸들링 & 로깅
- [ ] 전역 예외 핸들러 (백엔드 + 프론트엔드)
- [ ] Serilog 구조화 로깅
- [ ] Ingest 실패 시 재시도 큐
- [ ] Agent 타임아웃 처리
- [ ] 사용자에게 에러 토스트 알림

### 7.6 배포 패키징
- [ ] Windows Installer (MSIX 또는 InnoSetup)
- [ ] 단일 실행 파일 (self-contained publish)
- [ ] 자동 업데이트 체크 (선택)

### 7.7 7단계 검증
- [ ] E2E: 앱 시작 → D&D → Ingest → 검색 → Query → 결과 확인
- [ ] E2E: MCP ingest → 위키 생성 → MCP search로 확인
- [ ] E2E: 바이브코딩 자산 저장 → MCP get_asset으로 조회
- [ ] Lint 대시보드에서 문제 탐지 → 자동 수정
- [ ] 10,000 페이지 인덱스 성능 테스트 (< 50ms)
- [ ] Windows Installer 설치/제거 테스트
