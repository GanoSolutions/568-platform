import { useState, useEffect, type ReactNode } from 'react';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import type { EmployeeDetail } from '@/types';

interface EmployeeFormData {
	name: string
	surname: string
	fiscalCode: string
	email: string
	phone: string
	contractEnd: string
}

const EMPTY_FORM: EmployeeFormData = { name: '', surname: '', fiscalCode: '', email: '', phone: '', contractEnd: '' };

/** Il nome completo (EmployeeDetail.name) meno il cognome, per precompilare il
 * campo "Nome" separato — name è sempre costruito come `${nome} ${cognome}`.trim()
 * (mapUserDTOToEmployee), quindi togliere gli ultimi surname.length caratteri
 * isola sempre il nome, anche se contiene spazi. */
function splitFirstName(fullName: string, surname: string): string {
	return fullName.slice(0, fullName.length - surname.length).trim();
}

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

function Input({ value, onChange, onBlur, type = 'text', placeholder, autoComplete }: { value: string; onChange: (value: string) => void; onBlur?: () => void; type?: string; placeholder?: string; autoComplete?: string }) {
	return (
		<input
			type={type}
			value={value}
			onChange={e => onChange(e.target.value)}
			onBlur={onBlur}
			placeholder={placeholder}
			autoComplete={autoComplete}
			className="w-full bg-slate-50 border border-slate-200 rounded-xl px-4 py-3 text-sm text-slate-800 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-400/20 transition"
		/>
	);
}

const PHONE_RE = /^[+]?[\d\s()-]{6,20}$/;

/** Valida un singolo campo — condivisa tra validate() (submit) e la validazione
 * dal vivo mentre si digita (richiesta da Hermann in review PR #68). */
function validateField(field: keyof EmployeeFormData, form: EmployeeFormData): string {
	switch (field) {
	case 'name': return form.name.trim() ? '' : 'Campo obbligatorio';
	case 'surname': return form.surname.trim() ? '' : 'Campo obbligatorio';
	case 'fiscalCode': return form.fiscalCode.trim() ? '' : 'Campo obbligatorio';
	case 'email':
		if (!form.email.trim()) return 'Campo obbligatorio';
		return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email) ? '' : 'Email non valida';
	case 'phone':
		if (!form.phone.trim()) return 'Campo obbligatorio';
		return PHONE_RE.test(form.phone.trim()) ? '' : 'Numero di telefono non valido';
	default: return '';
	}
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
		name: splitFirstName(employee.name, employee.surname),
		surname: employee.surname,
		fiscalCode: employee.fiscalCode,
		email: employee.email ?? '',
		phone: employee.phone,
		contractEnd: employee.contractEnd ?? '',
	} : EMPTY_FORM);
	const [errors, setErrors] = useState<Record<string, string>>({});
	const [touched, setTouched] = useState<Partial<Record<keyof EmployeeFormData, boolean>>>({});
	const [submitting, setSubmitting] = useState(false);

	useEffect(() => {
		if (!employee) return;

		setForm({
			name: splitFirstName(employee.name, employee.surname),
			surname: employee.surname,
			fiscalCode: employee.fiscalCode,
			email: employee.email ?? '',
			phone: employee.phone,
			contractEnd: employee.contractEnd ?? '',
		});
	}, [employee]);

	// onChange: aggiorna il valore e, se il campo è già stato "toccato" (blur o
	// tentativo di submit precedente), ne ricalcola subito l'errore — validazione
	// dal vivo mentre si digita, non solo al salvataggio (richiesta da Hermann).
	const set = (field: keyof EmployeeFormData) => (value: string) => {
		const next = { ...form, [field]: value };
		setForm(next);
		if (touched[field]) {
			setErrors(prev => ({ ...prev, [field]: validateField(field, next) }));
		}
	};

	const touch = (field: keyof EmployeeFormData) => () => {
		setTouched(prev => ({ ...prev, [field]: true }));
		setErrors(prev => ({ ...prev, [field]: validateField(field, form) }));
	};

	const validate = () => {
		const fields: (keyof EmployeeFormData)[] = ['name', 'surname', 'fiscalCode', 'email', 'phone'];
		const e: Record<string, string> = {};
		for (const field of fields) {
			const msg = validateField(field, form);
			if (msg) e[field] = msg;
		}
		setErrors(e);
		setTouched(Object.fromEntries(fields.map(f => [f, true])));
		return Object.keys(e).length === 0;
	};

	const handleSave = async () => {
		if (!validate()) return;

		setSubmitting(true);
		try {
			await onSave({
				name: form.name.trim(),
				surname: form.surname.trim(),
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
			{/* Solo il corpo scrolla (flex-1 min-h-0 overflow-y-auto): header, X e
			    footer restano sempre visibili anche a tastiera aperta / form lungo
			    (segnalato da Hermann in review). Larghezza limitata via wrapper
			    interno (mx-auto) perché il side=bottom del componente Sheet è
			    sempre full-width. */}
			<SheetContent side="bottom" className="rounded-t-2xl max-h-[92vh] p-0">
				<div className="w-full max-w-md mx-auto flex flex-col flex-1 min-h-0">
					<SheetHeader className="px-5 pt-6 pb-2 shrink-0">
						<SheetTitle className="text-slate-800">
							{isEdit ? 'Modifica dipendente' : 'Nuovo dipendente'}
						</SheetTitle>
					</SheetHeader>

					<div className="flex-1 min-h-0 overflow-y-auto px-5 space-y-4 pb-2">
						<Field label="Nome" required>
							<Input value={form.name} onChange={set('name')} onBlur={touch('name')} placeholder="Mario" autoComplete="given-name" />
							{errors.name && <p className="text-red-400 text-xs mt-1">{errors.name}</p>}
						</Field>

						<Field label="Cognome" required>
							<Input value={form.surname} onChange={set('surname')} onBlur={touch('surname')} placeholder="Rossi" autoComplete="family-name" />
							{errors.surname && <p className="text-red-400 text-xs mt-1">{errors.surname}</p>}
						</Field>

						<Field label="Codice fiscale" required>
							<Input value={form.fiscalCode} onChange={set('fiscalCode')} onBlur={touch('fiscalCode')} placeholder="RSSMRA80A01H501Z" autoComplete="off" />
							{errors.fiscalCode && <p className="text-red-400 text-xs mt-1">{errors.fiscalCode}</p>}
						</Field>

						<Field label="Email" required>
							<Input value={form.email} onChange={set('email')} onBlur={touch('email')} type="email" placeholder="mario@email.com" autoComplete="email" />
							{errors.email && <p className="text-red-400 text-xs mt-1">{errors.email}</p>}
						</Field>

						<Field label="Numero di cellulare" required>
							<Input value={form.phone} onChange={set('phone')} onBlur={touch('phone')} type="tel" placeholder="3331234567" autoComplete="tel" />
							{errors.phone && <p className="text-red-400 text-xs mt-1">{errors.phone}</p>}
						</Field>

						<Field label="Data fine contratto">
							<Input value={form.contractEnd} onChange={set('contractEnd')} type="date" />
							<p className="text-slate-500 text-xs mt-1">Lascia vuoto se a tempo indeterminato</p>
						</Field>

						{saveError && (
							<div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
								{saveError}
							</div>
						)}
					</div>

					<SheetFooter className="px-5 py-4 shrink-0 flex gap-2">
						<Button variant="outline" onClick={onClose} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm">Annulla</Button>
						<Button onClick={handleSave} disabled={submitting} className="flex-1 rounded-xl py-3 text-sm bg-indigo-500 hover:bg-indigo-400 text-white disabled:opacity-70 flex items-center justify-center gap-2">
							{submitting ? (
								<><Spinner /> Salvataggio...</>
							) : 'Salva dati'}
						</Button>
					</SheetFooter>
				</div>
			</SheetContent>
		</Sheet>
	);
}
