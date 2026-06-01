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
- 실제 게임 상태는 MSSQL `dbo.ea_players` 테이블에 JSON으로 저장합니다.
- 강화 시도는 `dbo.ea_enhancement_attempts` 테이블에 별도로 누적합니다.
- 상태에 영향을 줄 수 있는 게임 요청은 성공과 실패 모두 `dbo.ea_game_action_logs` 테이블에 기록합니다.
- 기존 ForgeIdle 운영 DB와 충돌하지 않도록 새 테이블에는 `ea_` 접두사를 사용합니다.

## 이식된 게임 시스템

- 사냥터 12개와 관문 보스 11마리
- +30 무기 강화와 +15 이상 파괴 판정
- 파괴 시 +12 복구와 보호권
- 한국 시간 자정 기준 일일 자동 사냥
- 보스 처치마다 일일 자동 사냥 30분 증가
- 3초 간격 직접 사냥과 정예·황금 몬스터
- 레벨, 경험치, 4종 스탯
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
