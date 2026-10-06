# TaskHub

Project/task management app built to the spec in `Project.md`: a versioned ASP.NET Core REST API (EF Core, Scalar, JWT + 2FA, multi-database support) and an Angular + PrimeNG client (i18n, light/dark/auto theme, responsive layout), styled to match `F:\My_project\AirMonitor\frontend`'s conventions (PrimeNG Aura theme, top menubar, BEM-ish component classes, `pi pi-*` icons).

## Structure

```
src/
  Backend/
    TaskHub.sln
    TaskHub.Domain/          entities, enums
    TaskHub.Application/     DTOs, service interfaces/implementations, AutoMapper profile
    TaskHub.Infrastructure/  EF Core DbContext, Identity, JWT, DB-provider selection, seeding
    TaskHub.Api/              controllers, Program.cs, appsettings
  Client/
    taskhub-client/           Angular 19 standalone app
```

## Backend

**Run:**
```
cd src/Backend
dotnet run --project TaskHub.Api
```

On first run, if the configured database doesn't exist yet, it is created automatically and seeded with:
- roles `Admin`, `User`
- default admin user `admin` / `admin` (Identity password policy is relaxed so this works out of the box — change it after first login)

**Database provider** — set in `TaskHub.Api/appsettings.json`:
```json
"Database": {
  "Provider": "Sqlite",   // Sqlite | Postgresql | MySql | MsSql
  "ConnectionString": "Data Source=taskhub.db"
}
```
Example connection strings:
- Postgresql: `Host=localhost;Database=taskhub;Username=postgres;Password=postgres`
- MySql: `Server=localhost;Database=taskhub;User=root;Password=root`
- MsSql: `Server=localhost;Database=taskhub;Trusted_Connection=True;TrustServerCertificate=True`

**JWT** — set a real `Jwt:SigningKey` (32+ chars) before any non-local deployment.

**API docs** — Scalar UI at `/scalar/v1` in Development; versioned routes under `/api/v1/...` (Asp.Versioning, URL-segment versioning).

**Auth flow** — `POST /api/v1/auth/login` returns either an access token, or `requiresTwoFactor: true` with a short-lived `twoFactorToken` to pass to `POST /api/v1/auth/2fa/verify` along with the authenticator code. 2FA is TOTP-based (`/auth/2fa/setup`, `/auth/2fa/enable`, `/auth/2fa/disable`), compatible with any standard authenticator app.

**Domain model** — ProjectGroup → Project → Task → Subtask (project groups and subtasks are optional, subtasks one level deep; a task with subtasks cannot be started or completed directly — its status follows the subtasks and it is done once all of them are done), with per-project membership (Owner/Editor/Viewer) and separate cross-project UserGroups. An Admin role bypasses membership checks. A project can be deactivated from its edit form (owner or admin): it stays on the Projects page, marked as inactive, but its tasks no longer appear in the cross-project task list, dashboard, calendar or reports.

**Attachments** — tasks can carry file attachments. Metadata lives in the database; the files go to the storage chosen in `AttachmentStorage` in `appsettings.json` (`FileSystem` or `Ftp`) and are stored once under their SHA-256 hash, which is also the lookup key (`GET /api/v1/attachments/{sha256}?taskId={taskId}`). The lookup requires the id of a task the file is attached to, and access is checked through that task's project.

**Audit and security** — every login attempt is stored in `LoginLogs` (user name, IP address, browser, result) and every modifying request of a signed-in user in `UserActivityLogs` (action, path, status code, IP address). Administrators read both under `GET /api/v1/admin/logs/logins` and `GET /api/v1/admin/logs/activity`, and in the admin panel. Accounts are locked for a set time after a set number of failed logins; the defaults come from `Security` in `appsettings.json` and can be changed in the admin panel (`/api/v1/admin/security`, stored in `SystemSettings`). Requests are rate limited per user or IP address (`RateLimiting` in `appsettings.json`), with a tighter limit on login, registration and 2FA; exceeding it returns `429`. Behind a reverse proxy, configure forwarded headers, otherwise the proxy's address is logged and limited instead of the client's.

**Google sign-in** — set `GoogleAuth:ClientId` in `appsettings.json` to an OAuth 2.0 *Web application* client ID from Google Cloud Console and add the address the Angular app is served from (for example `http://localhost:4200`) to its *Authorized JavaScript origins*. A user first links a Google account in their profile; after that the login page accepts that Google account. An unlinked Google account cannot log in, even if its e-mail matches a user. With an empty client ID the Google buttons are hidden.

## Frontend

**Run:**
```
cd src/Client/taskhub-client
npm install
npm start
```

> **Note:** on machines where Hyper-V/WSL has reserved the 4200–4389 TCP range (check with `netsh int ipv4 show excludedportrange protocol=tcp`), the default Angular port 4200 will fail with `EACCES`. Use `npm start -- --port 4600` instead (already whitelisted in the API's CORS config alongside 4200 — see `Cors:AllowedOrigins` in `appsettings.json`).

The client expects the API at `http://localhost:5080/api/v1` (see `src/environments/environment.ts`); adjust if you run the API on a different port.

**Features implemented:**
- Login / register / 2FA verification, profile page to enable/disable 2FA
- Projects: create/edit/delete, members with roles, tasks with optional subtasks (each task card lists its subtasks)
- Calendar: month view of tasks by due date across all accessible projects
- User groups: independent from projects, for organizing users
- Admin module: role management, lockout, delete users (Admin role only)
- Language switcher (English/Polish, `ngx-translate`), light/dark/automatic theme (PrimeNG Aura's dark-mode selector wired to `prefers-color-scheme` + a manual toggle), responsive layout down to mobile widths
- UI library: PrimeNG (Aura theme) with a top `p-menubar`, matching `AirMonitor/frontend`'s look — `p-card`/`p-button`/`p-table`/`p-select` etc., PrimeNG `DialogService` for modals, `primeicons` throughout

## Verification performed

- Backend: full `dotnet build` succeeds; manually exercised via HTTP — register/login, full 2FA setup→enable→login→verify round trip, project/group/task CRUD, calendar query, CORS — all against a freshly seeded SQLite database.
- Frontend: `ng build` succeeds in both development and production configurations; dev server verified to serve the compiled app and reach the API (including CORS) over HTTP.
- Browser-level UI interaction was not exercised in this session (no connected browser instance available) — smoke-test the actual pages before relying on this in production, especially the PrimeNG `DialogService` modals (project/task/group/member forms) and the dark-mode toggle.
