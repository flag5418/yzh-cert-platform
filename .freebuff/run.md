# Preview Run Doc

## Services

### Frontend (Vite Dev Server)
- **Port**: 9990
- **URL**: http://localhost:9990
- **Start Command**: `cd src/server/Vue.NetCore/vol.web && npm run serve`
- **PID**: 31647

### Backend (.NET Core API)
- **Port**: 9992
- **URL**: http://localhost:9992
- **Start Command**: `cd src/server/Vue.NetCore/vol.api && dotnet run --project VOL.WebApi/VOL.WebApi.csproj --urls "http://localhost:9992"`
- **PID**: 98959

## Login Pages
- Admin Login: http://localhost:9990/#/login
- Auditor Login: http://localhost:9990/#/auditor-login

## Database
- Host: 127.0.0.1
- Port: 3307
- Database: yzh_cert_platform
- User: root
- Password: Yzh123456.

## Role Setup
Run the SQL script `DB/mysql/insert_roles.sql` to insert role data before testing login.
