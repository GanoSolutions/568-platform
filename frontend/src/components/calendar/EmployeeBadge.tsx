interface EmployeeBadgeProps {
  name: string
  color: string
  timeLabel: string
  isCurrentUser: boolean
}

/** Returns #ffffff or #1e293b (slate-800) based on WCAG luminance threshold */
function getTextColor(hex: string): string {
	const r = parseInt(hex.slice(1, 3), 16);
	const g = parseInt(hex.slice(3, 5), 16);
	const b = parseInt(hex.slice(5, 7), 16);
	const toLinear = (c: number) => {
		const s = c / 255;
		return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
	};
	const L = 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b);
	return L > 0.179 ? '#1e293b' : '#ffffff';
}

export default function EmployeeBadge({ name, color, timeLabel, isCurrentUser }: EmployeeBadgeProps) {
	const textColor = getTextColor(color);
	return (
		<span
			className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold shadow-sm whitespace-nowrap ${
				isCurrentUser ? 'ring-2 ring-offset-1 ring-white' : ''
			}`}
			style={{ backgroundColor: color, color: textColor }}
		>
			{name}
			<span className="font-normal opacity-90">{timeLabel}</span>
		</span>
	);
}
