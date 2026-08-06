import { Toast } from '@base-ui/react/toast';
import { toastManager } from '@/lib/toast';

const TYPE_STYLES: Record<string, string> = {
	error: 'border-red-200 bg-red-50 text-red-700',
	success: 'border-emerald-200 bg-emerald-50 text-emerald-700',
};

function ToastList() {
	const { toasts } = Toast.useToastManager();

	return toasts.map((toast) => (
		<Toast.Root
			key={toast.id}
			toast={toast}
			className={`pointer-events-auto flex items-start gap-3 rounded-xl border px-4 py-3 text-sm font-medium shadow-lg transition-all duration-200 data-starting-style:translate-y-2 data-starting-style:opacity-0 data-ending-style:opacity-0 ${TYPE_STYLES[toast.type ?? 'error'] ?? TYPE_STYLES.error}`}
		>
			<Toast.Title className="flex-1" />
			<Toast.Close aria-label="Chiudi" className="shrink-0 leading-none opacity-60 hover:opacity-100">
				✕
			</Toast.Close>
		</Toast.Root>
	));
}

export default function Toaster() {
	return (
		<Toast.Provider toastManager={toastManager} timeout={5000}>
			<Toast.Portal>
				<Toast.Viewport className="fixed inset-x-4 bottom-20 z-[60] flex flex-col gap-2 sm:inset-x-auto sm:right-4 sm:w-80">
					<ToastList />
				</Toast.Viewport>
			</Toast.Portal>
		</Toast.Provider>
	);
}
