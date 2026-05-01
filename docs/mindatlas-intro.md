---
marp: true
theme: default
paginate: true
size: 16:9
header: 'MindAtlas — LLM Wiki'
footer: '© MindAtlas · Knowledge that writes itself'
style: |
  section {
    font-family: 'Pretendard', 'Segoe UI', sans-serif;
    padding: 60px 70px;
  }
  section.lead {
    text-align: center;
    justify-content: center;
  }
  h1 { color: #1f2a44; }
  h2 { color: #2a3a5f; border-bottom: 2px solid #e2e8f0; padding-bottom: 8px; }
  strong { color: #d35400; }
  code { background: #f4f4f5; padding: 2px 6px; border-radius: 4px; }
  blockquote {
    border-left: 4px solid #6366f1;
    color: #374151;
    background: #f5f7ff;
    padding: 10px 18px;
    border-radius: 4px;
  }
  .columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 28px;
  }
---

<!-- _class: lead -->

# MindAtlas
## 스스로 자라는 **LLM Wiki**

개발자 · 작가 · 유튜버를 위한  
**지식 정리의 새로운 표준**

<br>


---

## 오늘 이야기할 것

1. 왜 우리는 늘 **똑같은 것을 다시 찾고** 있을까?
2. MindAtlas가 푸는 문제 — **LLM Wiki**란?
3. 세 가지 관점에서 본 가치
   - 👨‍💻 개발자
   - ✍️ 작가
   - 🎬 유튜버·크리에이터
4. 실제 동작 방식 (Ingest → Query → Link)
5. 바로 써볼 수 있는 시작법

---

<!-- _class: lead -->

# 1. 문제 정의
## "나는 분명히 어딘가에 적어뒀는데…"

---

## 지식은 흩어진다

창작자·개발자의 **하루 평균 지식 저장 경로**:

- 메모 앱 (Obsidian, Notion, Apple Notes…)
- 채팅 기록 (ChatGPT, Copilot, Slack)
- 브라우저 북마크와 탭 수십 개
- 코드 주석, PR 설명, 회의록
- 영상 자막 초고, 대본 버전 v1/v2/v3.docx

> 정보는 **모였지만 연결되지 않았다.**  
> 검색은 가능해도, **이해되지 않는다.**

---

## 기존 Wiki의 한계

| 기존 Wiki | 현실 |
|---|---|
| 페이지를 **사람이** 만든다 | 귀찮아서 안 씀 |
| 링크를 **사람이** 건다 | 고아 페이지 양산 |
| 구조를 **사람이** 설계한다 | 한 달 뒤 무너짐 |
| 검색은 **키워드** 기반 | 맥락을 못 읽음 |

**→ 결국 "문서화 담당자"가 필요한 시스템**

---

<!-- _class: lead -->

# 2. MindAtlas란?
## 텍스트를 던지면, **AI가 Wiki로 만든다**

---

## 한 줄 정의

> **"원문(raw)을 던지면,  
>   AI가 의미 단위로 쪼개  
>   위키 페이지 + 링크 + 인덱스로 엮어주는 시스템."**

<br>

핵심 루프:

```
Ingest  →  AI 분석  →  Wiki Page 생성
   ↑                        ↓
   └──  Query / Lint  ──────┘
```

---

## 네 가지 핵심 동작 (1/2)

<div class="columns">

**1. Ingest (주입)**
- 아무 텍스트나 붙여넣기
- AI가 주제별로 **자동 분할**
- 원문은 **raw**로 영구 보존

**2. Auto-Link**
- `[[위키링크]]` 자동 부여
- 관련 개념 **역추적**
- 지식 그래프 형성

</div>

---

## 네 가지 핵심 동작 (2/2)

<div class="columns">

**3. Query (질의)**
- 자연어 질문
- Wiki 전체 맥락으로 응답
- 인용 페이지 함께 반환

**4. Lint (건강검진)**
- 고아 페이지 감지
- 깨진 링크 탐지
- 인덱스 누락 확인

</div>

---

## 무엇이 다른가?

- **수동 문서화 → 수동이 아니다**  
  ingest 한 번이면 구조화까지 끝.

- **검색 → 대화**  
  "그때 그거 뭐였지?" 를 문장으로 물어보면 됨.

- **파편 → 네트워크**  
  같은 개념은 자동으로 한 페이지로 수렴.

- **MCP 연동**  
  Copilot·Claude 같은 AI 에이전트가  
  **너의 Wiki를 직접 읽고 쓸 수 있음.**

---

<!-- _class: lead -->

# 3. 누구에게 무엇이 좋은가?

---

<!-- _class: lead -->

# 👨‍💻 개발자에게

---

## 개발자의 고통

- PR 설명, 디자인 문서, 트러블슈팅 로그가  
  **전부 다른 곳에** 있다.
- "이 버그 전에 한 번 고쳤던 것 같은데…"
- Onboarding = **구전(口傳) 문화**
- Copilot이 **우리 프로젝트 맥락을 모른다.**

---

## MindAtlas가 주는 것

1. **Vibe Coding Assets**  
   agents / rules / prompts / snippets / templates 을  
   Wiki 자산으로 관리 → `mindatlas_get_asset` 으로 즉시 호출.

2. **MCP 서버 내장**  
   `mindatlas_query`, `mindatlas_ingest`, `mindatlas_lint` …  
   AI 에이전트가 **프로젝트의 "기억"에 직접 접근**.

3. **자동 컨텍스트**  
   회의록·디버그 로그를 ingest 하면  
   Copilot이 그걸 근거로 코드를 제안.

---

## 개발자 시나리오

> **월요일 오전** — 긴 Slack 스레드를 복사해 ingest.  
> **월요일 오후** — "지난주 인증 버그 원인 뭐였지?" 라고 질문.  
> **결과** — 관련 페이지 3개 + 원문 링크 + 해결 PR 요약.

<br>

결과적으로:

- Onboarding 문서가 **저절로 쌓인다**
- `dev-skill` 같은 **스킬팩**을 Wiki 자산으로 공유
- AI 도구가 **팀 지식을 학습**한 상태로 동작

---

<!-- _class: lead -->

# ✍️ 작가에게

---

## 작가의 고통

- 세계관 설정이 **Excel 여러 시트**에 흩어짐
- 캐릭터 이름·나이·말투가 **챕터마다 어긋남**
- "2장에서 이 인물 이미 죽였던가?"
- 장편일수록 **설정 누락 = 독자 이탈**

---

## MindAtlas가 주는 것

1. **설정이 곧 Wiki**  
   인물·장소·마법체계·연표를 ingest → 자동 분리 저장.

2. **위키링크 자동화**  
   본문을 ingest 하면 등장인물이 자동으로 `[[인물:리안]]`으로 연결.

3. **일관성 Lint**  
   "인물 A는 있지만 참조가 0회" → **고아 감지**  
   "날짜가 서로 모순" → 후속 질의로 확인.

4. **자연어 질의**  
   "리안의 어머니 이름이 뭐였지?" 즉답.

---

## 작가 시나리오

> **초안 작성 중** — "북부 왕국 성년식 나이가 몇이었더라?"  
> **질의** — `mindatlas_query("북부 왕국 성년식")`  
> **응답** — 18세 / 해당 페이지 / 관련 의식 페이지 3개.

<br>

결과:

- **연재형 장편**에서 설정 붕괴 방지
- 공동 창작(합작 소설)에서 **설정 동기화**
- 퇴고 시 **캐릭터 아크**를 한눈에 추적

---

<!-- _class: lead -->

# 🎬 유튜버 · 크리에이터에게

---

## 크리에이터의 고통

- 기획안, 대본, 자료조사, 댓글 피드백이  
  **채널마다·영상마다** 분산.
- 시리즈가 길어질수록 **과거 발언과 충돌**
- 자료 조사본이 **한 번 쓰고 사라짐**
- 협업 작가·편집자와 **맥락 공유 비용** 폭증

---

## MindAtlas가 주는 것

1. **리서치 아카이브**  
   수집한 웹 자료를 ingest → 주제별 자동 정리.

2. **시리즈 바이블**  
   캐릭터/출연자/고정 코너/인트로 규칙을  
   영구 Wiki로 유지.

3. **대본 일관성**  
   "이 주제 예전에 다뤘나?" 한 줄 질의로 확인.

4. **쇼츠·롱폼 재활용**  
   기존 대본을 ingest → 핵심 주장만 뽑아 Shorts 스크립트 생성.

---

## 크리에이터 시나리오

> **기획 단계** — 최근 한 달치 구독자 댓글을 ingest.  
> **질의** — "최근 가장 자주 요청된 주제 3가지?"  
> **응답** — 주제별 페이지 + 언급 빈도 + 원문 댓글 링크.

<br>

결과:

- **데이터 기반 콘텐츠 기획**
- 채널 **톤 & 매너**가 흔들리지 않음
- 편집자·작가에게 Wiki 링크 하나로 **브리핑 끝**

---

<!-- _class: lead -->

# 4. 어떻게 동작하는가?

---

## 아키텍처 한 장

```
  ┌─────────────┐     ┌──────────────┐     ┌────────────┐
  │  Desktop    │     │  Web (Blazor)│     │  MCP Client│
  │ (Avalonia)  │     │    WASM      │     │ (Copilot등)│
  └──────┬──────┘     └──────┬───────┘     └─────┬──────┘
         │                   │                   │
         └─────────┬─────────┴─────────┬─────────┘
                   ▼                   ▼
           ┌───────────────────────────────┐
           │   MindAtlas.Server (ASP.NET)  │
           │   REST · SignalR · MCP /mcp   │
           └──────────────┬────────────────┘
                          ▼
           ┌───────────────────────────────┐
           │   MindAtlas.Engine            │
           │   Ingest · Query · Lint · Idx │
           └───────────────────────────────┘
```

**로컬 우선 · 프라이버시 중심 · 오프라인 가능**

---

## 세 가지 접점

<div class="columns">

**🖥 Desktop**  
- Avalonia 네이티브 셸  
- 임베디드 서버 자동 구동  
- 트레이 · 단축키 · 드래그&드롭

**🌐 Web**  
- Blazor WASM  
- Wiki / Search / Query / Log  
- 어디서든 접속

</div>

**🔌 MCP**  
- Copilot, Claude Desktop 등에서  
  `mindatlas_query` / `ingest` / `lint` / `get_asset` 직접 호출

---

## 데이터 흐름

1. **Raw Source**  
   원문은 절대 잃어버리지 않음 (`IRawRepository`)
2. **Ingest Pipeline**  
   AI가 분할 → Wiki Page 후보 생성
3. **Wiki Repository**  
   페이지 · 인덱스 · 링크 그래프 저장
4. **Query**  
   자연어 → 관련 페이지 랭킹 → 인용된 답변
5. **Lint**  
   주기적 건강검진으로 **스스로 정리**

---

<!-- _class: lead -->

# 5. 지금 바로 시작하기

---

## 세 가지 시작법

<div class="columns">

**① 데스크톱 앱**
```powershell
dotnet publish `
  src/MindAtlas.Desktop `
  -p:PublishProfile=win-x64
```
설치 후 트레이 아이콘 → 붙여넣기.

**② 개발 모드**
```powershell
./run.ps1
```
Server + Web 자동 기동,  
`localhost:5001` 접속.

</div>

**③ MCP 연동 (Copilot / Claude Desktop)**
```json
{ "command": "MindAtlas.Server", "args": ["--mcp-stdio"] }
```

---

## 처음 30분 로드맵

1. 평소 쓰던 **메모 한 덩어리**를 ingest
2. 생성된 Wiki 페이지들 훑어보기
3. `/lint` 돌려 구조 확인
4. 궁금한 것 한 줄 질의
5. **MCP 연결** 후 AI 도구가 Wiki를 읽게 만들기

> 5단계만 지나도 **"왜 이걸 진작 안 했지"** 가 됩니다.

---

<!-- _class: lead -->

# 마무리

---

## 핵심 메시지

- 지식은 쌓이는 게 아니라 **연결되어야** 한다.
- 연결을 **사람이** 하면 지속 불가능하다.
- MindAtlas는 **AI가 연결하는 Wiki**다.
- 개발자·작가·유튜버 모두  
  **"내가 쌓은 것을 다시 쓸 수 있게"** 만든다.

<br>

> **당신의 머릿속이 검색 가능해진다면, 무엇을 만들 수 있을까?**

---

<!-- _class: lead -->

# 감사합니다
## Questions?

**MindAtlas** — Knowledge that writes itself.
