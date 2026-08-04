import * as signalR from '@microsoft/signalr';
import { API_BASE, getAccessToken } from '@/lib/apiClient';

const APP_HUB_PATH = '/hubs/app';

let connection: signalR.HubConnection | null = null;

/**
 * Connessione singleton condivisa da RequestsContext (che la possiede: avvio/stop)
 * e da altri consumer come useCalendar (che si limitano a on/off sugli eventi).
 * Si ferma da sola al logout ('app:logout', dispatchato da AuthContext/apiClient),
 * indipendentemente da quale componente l'abbia creata o sia montato in quel momento.
 */
export function getAppHubConnection(): signalR.HubConnection {
	if (!connection) {
		connection = new signalR.HubConnectionBuilder()
			.withUrl(`${API_BASE}${APP_HUB_PATH}`, {
				accessTokenFactory: () => getAccessToken() ?? '',
				// Il client SignalR manda credentials 'include' di default: con la CORS
				// policy del backend (AllowAnyOrigin, niente AllowCredentials) il browser
				// blocca ogni risposta ("Failed to fetch"). Non servono cookie, l'auth
				// passa dal Bearer token via accessTokenFactory.
				withCredentials: false,
			})
			.withAutomaticReconnect()
			.configureLogging(signalR.LogLevel.Warning)
			.build();

		window.addEventListener('app:logout', () => {
			connection?.stop().catch(() => {});
		});
	}
	return connection;
}

export { signalR };
