# 568 Platform

Web app per la gestione dei turni settimanali del locale 568.

## Stack

| Layer | Tech |
| --- | --- |
| Frontend | React 19 + Vite + TypeScript |
| UI | Tailwind CSS v4 + Shadcn/ui |
| Routing | React Router v7 |
| Backend | .NET 10 WebAPI (C#) |
| ORM | Entity Framework Core + Migrations |
| Database | PostgreSQL 17 (Docker) |
| Auth | JWT (access token + refresh token) |
| Notifiche | Resend (email) + Telegram Bot (Edge Functions) |
| Deploy | Vercel (frontend) |
| Test Backend | xUnit (unit + integration) |
| Test Frontend | Vitest + Testing Library |

## Architettura

```text
┌─────────────────────┐        REST API        ┌─────────────────────────┐
│   Frontend (React)  │ ─────────────────────► │   Backend (.NET 10)     │
│                     │                         │   WebAPI  :8080         │
│  • UI Components    │ ◄───────────────────── │  • Controllers          │
│  • React Router     │        JSON             │  • Facades / Services   │
│  • HTTP calls       │                         │  • JWT Auth             │
└─────────────────────┘                         │  • EF Core              │
                                                 └───────────┬─────────────┘
                                                             │
                                                     ┌───────▼──────┐
                                                     │  PostgreSQL  │
                                                     │  :5432       │
                                                     └──────────────┘
```

Le notifiche email e Telegram rimangono gestite tramite **Supabase Edge Functions** (folder `supabase/functions/`). (todo: da rimuovere)

## Funzionalità

- Login / Logout con JWT (access token + refresh token)
- Registrazione tramite link di invito → impostazione password → accesso automatico
- Gestione dipendenti: crea, modifica, elimina, invita via link
- Ruoli: **Admin** / **Manager** / **Employee**
- Calendario turni settimanale con navigazione e copia settimana
- Creazione e modifica turni
- Richieste di cambio turno con stati Pending / Accepted / Rejected
- Notifiche email (Resend) e Telegram per eventi swap e digest settimanale
- Layout mobile-first / PWA-ready

## Struttura del progetto

```text
backend/
  WebAPI/                     # .NET 10 WebAPI
    Controllers/              # AuthController, UserController
    Facades/                  # Logica orchestrazione
    Services/                 # AuthService, JwtService, UserService
    Migrations/               # EF Core migrations
    Models/                   # Entità DB
    appsettings.json
    appsettings.Development.json
  Five68Initializer/          # Tool di seed del database
  tests/
    Five68.UnitTests/
    Five68.IntegrationTests/
  Dockerfile
frontend/
  src/
    __tests__/
    components/
      calendar/
      employees/
      modals/
      ui/
    context/
      AuthContext.tsx
      RequestsContext.tsx
    hooks/
    lib/
    pages/
supabase/
  functions/
    send-email/
    send-invite/
    telegram-webhook/
    weekly-digest/
  schema.sql
docker-compose.yml
```

---

## Setup e avvio

### Prerequisiti

- [Docker](https://www.docker.com/) e Docker Compose
- Node.js 18+ e npm 9+ (solo per il frontend)
- .NET 10 SDK (opzionale, solo per sviluppo backend fuori Docker)

---

### 1. Backend + Database (Docker)

Tutti i servizi backend girano tramite Docker Compose.

#### Configurazione

Prima di avviare, assicurati che il file `backend/WebAPI/appsettings.Development.json` esista con i valori corretti. Il file di default (già presente nel repo) punta al database Docker con le credenziali di sviluppo:

> **Attenzione:** non committare mai `appsettings.Development.json` con segreti reali. Il file è già in `.gitignore`.

#### Avvio

```bash
# Avvia database + backend API
docker compose up --build

# Oppure in background
docker compose up --build -d
```

Il backend sarà disponibile su `http://localhost:8080`.
La Swagger UI è accessibile su `http://localhost:8080/swagger` (solo in ambiente Development).

#### Fermare i servizi

```bash
docker compose down

# Per eliminare anche il volume del database
docker compose down -v
```

---

### 2. Database Initializer (seed)

Il **Five68Initializer** è un tool separato che:

- Elimina e ricrea lo schema del database
- Inserisce dati di seed (admin, manager, dipendenti di esempio, turni, swap request)

> **Usare solo in ambiente di sviluppo/testing. Cancella tutti i dati esistenti.**

```bash
docker compose run --build --rm db_init
```

---

### 3. Frontend

```bash
cd frontend
npm install
```

Crea il file `frontend/.env.local` (non committare):

```bash
VITE_SUPABASE_URL=https://<project-ref>.supabase.co
VITE_SUPABASE_ANON_KEY=<anon-key>
# Opzionale - Web Push
VITE_VAPID_PUBLIC_KEY=<vapid-public-key>
```

Avvio:

```bash
cd frontend
npm run dev
```

---

## Test

### Backend

I test girano in Docker tramite i profili `test`:

```bash
# Unit test
docker compose run --build --rm unit-tests

# Integration test (richiede il DB healthy)
docker compose run --build --rm integration-tests
```

I test di integrazione usano `Five68WebAppFactory` per avviare un server in-process con un database PostgreSQL reale.

### Frontend

```bash
cd frontend
npm test           # run once
npm run test:watch # modalità watch
```

---

## API principali

| Metodo | Endpoint | Descrizione | Auth |
| --- | --- | --- | --- |
| `POST` | `/api/auth/login` | Login, restituisce access + refresh token | No |
| `POST` | `/api/auth/logout` | Logout, invalida il refresh token | Sì |
| `POST` | `/api/auth/refresh` | Rinnova l'access token | No |
| `POST` | `/api/auth/signup` | Registrazione tramite invite token | No |
| `GET` | `/api/user/me` | Profilo utente corrente | Sì |
| `GET` | `/api/user` | Lista utenti (admin/manager) | Sì |
| `POST` | `/api/user/invite` | Genera link invito per un nuovo utente | Sì |

La documentazione completa è disponibile via Swagger su `http://localhost:8080/swagger`.

---

## Data model

### Tabelle principali

| Tabella | Descrizione |
| --- | --- |
| `Users` | Profili, ruoli (`Admin`, `Manager`, `Employee`), stato, hash password |
| `Employees` | Dati dipendente (es. codice fiscale), chiave esterna su `Users` |
| `Shifts` | Un turno per data, creato da un utente |
| `ShiftAssignments` | Assegnazione dipendente a un turno |
| `SwapRequests` | Richieste di cambio turno tra dipendenti |
| `RefreshTokens` | Token di rinnovo JWT per sessione utente |

### Modello auth

- `Users.Id` è l'ID interno dell'utente nell'applicazione
- L'autenticazione è gestita dal backend .NET con JWT
- Il refresh token è salvato in `RefreshTokens` ed è monouso (rotation)

---

## Supabase Edge Functions (Notifiche)

Le notifiche email e Telegram sono gestite da Supabase Edge Functions.

Deploy:

```bash
npx supabase functions deploy <nome-funzione> --project-ref <ref> --use-api
```

Secrets da configurare in **Supabase Dashboard → Functions → Secrets**:

| Secret | Descrizione |
| --- | --- |
| `RESEND_API_KEY` | Chiave API Resend |
| `TELEGRAM_BOT_TOKEN` | Token del bot Telegram |
| `TELEGRAM_WEBHOOK_SECRET` | Header secret per il webhook Telegram |
| `SUPABASE_SERVICE_ROLE_KEY` | Chiave service role (disponibile di default) |
| `APP_URL` | URL dell'app (es. `https://568-platform.vercel.app`) |

---

## Deploy Frontend

```bash
npx vercel --prod
```

---

## Note

- `appsettings.Development.json` e `.env.local` non devono mai essere committati
- Il `docker-compose.yml` monta `./io/database` come volume persistente per PostgreSQL; cancellalo manualmente per ripartire da zero senza usare l'initializer
- Il `Five68Initializer` chiama `EnsureDeleted()` + `EnsureCreated()`: non usarlo mai su dati reali
