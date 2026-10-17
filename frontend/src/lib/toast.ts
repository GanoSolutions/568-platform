import { Toast } from '@base-ui/react/toast';

export type ToastType = 'error' | 'success';

export const toastManager = Toast.createToastManager();

export function showErrorToast(message: string): void {
	toastManager.add({ title: message, type: 'error' });
}

export function showSuccessToast(message: string): void {
	toastManager.add({ title: message, type: 'success' });
}
