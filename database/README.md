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

The app creates the `accounts` table on first startup.

## Social Login

Register these development callback URLs in the provider consoles:

```text
http://127.0.0.1:5179/signin-kakao
http://127.0.0.1:5179/signin-naver
```

Set the provider secrets only in the terminal used to start the app:

```powershell
$env:Authentication__Kakao__ClientId='YOUR_KAKAO_REST_API_KEY'
$env:Authentication__Kakao__ClientSecret='YOUR_KAKAO_CLIENT_SECRET'
$env:Authentication__Naver__ClientId='YOUR_NAVER_CLIENT_ID'
$env:Authentication__Naver__ClientSecret='YOUR_NAVER_CLIENT_SECRET'
```

The game requests only the provider user identifier. It does not require email, real name, or phone number.

For local Kakao development, use the prompt-based helper so values are stored outside the project:

```powershell
cd D:\Projects\ForgeIdle
.\scripts\set-kakao-secrets.ps1
```
