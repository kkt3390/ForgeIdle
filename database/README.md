# ForgeIdle SQL Server Setup

## Local Database

Install SQL Server Express as the `SQLEXPRESS` named instance. Store database files under:

```text
D:\SqlData
```

Create the database by running:

```powershell
sqlcmd -S localhost\SQLEXPRESS -E -i D:\Projects\ForgeIdle\database\setup.sql
```

Start the app with Windows authentication:

```powershell
$env:FORGEIDLE_DB_CONNECTION='Server=localhost\SQLEXPRESS;Database=forgeidle;Trusted_Connection=True;TrustServerCertificate=True;'
dotnet run --urls http://127.0.0.1:5179
```

The app creates the `accounts` and `enhancement_attempts` tables on startup.

## Enhancement Statistics

Each enhancement attempt stores the applied probabilities, random roll, protection
ticket usage, and final result. Run this query in SSMS to compare configured and
actual rates by enhancement level:

```sql
SELECT
    BeforeLevel,
    COUNT(*) AS Attempts,
    AVG(AppliedSuccessRate) * 100 AS ExpectedSuccessPercent,
    SUM(CASE WHEN Result = N'Success' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS ActualSuccessPercent,
    AVG(AppliedKeepRate) * 100 AS ExpectedKeepPercent,
    SUM(CASE WHEN Result = N'Keep' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS ActualKeepPercent,
    AVG(AppliedDestroyRate) * 100 AS ExpectedDestroyPercent,
    SUM(CASE WHEN Result IN (N'Protected', N'Destroyed') THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS ActualDestroyRollPercent
FROM dbo.enhancement_attempts
GROUP BY BeforeLevel
ORDER BY BeforeLevel;
```

## Social Login

Register these development callback URLs in the provider consoles:

```text
http://127.0.0.1:5179/signin-kakao
```

Set the provider secrets only in the terminal used to start the app:

```powershell
$env:Authentication__Kakao__ClientId='YOUR_KAKAO_REST_API_KEY'
$env:Authentication__Kakao__ClientSecret='YOUR_KAKAO_CLIENT_SECRET'
```

The game requests only the provider user identifier. It does not require email, real name, or phone number.

For local Kakao development, use the prompt-based helper so values are stored outside the project:

```powershell
cd D:\Projects\ForgeIdle
.\scripts\set-kakao-secrets.ps1
```
