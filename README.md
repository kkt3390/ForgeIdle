# 강화중독 Web Forms

기존 ForgeIdle 운영 버전을 보존하면서 새로 만드는 ASP.NET Web Forms 기반 SPA 프로젝트입니다. 카카오 로그인을 거쳐 MSSQL에 계정별 진행 상황과 게임 데이터 변경 로그를 저장합니다.

## 목표

- 화면은 `Default.aspx` 한 페이지에서 유지합니다.
- 버튼 동작은 JavaScript가 `.ashx` API를 호출하여 처리합니다.
- 서버 코드는 익숙한 `.cs` 파일에서 직접 수정할 수 있습니다.
- 주석, 커밋 메시지, 패치노트는 한글로 작성합니다.

## 주요 파일

| 파일 | 역할 |
| --- | --- |
| `Default.aspx` | SPA 화면 구조 |
| `Scripts/game.js` | 화면 갱신과 API 호출 |
| `Api/GameApi.ashx.cs` | API 요청 분기 |
| `Game/GameCatalog.cs` | 사냥터 보상과 강화 확률 |
| `Game/GameService.cs` | 사냥과 강화 처리 |
| `Game/PlayerState.cs` | 사용자 상태 |
| `Data/PlayerRepository.cs` | MSSQL 조회와 저장 |
| `database/setup.sql` | 로컬 DB와 전용 테이블 생성 |
| `database/verify_legacy_migration.sql` | 이전 데이터가 빠짐없이 옮겨졌는지 조회 |
| `database/cleanup_legacy_tables.sql` | 검증을 통과한 이전 테이블을 수동 삭제 |
| `database/view_player_states.sql` | 운영 플레이어의 핵심 상태를 일반 컬럼으로 조회 |
| `Content/monsters` | 도감용 512x512 WebP 몬스터 이미지 |

## 로컬 MSSQL 생성

아래 명령을 한 번 실행하면 `enhance_addiction` DB가 생성됩니다.

```powershell
sqlcmd -S .\SQLEXPRESS -E -i D:\Projects\EnhanceAddiction.WebForms\database\setup.sql
```

DB 파일은 C 드라이브 공간을 사용하지 않도록 아래 경로에 생성됩니다.

```text
D:\SqlData\enhance_addiction.mdf
D:\SqlData\enhance_addiction_log.ldf
```

## 저장 방식

- 브라우저 세션에는 플레이어 조회용 키만 저장합니다.
- 카카오 로그인 계정은 `dbo.ea_social_accounts` 테이블에서 플레이어와 연결합니다.
- 닉네임, 골드, 강화도, 레벨, 경험치, 스탯과 자동 사냥 상태는 `dbo.ea_players`의 일반 컬럼에 저장합니다.
- `StateJson`은 최근 메시지와 이전 버전 호환을 위한 보조 사본으로 함께 저장합니다.
- 강화 시도는 `dbo.ea_enhancement_attempts` 테이블에 별도로 누적합니다.
- 상태에 영향을 줄 수 있는 게임 요청은 성공과 실패 모두 `dbo.ea_game_action_logs` 테이블에 기록합니다.
- 기존 ForgeIdle 운영 DB와 충돌하지 않도록 새 테이블에는 `ea_` 접두사를 사용합니다.
- 서버 시작 시 기존 `dbo.accounts`가 발견되면 사용자 상태와 강화 이력을 `ea_` 테이블로 한 번만 자동 이전합니다.
- 기존 JSON 중심 플레이어는 첫 조회 때 일반 컬럼 구조로 자동 동기화합니다.

## 도감 공개

도감 이미지와 운영 준비가 끝난 뒤 아래 환경 변수를 `true`로 변경하면 재배포 없이 도감 탭, 안내 확률표와 등록 판정이 함께 활성화됩니다.

```text
ENHANCE_ADDICTION_COLLECTION_ENABLED=true
```

## 이식된 게임 시스템

- 사냥터 12개와 관문 보스 11마리
- +30 무기 강화와 +15 이상 파괴 판정
- 파괴 시 +12 복구와 보호권
- 한국 시간 자정 기준 일일 자동 사냥
- 보스 처치마다 일일 자동 사냥 10분 증가
- 1초 간격 직접 사냥과 정예·황금 몬스터
- 레벨, 경험치, 4종 스탯
- 직접 사냥터 선택과 지역별 몬스터 도감
- 닉네임과 실시간 랭킹
- 개인 최근 기록 최대 100줄

## 카카오 로그인과 운영 환경 변수

비밀값은 저장소에 커밋하지 않고 로컬 IIS Express 또는 운영 서버 환경 변수로만 주입합니다.

- `Authentication__Kakao__ClientId`: 카카오 REST API 키
- `Authentication__Kakao__ClientSecret`: 카카오 클라이언트 시크릿
- `ENHANCE_ADDICTION_DB_CONNECTION`: 운영 MSSQL 연결 문자열

기존 사이트를 바로 교체할 수 있도록 `FORGEIDLE_DB_CONNECTION`도 예비 연결 문자열로 읽습니다.

카카오 개발자 콘솔에 아래 콜백 주소를 등록합니다.

- 로컬: `http://localhost:5180/Auth/KakaoCallback.ashx`
- 운영: `https://forgeidle.runasp.net/Auth/KakaoCallback.ashx`

## 감사 로그

메뉴 이동처럼 게임 데이터에 영향을 주지 않는 행동은 기록하지 않습니다. 아래처럼 게임 상태를 바꿀 수 있는 요청은 행동 전후 상태와 함께 기록합니다.

- 직접 사냥
- 닉네임 변경
- 자동 사냥 시작과 정산
- 강화
- 보스 도전
- 스탯 투자와 초기화

최근 행동을 확인하는 SQL:

```sql
SELECT TOP (100)
    Id, PlayerKey, ActionType, Succeeded, Message, CreatedAt
FROM dbo.ea_game_action_logs
ORDER BY Id DESC;
```

## 유지보수 규칙

- C# 메소드와 JavaScript 함수 위에는 담당 기능을 설명하는 한글 주석을 둡니다.
- 여러 동작을 한 줄에 압축하지 않고 수정하기 쉬운 형태로 줄을 나눕니다.
- 운영 DB 테이블 삭제는 서버 시작 시 자동으로 처리하지 않습니다.
- 이전 데이터 정리 전에는 `database/verify_legacy_migration.sql`을 실행하고 누락 건수가 0인지 확인합니다.
- 확인 후에만 `database/cleanup_legacy_tables.sql`을 수동 실행합니다.

강화 단계별 실제 확률을 확인하는 SQL:

```sql
SELECT
    BeforeLevel,
    COUNT(*) AS Attempts,
    AVG(SuccessRate) * 100 AS ExpectedSuccessPercent,
    SUM(CASE WHEN Result = N'Success' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS ActualSuccessPercent,
    AVG(KeepRate) * 100 AS ExpectedKeepPercent,
    SUM(CASE WHEN Result = N'Keep' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS ActualKeepPercent,
    AVG(DestroyRate) * 100 AS ExpectedDestroyPercent,
    SUM(CASE WHEN Result = N'Destroyed' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS ActualDestroyPercent
FROM dbo.ea_enhancement_attempts
GROUP BY BeforeLevel
ORDER BY BeforeLevel;
```

## 빌드

```powershell
E:\Microsoft\MSBuild\Current\Bin\MSBuild.exe EnhanceAddiction.WebForms.sln /t:Build /p:Configuration=Debug
```
