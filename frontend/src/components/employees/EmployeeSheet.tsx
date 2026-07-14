import { useState, useEffect, type ReactNode } from 'react';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import type { EmployeeDetail } from '@/types';

interface EmployeeFormData {
	name: string
	fiscalCode: string
	email: string
	phone: string
	contractEnd: string
}

const EMPTY_FORM: EmployeeFormData = { name: '', fiscalCode: '', email: '', phone: '', contractEnd: '' };

function Field({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
	return (
		<div>
			<label className="text-slate-500 text-xs font-semibold uppercase tracking-wider block mb-1.5">
				{label}{required && <span className="text-red-400 ml-0.5">*</span>}
			</label>
			{children}
		</div>
	);
}

function Input({ value, onChange, type = 'text', placeholder, autoComplete }: { value: string; onChange: (value: string) => void; type?: string; placeholder?: string; autoComplete?: string }) {
	return (
		<input
			type={type}
			value={value}
			onChange={e => onChange(e.target.value)}
			placeholder={placeholder}
			autoComplete={autoComplete}
			className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 text-sm text-slate-800 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-400/20 transition"
		/>
	);
}

interface EmployeeSheetProps {
	employee: EmployeeDetail | null
	onSave: (data: EmployeeFormData) => Promise<void>
	onClose: () => void
	saveError: string
}

export default function EmployeeSheet({ employee, onSave, onClose, saveError }: EmployeeSheetProps) {
	const isEdit = !!employee;
	const [form, setForm] = useState<EmployeeFormData>(isEdit ? {
		name: employee.name,
		fiscalCode: employee.fiscalCode,
		email: employee.email ?? '',
		phone: employee.phone,
		contractEnd: employee.contractEnd ?? '',
	} : EMPTY_FORM);
	const [errors, setErrors] = useState<Record<string, string>>({});
	const [submitting, setSubmitting] = useState(false);

	useEffect(() => {
		if (!employee) return;

		setForm({
			name: employee.name,
			fiscalCode: employee.fiscalCode,
			email: employee.email ?? '',
			phone: employee.phone,
			contractEnd: employee.contractEnd ?? '',
		});
	}, [employee]);

	const set = (field: keyof EmployeeFormData) => (value: string) => setForm(prev => ({ ...prev, [field]: value }));

	const validate = () => {
		const e: Record<string, string> = {};
		if (!form.name.trim()) e.name = 'Campo obbligatorio';
		if (!form.fiscalCode.trim()) e.fiscalCode = 'Campo obbligatorio';
		if (!form.email.trim()) e.email = 'Campo obbligatorio';
		else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) e.email = 'Email non valida';
		if (!form.phone.trim()) e.phone = 'Campo obbligatorio';
		setErrors(e);
		return Object.keys(e).length === 0;
	};

	const handleSave = async () => {
		if (!validate()) return;

		setSubmitting(true);
		try {
			await onSave({
				name: form.name.trim(),
				fiscalCode: form.fiscalCode.trim().toUpperCase(),
				email: form.email.trim().toLowerCase(),
				phone: form.phone.trim(),
				contractEnd: form.contractEnd,
			});
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<Sheet open onOpenChange={onClose}>
			<SheetContent side="bottom" className="rounded-t-2xl max-h-[92vh] overflow-y-auto px-5 py-6">
				<SheetHeader className="mb-5">
					<SheetTitle className="text-slate-800">
						{isEdit ? 'Modifica dipendente' : 'Nuovo dipendente'}
					</SheetTitle>
				</SheetHeader>

				<div className="space-y-4">
					<Field label="Nome e cognome" required>
						<Input value={form.name} onChange={set('name')} placeholder="Mario Rossi" autoComplete="name" />
						{errors.name && <p className="text-red-400 text-xs mt-1">{errors.name}</p>}
					</Field>

					<Field label="Codice fiscale" required>
						<Input value={form.fiscalCode} onChange={set('fiscalCode')} placeholder="RSSMRA80A01H501Z" autoComplete="off" />
						{errors.fiscalCode && <p className="text-red-400 text-xs mt-1">{errors.fiscalCode}</p>}
					</Field>

					<Field label="Email" required>
						<Input value={form.email} onChange={set('email')} type="email" placeholder="mario@email.com" autoComplete="email" />
						{errors.email && <p className="text-red-400 text-xs mt-1">{errors.email}</p>}
					</Field>

					<Field label="Numero di cellulare" required>
						<Input value={form.phone} onChange={set('phone')} type="tel" placeholder="3331234567" autoComplete="tel" />
						{errors.phone && <p className="text-red-400 text-xs mt-1">{errors.phone}</p>}
					</Field>

					<Field label="Data fine contratto">
						<Input value={form.contractEnd} onChange={set('contractEnd')} type="date" />
						<p className="text-slate-400 text-xs mt-1">Lascia vuoto se a tempo indeterminato</p>
					</Field>

					{saveError && (
						<div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
							{saveError}
						</div>
					)}
				</div>

				<SheetFooter className="mt-6 flex gap-2">
					<Button variant="outline" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm">Annulla</Button>
					<Button onClick={handleSave} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm bg-indigo-500 hover:bg-indigo-400 text-white disabled:opacity-70 flex items-center justify-center gap-2">
						{submitting ? (
							<><Spinner /> Salvataggio...</>
						) : 'Salva dati'}
					</Button>
				</SheetFooter>
			</SheetContent>
		</Sheet>
	);
}
