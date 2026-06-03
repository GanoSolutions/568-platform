# API Migration: Supabase → Backend C#

Questo documento elenca tutte le chiamate Supabase presenti nel frontend,
divise in due categorie: quelle già migrabili subito perché il backend le ha
già implementate, e quelle che il backend deve ancora scrivere.

---

## ✅ API già pronte nel backend — migrabili subito

| Endpoint C# | Metodo | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `POST /auth/login` | Pubblica | `supabase.auth.signInWithPassword()` | `AuthContext.tsx` → `loginWithPassword()` |
| `POST /auth/refresh` | Pubblica | *(da aggiungere nel FE — non esiste ancora)* | `apiClient.ts` da creare |
| `POST /auth/logout` | `[Authorize]` | `supabase.auth.signOut()` | `AuthContext.tsx` → `logout()` |
| `GET /user` | `[Authorize]` | `supabase.from('profiles').select(...)` | `AuthContext.tsx` → `getProfileForUser()` |
| `GET /user/{id}` | `[Authorize]` | `supabase.from('profiles').select(...).eq('id', ...)` | `AuthContext.tsx` → `getProfileForUser()` |
| `POST /user/signup` | `[Authorize]` | `supabase.from('profiles').insert()` + `supabase.from('employees').insert()` | `useEmployees.ts` → `addEmployee()` |
| `POST /user/{id}/invite` | `[Authorize]` | `supabase.functions.invoke('send-invite', { profileId })` | `useEmployees.ts` → `sendInvite()` |
| `POST /user/invite/accept` | Pubblica | `supabase.auth.updateUser({ password })` + `supabase.from('profiles').update({ first_login_completed })` | `SetPassword.tsx` |

---

## ❌ API che il backend deve ancora scrivere

### 🔐 Auth

| Endpoint C# | Cosa fa | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `GET /auth/me` | Ritorna profilo completo dell'utente loggato (dai claims JWT) | `supabase.auth.getSession()` + `supabase.from('profiles').select(...)` | `AuthContext.tsx` → `refreshProfile()`, `SetPassword.tsx` |
| `POST /auth/change-password` | Cambia password da utente già autenticato | `supabase.auth.updateUser({ password })` | `SetPassword.tsx` (flusso cambio volontario) |

### 👥 Employees

| Endpoint C# | Cosa fa | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `GET /employees` | Lista dipendenti con dati Employee (fiscal code, phone, contract end, invited) | `supabase.from('profiles').select(...).join(employees)` | `useEmployees.ts` → `fetchEmployees()` |
| `PUT /employees/{id}` | Aggiorna nome, email, fiscal code, phone, contract end | `supabase.from('profiles').update()` + `supabase.from('employees').update()` | `useEmployees.ts` → `updateEmployee()` |
| `DELETE /employees/{id}` | Elimina dipendente (cascade su employee) | `supabase.from('profiles').delete().eq('id', id)` | `useEmployees.ts` → `deleteEmployee()` |
| `GET /employees/{id}/invite-link` | Genera e ritorna il link invito senza mandare email | `supabase.functions.invoke('send-invite', { profileId, copyOnly: true })` | `useEmployees.ts` → `copyInviteLink()` |

### 📅 Shifts (turni)

| Endpoint C# | Cosa fa | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `GET /shifts?from=&to=` | Carica tutti i turni di una settimana con le assignments | `supabase.from('shifts').select(...).gte().lte()` | `useCalendar.ts` → `loadWeekShifts()` |
| `POST /shifts` | Crea o aggiorna un turno + assignments in una transazione atomica | `supabase.from('shifts').upsert()` + `shift_assignments.delete()` + `shift_assignments.insert()` | `useCalendar.ts` → `persistShiftForDate()` |
| `DELETE /shifts/{id}` | Elimina un turno e le sue assignments | `supabase.from('shifts').delete().eq('work_date', ...)` | `useCalendar.ts` → `persistShiftForDate()` (quando shiftData è null) |
| `POST /shifts/copy-week` | Copia i turni di una settimana su un range di date in un'unica transazione | Loop di chiamate a `persistShiftForDate()` per ogni giorno×settimana | `useCalendar.ts` → `copyWeek()` |

### 🔄 Swap Requests (cambio turno)

| Endpoint C# | Cosa fa | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `GET /swap-requests` | Lista richieste filtrata per ruolo (admin vede tutto, employee vede le sue) | `supabase.from('swap_requests').select(...)` con `.or(...)` per employee | `RequestsContext.tsx` → `loadRequests()` / `reloadRequests()` |
| `POST /swap-requests` | Crea una o più richieste + notifiche push + email | `supabase.rpc('create_swap_request')` × N + `functions.invoke('send-push')` + `functions.invoke('send-email')` | `RequestsContext.tsx` → `createSwapRequest()` |
| `POST /swap-requests/{id}/respond` | Accetta o rifiuta + notifiche + swappa assignments se accepted | `supabase.rpc('respond_to_swap_request')` + `functions.invoke('send-push')` + `functions.invoke('send-email')` | `RequestsContext.tsx` → `respondToSwapRequest()` |

### 🔔 Push Notifications

| Endpoint C# | Cosa fa | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `POST /push/subscribe` | Registra una subscription Web Push per l'utente | `supabase.rpc('register_push_subscription', { endpoint, p256dh, auth, user_agent })` | `usePushNotifications.ts` → `enable()` / `registerIfGranted()` |
| `POST /push/unsubscribe` | Disattiva la subscription Push | `supabase.rpc('disable_push_subscription', { endpoint })` | `usePushNotifications.ts` → `disable()` |
| `GET /push/status` | Verifica se l'utente ha una subscription attiva | `supabase.from('push_subscriptions').select('id').eq('active', true)` | `usePushNotifications.ts` → `loadSubscriptionState()` |

### 📲 Telegram

| Endpoint C# | Cosa fa | Chiamata Supabase attuale | File frontend |
|---|---|---|---|
| `POST /telegram/link-token` | Genera un token one-time per collegare l'account Telegram | `supabase.rpc('generate_telegram_link_token')` | `Requests.tsx` → `handleGenerateTelegramLink()` |
| `POST /telegram/unlink` | Scollega Telegram dall'account utente | `supabase.rpc('unlink_telegram')` | `Requests.tsx` → `handleUnlinkTelegram()` |

### ⚡ Realtime (Supabase-specific)

Queste non sono semplici chiamate REST ma **subscriptions realtime** di Supabase.
Una volta migrati al backend, andranno sostituite con **polling** o **WebSocket/SSE**.

| Uso attuale | Scopo | File frontend |
|---|---|---|
| `supabase.channel('calendar-...').on('postgres_changes', ...)` | Aggiorna il calendario automaticamente quando cambiano turni o assignments | `useCalendar.ts` |
| `supabase.channel('swap-requests-...').on('postgres_changes', ...)` | Aggiorna la lista richieste in tempo reale | `RequestsContext.tsx` |

---

## 📊 Riepilogo

| Categoria | Pronti nel backend | Da scrivere nel backend |
|---|---|---|
| Auth | 3 | 2 |
| Employees | 2 | 4 |
| Shifts | 0 | 4 |
| Swap Requests | 0 | 3 |
| Push Notifications | 0 | 3 |
| Telegram | 0 | 2 |
| Realtime | — | 2 (polling/WS) |
| **Totale** | **5** | **20** |
