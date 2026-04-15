# MindAtlas 실현가능성 조사 보고서

> 조사일: 2026-04-15
> 이 문서는 PRD.md의 기술 스택이 실제 구현 가능한지 웹 검색 기반으로 조사한 결과이다.
> 구현 시 이 문서를 참조하여 정확한 패키지명, 버전, API 패턴을 사용할 것.

---

## 1. GitHub Copilot SDK (.NET)

### 조사 질문
GitHub Copilot SDK의 .NET 버전이 존재하며, 프로그래밍 방식으로 Agent를 생성하고 Custom Tool을 등록하여 LLM Wiki의 Ingest/Query/Lint 파이프라인을 구현할 수 있는가?

### 조사 결과
- **공식 저장소**: [github/copilot-sdk](https://github.com/github/copilot-sdk) — 8.4k Stars, 1.1k Forks, 63 Contributors
- **NuGet 패키지**: `GitHub.Copilot.SDK` v0.2.2 (2026-04-10 업데이트, 163,676 다운로드)
- **상태**: **Public Preview** — 기능적으로 동작하나 프로덕션 용도에는 아직 부적합할 수 있음
- **지원 언어**: TypeScript, Python, Go, **.NET (C#)**, Java
- **핵심 기여자**: SteveSandersonMS, stephentoub (Microsoft .NET 팀 핵심 멤버)
- **아키텍처**: SDK → JSON-RPC → Copilot CLI (서버 모드) — CLI 번들 포함, 별도 설치 불필요
- **인증**: GitHub 로그인, OAuth, 환경변수, **BYOK (OpenAI/Azure/Anthropic 키 직접 사용)**
- **요구사항**: .NET 8.0 이상, GitHub Copilot 구독 (Free 티어 포함, BYOK 시 구독 불필요)

### 핵심 API 패턴 (구현 시 참조)

```csharp
// 1. 클라이언트 생성 및 시작
await using var client = new CopilotClient();
await client.StartAsync();

// 2. 세션 생성 — SystemMessage로 Schema 주입
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5-mini",  // 0× 승수 — PR 무소모 (유료 플랜). §10 참조
    OnPermissionRequest = PermissionHandler.ApproveAll,
    Streaming = true,
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Append,
        Content = schemaContent  // AGENTS.md 내용 주입
    },
    Tools = [ingestTool, queryTool, lintTool]  // Custom Tools 등록
});

// 3. Custom Tool 정의 (Microsoft.Extensions.AI 사용)
var ingestTool = AIFunctionFactory.Create(
    async ([Description("Raw 파일 경로")] string rawPath) => {
        // Ingest 로직
        return new { status = "success", pages = createdPages };
    },
    "wiki_ingest",
    "raw 소스를 읽고 위키 페이지를 생성/업데이트한다");

// 4. 메시지 전송 및 스트리밍 응답 처리
session.On(evt => {
    switch (evt) {
        case AssistantMessageDeltaEvent delta:
            // 스트리밍 청크 처리
            break;
        case SessionIdleEvent:
            // 완료
            break;
    }
});
await session.SendAsync(new MessageOptions { Prompt = "..." });

// 5. 이미지 첨부 (D&D 이미지 처리)
await session.SendAsync(new MessageOptions
{
    Prompt = "이 이미지의 내용을 분석하여 위키에 정리해줘",
    Attachments = new List<UserMessageDataAttachmentsItem>
    {
        new UserMessageDataAttachmentsItemFile
        {
            Path = "/path/to/image.jpg",
            DisplayName = "image.jpg",
        }
    }
});

// 6. BYOK 사용 시
var session = await client.CreateSessionAsync(new SessionConfig
{
    Provider = new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = "your-api-key"
    }
});
```

### 추가 기능
- **Custom Agents**: 세션 생성 시 `customAgents` 배열로 전문 에이전트 정의 가능
- **System Message Customize**: 섹션별 Replace/Remove/Append/Prepend로 시스템 프롬프트 정밀 제어
- **Session Hooks**: PreToolUse/PostToolUse/UserPromptSubmitted 등 생명주기 훅
- **Infinite Sessions**: 자동 컨텍스트 압축으로 장시간 세션 유지
- **MCP 서버 연결**: 세션에서 외부 MCP 서버에 연결 가능

### 평가
| 지표 | 점수 | 근거 |
|------|------|------|
| 기술 성숙도 | 70% | Public Preview이나 MS 핵심 개발자 참여, 39 릴리스 |
| 통합 호환성 | 95% | .NET 네이티브 SDK, NuGet 패키지 제공, Custom Tool API 명확 |
| 구현 난이도 | 80% | API 문서 충실, 샘플 코드 포함, 코드 패턴 직관적 |
| 리소스 충족도 | 90% | .NET 8.0+, Copilot 구독(Free 포함) 또는 BYOK |
| 생태계 지원 | 75% | 공식 Cookbook, 커스텀 Instructions 가이드, 커뮤니티 활발 |
| **실현 가능성** | **82%** | ✅ **가능** — Public Preview 주의, 핵심 기능은 안정적 |

### 리스크 및 대응
| 리스크 | 대응 |
|--------|------|
| Public Preview → Breaking Change | SDK 버전 고정, 추상화 레이어(IWikiEngine) 도입 |
| Copilot CLI 프로세스 관리 | SDK가 자동 관리, CliUrl로 외부 서버 연결도 가능 |
| 비용 (Premium Request 과금) | GPT-5 mini(0×승수) 기본 사용, BYOK 폴백. §10 참조 |

---

## 2. MCP C# SDK (Model Context Protocol)

### 조사 질문
MindAtlas가 MCP 서버를 제공하여 VS Code Copilot 등 외부 도구에서 지식을 저장/검색할 수 있는가?

### 조사 결과
- **공식 저장소**: [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — 4.2k Stars, 679 Forks
- **NuGet 패키지** (3종):
  - `ModelContextProtocol.Core` — 최소 의존성, 클라이언트/로우레벨 서버
  - `ModelContextProtocol` — 호스팅 + DI 확장, **대부분의 프로젝트에 적합**
  - `ModelContextProtocol.AspNetCore` — HTTP 기반 MCP 서버 (SSE/Streamable HTTP)
- **최신 버전**: v1.2.0 (2026-03-25), 총 6.7M 다운로드
- **유지관리**: Microsoft 협업 — stephentoub, halter73 등 .NET 팀 주요 멤버 참여
- **라이선스**: Apache-2.0
- **지원 전송**: stdio, **Streamable HTTP (SSE 포함)**

### 핵심 구현 패턴 (구현 시 참조)

```csharp
// === stdio MCP 서버 (CLI 도구로 실행) ===
// Program.cs
using ModelContextProtocol;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MindAtlasTools>();  // 도구 클래스 등록
await builder.Build().RunAsync();

// === HTTP MCP 서버 (ASP.NET Core 통합) ===
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddMcpServer()
    .WithTools<MindAtlasTools>();
var app = builder.Build();
app.MapMcp();  // /mcp 엔드포인트 자동 등록
app.Run();

// === 도구 정의 ===
[McpServerToolType]
public class MindAtlasTools
{
    [McpServerTool(Name = "mindatlas_ingest")]
    [Description("텍스트를 MindAtlas 지식으로 저장합니다")]
    public async Task<string> Ingest(
        [Description("저장할 텍스트")] string content,
        [Description("제목")] string? title = null)
    {
        // raw/ 저장 + Ingest 파이프라인 트리거
        return "지식이 저장되었습니다";
    }

    [McpServerTool(Name = "mindatlas_search")]
    [Description("MindAtlas 위키를 키워드로 검색합니다")]
    public async Task<string> Search(
        [Description("검색 키워드")] string keyword,
        [Description("최대 결과 수")] int limit = 10)
    {
        // Fast Index 검색
        return JsonSerializer.Serialize(results);
    }
    
    [McpServerTool(Name = "mindatlas_query")]
    [Description("MindAtlas 위키에 자연어로 질문합니다")]
    public async Task<string> Query(
        [Description("질문 내용")] string question)
    {
        // AI Agent 질의
        return answer;
    }
    
    [McpServerTool(Name = "mindatlas_get_asset")]
    [Description("바이브코딩 자산을 검색·조회합니다")]
    public async Task<string> GetAsset(
        [Description("자산 유형")] string? assetType = null,
        [Description("검색어")] string query = "")
    {
        // 자산 검색
        return JsonSerializer.Serialize(assets);
    }

    [McpServerTool(Name = "mindatlas_lint")]
    [Description("위키 건강 검진을 실행합니다")]
    public async Task<string> Lint(
        [Description("검진 범위")] string scope = "full")
    {
        // Lint 엔진 실행
        return JsonSerializer.Serialize(lintResult);
    }
}
```

### VS Code MCP 설정 예시
```json
// .vscode/mcp.json (stdio 모드)
{
  "servers": {
    "mindatlas": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "path/to/MindAtlas.Mcp"]
    }
  }
}

// .vscode/mcp.json (HTTP 모드)
{
  "servers": {
    "mindatlas": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

### 평가
| 지표 | 점수 | 근거 |
|------|------|------|
| 기술 성숙도 | 90% | v1.2.0 안정 릴리스, 6.7M 다운로드, Microsoft 공식 협업 |
| 통합 호환성 | 95% | ASP.NET Core 네이티브 통합, VS Code/Claude Desktop 호환 |
| 구현 난이도 | 90% | Attribute 기반 도구 정의, DI 통합, 최소 보일러플레이트 |
| 리소스 충족도 | 95% | .NET 8.0+, 추가 의존성 최소 |
| 생태계 지원 | 90% | 공식 문서, 샘플, 커뮤니티 활발 |
| **실현 가능성** | **92%** | ✅ **가능** — 가장 성숙한 컴포넌트 |

### 리스크 및 대응
| 리스크 | 대응 |
|--------|------|
| MCP 프로토콜 버전 변경 | SDK 업데이트 추종, 버전 핀 |
| stdio + HTTP 동시 지원 | 별도 호스트 프로세스 또는 ASP.NET Core 앱에 양쪽 통합 |

---

## 3. Avalonia Desktop Shell

### 조사 질문
Avalonia로 시스템 트레이 상주, 전역 단축키, 드래그 앤 드롭을 갖춘 데스크톱 셸을 만들고, WebView로 Blazor WASM을 호스팅할 수 있는가?

### 3-1. Avalonia WebView (Blazor WASM 호스팅)

### 조사 결과
- **공식 패키지**: `Avalonia.Controls.WebView` v12.0.0 (2026-04-07, 공식 avaloniaui 게시)
- **엔진**: 플랫폼 네이티브 — WebView2 (Windows), WebKit (macOS), WebKitGTK (Linux)
- **특징**: 임베디드 브라우저 불필요 (경량), AOT 호환, JS 실행·양방향 메시징
- **라이선스**: MIT
- **대안**: `CheapAvaloniaBlazor` v3.0.0 (20,374 다운로드) — Blazor+Avalonia+Photino 전용 프레임워크
- **대안 2**: `WebView.Avalonia` v11.0.0.1 (80,249 다운로드) — 커뮤니티 패키지, 다소 오래됨

### Blazor WASM 호스팅 전략 (구현 시 참조)

```
방법 A: Avalonia.Controls.WebView + Self-hosted Kestrel
┌─────────────────────────────┐
│  Avalonia Desktop App       │
│  ┌───────────────────────┐  │
│  │ NativeWebView         │  │
│  │ Source="http://        │  │
│  │   localhost:5001"      │  │
│  └───────────────────────┘  │
│            ↕                 │
│  Embedded ASP.NET Core      │
│  (Self-hosted, port 5001)   │
│  ├─ Blazor WASM static     │
│  ├─ Wiki API                │
│  └─ SignalR Hub             │
└─────────────────────────────┘

방법 B: CheapAvaloniaBlazor (Photino 기반)
- Blazor 컴포넌트를 직접 Avalonia 안에 렌더링
- Photino 경량 웹뷰 사용
- JS Bridge 내장
```

**권장**: 방법 A (공식 WebView + Self-hosted Kestrel)
- Avalonia 공식 패키지 사용으로 안정성 확보
- ASP.NET Core self-host로 API와 UI 동시 제공
- WebView2가 Windows에서 기본 설치됨 (Edge 기반)

```csharp
// Avalonia MainWindow.axaml
<Window xmlns:web="using:Avalonia.Controls.WebView">
    <web:NativeWebView x:Name="WebView" 
                       Source="http://localhost:5001" />
</Window>

// App startup — 백엔드 서버 시작 후 WebView 로드
public override void OnFrameworkInitializationCompleted()
{
    // 1. ASP.NET Core 서버 시작 (백그라운드)
    _host = Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(web => {
            web.UseStartup<Startup>();
            web.UseUrls("http://localhost:5001");
        }).Build();
    _ = _host.StartAsync();

    // 2. Avalonia UI 시작
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
    }
    base.OnFrameworkInitializationCompleted();
}
```

### 3-2. 시스템 트레이

### 조사 결과
- Avalonia에 `TrayIcon` 컨트롤 **내장** (별도 패키지 불필요)
- Application.xaml 또는 코드비하인드에서 선언적 정의 가능
- 멀티플랫폼 지원 (Windows, macOS, Linux)

```xml
<!-- App.axaml -->
<Application.Resources>
    <TrayIcon Icon="/Assets/mindatlas.ico"
              ToolTipText="MindAtlas"
              IsVisible="True">
        <TrayIcon.Menu>
            <NativeMenu>
                <NativeMenuItem Header="열기" Command="{Binding ShowCommand}" />
                <NativeMenuItemSeparator />
                <NativeMenuItem Header="Ingest 상태" />
                <NativeMenuItem Header="설정" Command="{Binding SettingsCommand}" />
                <NativeMenuItemSeparator />
                <NativeMenuItem Header="종료" Command="{Binding ExitCommand}" />
            </NativeMenu>
        </TrayIcon.Menu>
    </TrayIcon>
</Application.Resources>
```

### 3-3. 전역 단축키

### 조사 결과
- **NuGet**: `PhoenixTools.NHotkey.Avalonia` v3.1.5 (406 다운로드 — 매우 낮음)
- **대안 (권장)**: Windows API 직접 P/Invoke — `RegisterHotKey`/`UnregisterHotKey`
- Avalonia에 내장 전역 핫키 API는 없음

```csharp
// Windows P/Invoke 방식 (구현 시 참조)
public class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT   = 0x0004;
    private const uint VK_SPACE    = 0x20;
    
    public void Register(IntPtr hwnd)
    {
        // Ctrl+Shift+Space
        RegisterHotKey(hwnd, 1, MOD_CONTROL | MOD_SHIFT, VK_SPACE);
    }
    
    public void Dispose()
    {
        UnregisterHotKey(_hwnd, 1);
    }
}
```

### 3-4. 드래그 앤 드롭

### 조사 결과
- Avalonia에 **DragDrop API 내장** — `DragDrop.Drop` 이벤트
- 파일 경로 목록을 `DataFormats.Files`로 수신 가능

```csharp
// DragDrop 처리 (구현 시 참조)
private async void OnDrop(object? sender, DragEventArgs e)
{
    var files = e.Data.GetFiles();
    if (files != null)
    {
        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var dest = Path.Combine(_rawDir, 
                $"{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(path)}");
            File.Copy(path, dest);
            // Ingest 트리거
            await _ingestPipeline.TriggerAsync(dest);
        }
    }
}
```

### Avalonia 종합 평가
| 지표 | 점수 | 근거 |
|------|------|------|
| 기술 성숙도 | 85% | Avalonia 12.0 안정, WebView 공식 패키지, TrayIcon 내장 |
| 통합 호환성 | 75% | WebView로 Blazor WASM 호스팅 가능하나 간접 통합 |
| 구현 난이도 | 65% | WebView+Self-hosted 서버 조합 복잡, 전역 핫키 P/Invoke 필요 |
| 리소스 충족도 | 90% | WebView2 Windows 기본, .NET 8.0+ |
| 생태계 지원 | 70% | Blazor+Avalonia 직접 통합 사례 제한적, WebView 패키지 신규 |
| **실현 가능성** | **77%** | ⚠️ **조건부 가능** — WebView 통합 복잡도 주의 |

### 리스크 및 대응
| 리스크 | 대응 |
|--------|------|
| Avalonia WebView v12.0.0 신규 (8일전 출시) | 안정화 대기 또는 CheapAvaloniaBlazor 대안 |
| 전역 핫키 크로스플랫폼 | 1차 Windows P/Invoke, 추후 macOS/Linux 추가 |
| Self-hosted 서버 + WebView 초기화 순서 | 서버 Health Check 후 WebView Source 설정 |

### 대안 (실현가능성 낮을 경우)
- **대안 1**: `CheapAvaloniaBlazor` — Blazor + Avalonia + Photino 통합 프레임워크 (20k 다운로드, 검증 필요)
- **대안 2**: 순수 Avalonia UI — Blazor 없이 Avalonia MVVM으로 전체 UI 구현 (WebView 불필요, 마크다운 렌더링은 별도 처리)
- **대안 3**: WPF + WebView2 — Windows 전용이나 훨씬 안정적인 경로

---

## 4. Blazor WebAssembly

### 조사 질문
Blazor WASM으로 위키 뷰어(마크다운 렌더링, 검색, AI Query)를 구현할 수 있는가?

### 조사 결과
- .NET 공식 프레임워크, 완전 지원
- Markdig NuGet (v1.1.2, **59.1M 다운로드**) — 서버 사이드에서 HTML 변환 후 전달, 또는 WASM에서 직접 사용 가능
- SignalR 클라이언트 — 실시간 알림 네이티브 지원
- HttpClient — REST API 호출

### 핵심 패턴

```csharp
// 마크다운 → HTML 변환 (Markdig)
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();
var html = Markdown.ToHtml(markdownContent, pipeline);

// [[wikilinks]] 커스텀 처리 — Markdig 확장
// Markdig는 확장 가능한 아키텍처로 커스텀 인라인 파서 추가 가능
public class WikiLinkInlineParser : InlineParser
{
    // [[page-name]] 패턴을 <a href="/wiki/page-name"> 으로 변환
}
```

### 평가
| 지표 | 점수 | 근거 |
|------|------|------|
| 기술 성숙도 | 95% | .NET 공식, 프로덕션 검증 |
| 통합 호환성 | 95% | ASP.NET Core, SignalR 네이티브 |
| 구현 난이도 | 85% | 풍부한 문서, 컴포넌트 라이브러리 |
| 리소스 충족도 | 95% | .NET 8.0+ |
| 생태계 지원 | 90% | MudBlazor, Radzen 등 UI 라이브러리 풍부 |
| **실현 가능성** | **92%** | ✅ **가능** |

---

## 5. Markdig (마크다운 처리)

### 조사 결과
- **NuGet**: `Markdig` v1.1.2 — **59.1M 총 다운로드**, 77.5K/일
- 600+ CommonMark 테스트 통과, 20+ 내장 확장
- 확장 가능한 아키텍처 — 커스텀 파서/렌더러 추가 가능
- NETStandard 2.0+ 호환 → Blazor WASM에서 직접 사용 가능
- `[[wikilinks]]` → 커스텀 InlineParser로 구현 가능

### 평가
| 지표 | 점수 | 근거 |
|------|------|------|
| **실현 가능성** | **98%** | ✅ 사실상 .NET 표준 마크다운 엔진 |

---

## 6. FileSystemWatcher (.NET)

### 조사 결과
- .NET 내장 `System.IO.FileSystemWatcher` — 추가 패키지 불필요
- Created, Changed, Deleted, Renamed 이벤트 지원
- 알려진 이슈: 짧은 시간 내 다중 이벤트 발생 → **디바운싱 필수**
- 대안: `Microsoft.Extensions.FileSystemGlobbing`으로 패턴 기반 필터링 보조

### 구현 패턴

```csharp
// 디바운싱 포함 FileWatcher (구현 시 참조)
public class RawDirectoryWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Channel<string> _channel;
    
    public RawDirectoryWatcher(string rawPath)
    {
        _channel = Channel.CreateUnbounded<string>();
        _watcher = new FileSystemWatcher(rawPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };
        _watcher.Created += (_, e) => _channel.Writer.TryWrite(e.FullPath);
    }
    
    public async IAsyncEnumerable<string> WatchAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var debounceSet = new HashSet<string>();
        while (await _channel.Reader.WaitToReadAsync(ct))
        {
            // 500ms 디바운스
            debounceSet.Clear();
            while (_channel.Reader.TryRead(out var path))
                debounceSet.Add(path);
            
            await Task.Delay(500, ct);
            while (_channel.Reader.TryRead(out var path))
                debounceSet.Add(path);
            
            foreach (var path in debounceSet)
                yield return path;
        }
    }
}
```

### 평가
| 지표 | 점수 | 근거 |
|------|------|------|
| **실현 가능성** | **95%** | ✅ .NET 내장, 디바운싱만 직접 구현 |

---

## 7. 종합 평가

### 전체 아키텍처 실현가능성 매트릭스

| 컴포넌트 | 패키지/기술 | 버전 | 상태 | 실현가능성 |
|----------|------------|------|------|-----------|
| AI Agent | `GitHub.Copilot.SDK` | 0.2.2 | Public Preview | 82% ⚠️ |
| MCP 서버 | `ModelContextProtocol` + `.AspNetCore` | 1.2.0 | Stable | 92% ✅ |
| 데스크톱 셸 | Avalonia + `Avalonia.Controls.WebView` | 12.0.0 | Stable (WebView 신규) | 77% ⚠️ |
| 프론트엔드 | Blazor WebAssembly | .NET 9 | Stable | 92% ✅ |
| 마크다운 | `Markdig` | 1.1.2 | Stable | 98% ✅ |
| 파일 감시 | `System.IO.FileSystemWatcher` | .NET 내장 | Stable | 95% ✅ |
| 실시간 통신 | SignalR | .NET 내장 | Stable | 95% ✅ |
| 트레이 아이콘 | Avalonia TrayIcon | 내장 | Stable | 90% ✅ |
| 전역 핫키 | Windows P/Invoke | OS API | Stable | 85% ✅ |
| **GPT-5 mini 제로-PR** | GPT-5 mini (Copilot 포함 모델) | — | **Included** | **90% ✅** |

### 종합 실현가능성: **88%** — ✅ **조건부 가능**

### 결론

MindAtlas 프로젝트는 **실현 가능**하다. 핵심 기술 스택 모두 NuGet 패키지가 존재하며, 공식 문서와 코드 패턴이 확보되어 있다. GPT-5 mini를 기본 모델로 사용하면 Copilot Pro($10/mo) 이상에서 **Premium Request 소모 없이 무제한 위키 운영**이 가능하다.

**주의가 필요한 영역:**
1. **GitHub Copilot SDK** — Public Preview 상태. BYOK 옵션으로 안정성 확보 가능. 추상화 레이어 도입 권장.
2. **Avalonia WebView** — v12.0.0으로 매우 신규. Self-hosted Kestrel + WebView 조합의 초기화 순서, 에러 처리에 세심한 주의 필요.
3. **GPT-5 mini 0-승수 정책** — 현재 유료 플랜에서 무제한이나, GitHub의 "변경될 수 있음" 고지에 유의. BYOK 폴백 전략 권장.

---

## 8. 구현 시 NuGet 패키지 목록

> 아래 목록을 각 프로젝트의 `.csproj`에 참조할 것.

### MindAtlas.Core
```xml
<PackageReference Include="Markdig" Version="1.1.2" />
```

### MindAtlas.Engine
```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.2.2" />
<PackageReference Include="Markdig" Version="1.1.2" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="9.0.*" />
```

### MindAtlas.Server
```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="*" />
```

### MindAtlas.Web (Blazor WASM)
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="9.0.*" />
<PackageReference Include="Markdig" Version="1.1.2" />
```

### MindAtlas.Desktop
```xml
<PackageReference Include="Avalonia" Version="12.0.0" />
<PackageReference Include="Avalonia.Desktop" Version="12.0.0" />
<PackageReference Include="Avalonia.Controls.WebView" Version="12.0.0" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.0" />
```

---

## 9. 참고 링크 모음

| 리소스 | URL |
|--------|-----|
| GitHub Copilot SDK (공식) | https://github.com/github/copilot-sdk |
| Copilot SDK .NET README | https://github.com/github/copilot-sdk/tree/main/dotnet |
| Copilot SDK Getting Started | https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md |
| Copilot SDK Custom Agents | https://github.com/github/copilot-sdk/blob/main/docs/features/custom-agents.md |
| Copilot SDK MCP 연결 | https://github.com/github/copilot-sdk/blob/main/docs/features/mcp.md |
| Copilot SDK .NET Instructions | https://github.com/github/awesome-copilot/blob/main/instructions/copilot-sdk-csharp.instructions.md |
| MCP C# SDK | https://github.com/modelcontextprotocol/csharp-sdk |
| MCP C# SDK 문서 | https://csharp.sdk.modelcontextprotocol.io/ |
| Avalonia.Controls.WebView | https://www.nuget.org/packages/Avalonia.Controls.WebView |
| Avalonia WebView QuickStart | https://docs.avaloniaui.net/accelerate/components/webview/quickstart |
| CheapAvaloniaBlazor (대안) | https://www.nuget.org/packages/CheapAvaloniaBlazor |
| Markdig | https://github.com/xoofx/markdig |
| Karpathy LLM Wiki | https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f |
| GitHub Copilot Plans | https://docs.github.com/en/copilot/about-github-copilot/plans-for-github-copilot |
| Copilot Premium Requests | https://docs.github.com/en/copilot/concepts/billing/copilot-requests |
| OpenAI GPT-5 mini Model | https://developers.openai.com/api/docs/models/gpt-5-mini |

---

## 10. GPT-5 mini 기반 제로-PR 운영 가능성 분석

> 조사일: 2026-04-15
> 핵심 질문: LLM Wiki의 Ingest/Query/Lint 파이프라인을 GPT-5 mini로 구동할 때, Premium Request(PR) 소모 없이 무제한 운영이 가능한가?

### 조사 질문

GitHub Copilot SDK를 통해 GPT-5 mini 모델을 사용할 경우, LLM Wiki의 3-Operation(Ingest/Query/Lint)을 Premium Request 소모 없이 무제한으로 실행할 수 있는가? 또한 GPT-5 mini의 성능이 위키 생성·검색·검진에 충분한가?

### 조사 결과

#### 10-1. Premium Request 과금 구조

| 플랜 | 월 비용 | GPT-5 mini 승수 | PR 소모 | 비고 |
|------|---------|-----------------|---------|------|
| Copilot Free | $0 | 1× | **1 PR/요청** | 월 50 PR 한도 → 위키 운영 불가 |
| Copilot Student | $0 | 0× | **없음 (무제한)** | 학생 인증 필요, 월 300 PR 별도 |
| Copilot Pro | $10 | 0× | **없음 (무제한)** | ✅ 권장 최소 플랜 |
| Copilot Pro+ | $39 | 0× | **없음 (무제한)** | 월 1,500 PR (프리미엄 모델용) |
| Copilot Business | $19/seat | 0× | **없음 (무제한)** | 조직용 |
| Copilot Enterprise | $39/seat | 0× | **없음 (무제한)** | 엔터프라이즈용 |

**출처**: [GitHub Docs — Copilot Requests](https://docs.github.com/en/copilot/concepts/billing/copilot-requests)
> "GPT-5 mini, GPT-4.1 and GPT-4o are the included models, and do not consume any premium requests if you are on a paid plan."
> "Using GPT-5 mini on a paid plan: No premium requests are consumed."

#### 10-2. GPT-5 mini 모델 스펙

| 항목 | 스펙 | LLM Wiki 요구 | 충족 여부 |
|------|------|---------------|----------|
| 컨텍스트 윈도우 | **400,000 토큰** | Ingest 시 대용량 raw 문서 처리 | ✅ 충분 (문서 수백 페이지 분량) |
| 최대 출력 토큰 | **128,000 토큰** | Wiki 페이지 생성 (수천 자) | ✅ 충분 |
| 추론 토큰 | **지원** (none/low/medium/high/xhigh) | Lint 검진, 복잡 Query | ✅ 추론 수준 조절 가능 |
| Function Calling | **지원** | Custom Tool 호출 (Ingest/Query/Lint) | ✅ 필수 기능 |
| Structured Output | **지원** | JSON 스키마 응답 (Lint 결과 등) | ✅ |
| 이미지 입력 | **지원** | D&D 이미지 분석 후 위키 변환 | ✅ |
| 스트리밍 | **지원** | 실시간 응답 표시 | ✅ |
| MCP | **지원** | MCP 서버 연동 | ✅ |
| 모델 등급 | "Near-frontier intelligence" | 위키 품질 요구 수준 | ⚠️ 아래 분석 참조 |

**출처**: [OpenAI — GPT-5 mini Model Page](https://developers.openai.com/api/docs/models/gpt-5-mini)

#### 10-3. LLM Wiki 3-Operation별 GPT-5 mini 적합성

| Operation | 작업 내용 | 난이도 | GPT-5 mini 적합도 | 분석 |
|-----------|----------|--------|-------------------|------|
| **Ingest** | Raw 텍스트/이미지 → 구조화된 Wiki 마크다운 생성 | 중 | ✅ 90% | 400K 컨텍스트로 대용량 raw 처리 가능. Function Calling으로 파일 읽기/쓰기 도구 호출. 구조적 변환은 mini 모델의 강점 영역. |
| **Query** | Wiki 검색 + 자연어 질의 응답 | 중–상 | ✅ 85% | Structured Output으로 검색 결과 포맷팅. 추론 토큰으로 복잡 질의 처리. 단, 매우 심층적인 추론 쿼리에서는 상위 모델 대비 품질 저하 가능. |
| **Lint** | Wiki 건강 검진 — 데드링크, 불일치, 정보 결손 검출 | 중 | ✅ 88% | Structured Output으로 검진 결과 JSON 반환. 규칙 기반 검진은 mini 모델로 충분. 의미적 일관성 검증에는 추론 토큰(medium+) 활용 권장. |

#### 10-4. 0-승수 모델 대안 비교

유료 플랜에서 PR 0 소모인 모델 간 비교:

| 모델 | PR 승수 | 컨텍스트 | 추론 | Function Calling | 이미지 입력 | 위키 적합도 |
|------|---------|----------|------|-----------------|-------------|-----------|
| **GPT-5 mini** | 0× | 400K | ✅ | ✅ | ✅ | ✅ **최적** |
| GPT-4.1 | 0× | 1M | ❌ | ✅ | ✅ | ✅ 대용량 Ingest에 유리 |
| GPT-4o | 0× | 128K | ❌ | ✅ | ✅ | ⚠️ 컨텍스트 제한 |
| Raptor mini | 0× | 미공개 | 미공개 | 미공개 | 미공개 | ❓ 정보 부족 |

**권장 전략**: 기본 모델로 GPT-5 mini 사용, 대용량 Ingest 시 GPT-4.1 자동 전환

```csharp
// Copilot SDK — 모델 선택 전략 (구현 시 참조)
var config = new SessionConfig
{
    // 기본: GPT-5 mini (무제한, 0 PR)
    Model = "gpt-5-mini",
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Append,
        Content = wikiSchema
    },
    Tools = [ingestTool, queryTool, lintTool]
};

// 대용량 Ingest 시 GPT-4.1로 전환 (1M 컨텍스트, 역시 0 PR)
if (rawContentTokens > 300_000)
{
    config.Model = "gpt-4.1";
}
```

#### 10-5. Copilot Free 플랜 운용 시나리오

| 항목 | 값 |
|------|----|
| 월 PR 한도 | 50회 |
| GPT-5 mini 승수 | 1× |
| 예상 소모 | Ingest 1회 + Query 2–3회/일 = 약 100회/월 |
| **판정** | ❌ **부족** — 일상적 위키 운영 불가 |

Free 플랜에서는 BYOK(Bring Your Own Key)로 전환 필요:
```csharp
// BYOK 모드 — Copilot 구독 없이 자체 API 키 사용
var config = new SessionConfig
{
    Provider = new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    },
    Model = "gpt-5-mini"  // OpenAI 직접 과금: $0.25/MTok input, $2.00/MTok output
};
```

### 평가 지표

| 지표 | 점수 | 근거 |
|------|------|------|
| 기술 성숙도 | 90% | GPT-5 mini는 GPT-5 계열 안정 모델, Copilot에서 포함 모델로 공식 지정 |
| 통합 호환성 | 95% | Copilot SDK `Model = "gpt-5-mini"` 직접 지정 가능, Function Calling/Structured Output 완비 |
| 구현 난이도 | 90% | 모델명 변경만으로 적용, 추가 구현 불필요 |
| 리소스 충족도 | 85% | Copilot Pro($10/mo) 이상 필요. Free 플랜은 50 PR/월로 부족 |
| 생태계 지원 | 90% | Copilot 공식 포함 모델, 문서 충실, 0-승수 대안 모델(GPT-4.1, GPT-4o)도 존재 |
| **실현 가능성** | **90%** | ✅ **가능** |

### 결론

**가능** — Copilot Pro($10/mo) 이상의 유료 플랜에서 GPT-5 mini는 **Premium Request를 전혀 소모하지 않고 무제한** 사용할 수 있다.

GPT-5 mini의 400K 컨텍스트, 128K 출력, 추론 토큰, Function Calling, Structured Output, 이미지 입력은 LLM Wiki의 Ingest/Query/Lint 3-Operation을 모두 충족한다. "Near-frontier intelligence" 수준으로 위키 페이지 생성 품질도 실용적이다.

### 제약 사항 및 블로커

- **Copilot Free($0)에서는 비현실적** — 월 50 PR, GPT-5 mini 1×승수 = 50회 요청이 한도. 일상 위키 운영(일 3–5회 이상)에 부족.
- **최소 요구**: Copilot Pro ($10/mo) 또는 Student (무료, 학생 인증 필요)
- **매우 심층적인 추론 쿼리**(복잡한 논리 체인, 수학적 분석 등)에서는 GPT-5.1/5.2 대비 품질 저하 가능 — 이 경우 1× PR 소모.
- **"모델 포함 범위는 변경될 수 있음"** (GitHub 공식 고지) — 미래에 0-승수 정책 변경 리스크 존재.
- **응답 시간은 피크 시간대에 가변적** (GitHub Footnote 2)

### 대안 (Free 플랜 사용자를 위한)

1. **BYOK (Bring Your Own Key)**: Copilot SDK의 Provider 설정으로 OpenAI API 직접 사용. GPT-5 mini $0.25/MTok 입력 + $2.00/MTok 출력. 일 5회 쿼리 기준 월 $1–3 수준.
2. **GPT-4.1 (0×승수)**: 유료 플랜에서 GPT-5 mini 대신 사용 가능. 1M 컨텍스트로 대용량 문서에 더 유리. 다만 추론 토큰 미지원.
3. **하이브리드 전략**: 단순 Ingest/Lint → GPT-5 mini (0 PR), 심층 Query → GPT-5.1 (1× PR). 월 300 PR 예산 내에서 심층 질의 300회 가능.

### 비용 시나리오 요약

| 시나리오 | 월 비용 | GPT-5 mini 한도 | 심층 질의 (GPT-5.1+) |
|---------|---------|----------------|---------------------|
| Copilot Free + BYOK | ~$1–3 (API) | BYOK 무제한 | BYOK로 별도 과금 |
| **Copilot Pro** | **$10** | **무제한 (0 PR)** | **300회/월** |
| Copilot Pro+ | $39 | 무제한 (0 PR) | 1,500회/월 |
| Copilot Business | $19/seat | 무제한 (0 PR) | 300회/user/월 |
