import { cn } from '@/lib/utils';

interface SpinnerProps {
	className?: string;
}

/**
 * Spinner di caricamento monocromatico: eredita il colore dal testo
 * (`currentColor`), così può essere usato sia su pulsanti chiari che scuri.
 * La dimensione di default è 1rem (h-4 w-4) ed è sovrascrivibile via `className`.
 */
export function Spinner({ className }: SpinnerProps) {
	return (
		<span
			role="status"
			aria-label="Caricamento"
			className={cn(
				'inline-block h-4 w-4 shrink-0 rounded-full border-2 border-current border-t-transparent animate-spin',
				className,
			)}
		/>
	);
}
