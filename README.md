# ForgeIdle

ASP.NET Core와 SQL Server로 만든 모바일 대응 방치형 강화 웹게임 프로토타입입니다.

## 주요 기능

- 카카오 OAuth 로그인
- 직접 사냥과 서버 기록 기반 자동 사냥
- 한국 시간 자정 기준 자동 사냥 시간 초기화
- 장비 +30 강화, 실패 유지, +15 이상 파괴 및 보호권
- 사냥 경험치, 레벨, 스탯 분배
- 사냥터 12개와 보스 11개
- 게임 내 확률표와 사용자 안내 메뉴

## 로컬 실행

SQL Server Express의 `forgeidle` 데이터베이스를 준비한 뒤 연결 문자열을 환경 변수로 설정합니다.

```powershell
$env:FORGEIDLE_DB_CONNECTION="Server=localhost\SQLEXPRESS;Database=forgeidle;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run --urls http://127.0.0.1:5179
```

카카오 로그인 개발 키는 프로젝트 파일에 저장하지 않고 .NET user-secrets 또는 배포 환경 변수로 관리합니다.

```text
Authentication__Kakao__ClientId
Authentication__Kakao__ClientSecret
```

로컬 개발용 카카오 리다이렉트 URI:

```text
http://127.0.0.1:5179/signin-kakao
```
