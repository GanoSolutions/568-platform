using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO
{
	/// <summary>
	/// Request per copiare tutti i turni di una settimana sorgente su ogni settimana
	/// compresa nel periodo target, mantenendo la corrispondenza dei giorni
	/// (lunedì su lunedì, ecc.). I turni già presenti sulle date target vengono
	/// sovrascritti.
	/// </summary>
	public class ShiftCopyWeek
	{
		/// <summary>
		/// Lunedì della settimana da cui leggere i turni da copiare. I turni vengono
		/// recuperati dal database al momento della richiesta (non inviati dal
		/// client), per evitare di copiare dati non aggiornati.
		/// </summary>
		[Required]
		public DateOnly SourceWeekMonday { get; set; }
		/// <summary>
		/// Primo giorno del periodo su cui copiare la settimana sorgente. Deve
		/// essere un lunedì, altrimenti la richiesta viene rifiutata.
		/// </summary>
		[Required]
		public DateOnly TargetStartDate { get; set; }
		/// <summary>
		/// Ultimo giorno del periodo su cui copiare la settimana sorgente. Deve
		/// essere una domenica, altrimenti la richiesta viene rifiutata.
		/// </summary>
		[Required]
		public DateOnly TargetEndDate { get; set; }
	}

	/// <summary>
	/// Riepilogo dell'esito di una copia settimana, riportato per permettere
	/// all'utente di verificare cosa è stato effettivamente applicato.
	/// </summary>
	public class ShiftCopyWeekResult
	{
		/// <summary>Numero di turni creati su date che non avevano già un turno.</summary>
		public int Created { get; set; }
		/// <summary>Numero di turni che hanno sostituito un turno già esistente sulla stessa data/dipendente.</summary>
		public int Overwritten { get; set; }
		/// <summary>Numero di turni non copiati perché la data target cadeva su un giorno di chiusura.</summary>
		public int SkippedClosedDays { get; set; }
	}
}