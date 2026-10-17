// v4 - backend C#: payload in PascalCase, origin dinamico, dedup per richiesta
self.addEventListener('install', () => {
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(clients.claim())
})

// Pagina da aprire al click, in base al Type dell'evento. Oggi tutti vanno su
// /requests, ma resta pronta a differenziare se arrivano altri Type.
function urlForType(type) {
  switch (type) {
    case 'swap_request_created':
    case 'swap_request_responded':
    default:
      return '/requests'
  }
}

self.addEventListener('push', (event) => {
  if (!event.data) return

  const payload = event.data.json()
  // Il backend C# manda le chiavi del payload in PascalCase (Title/Body/Type/
  // SwapRequestId), non camelCase — il fallback copre eventuali invii legacy.
  const title = payload.Title ?? payload.title ?? 'Turni 568'
  const origin = self.location.origin
  const options = {
    body: payload.Body ?? payload.body ?? 'Hai una nuova notifica',
    icon: origin + '/logo.png',
    badge: origin + '/logo-badge.png',
    // Stesso tag = stessa richiesta: notifiche successive sulla stessa richiesta
    // si sostituiscono invece di accumularsi.
    tag: payload.SwapRequestId ?? payload.swapRequestId,
    data: {
      url: payload.url || urlForType(payload.Type),
    },
  }

  event.waitUntil(self.registration.showNotification(title, options))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const targetUrl = event.notification.data?.url || '/requests'

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if ('focus' in client) {
          client.navigate(targetUrl)
          return client.focus()
        }
      }

      if (clients.openWindow) {
        return clients.openWindow(targetUrl)
      }

      return undefined
    }),
  )
})
