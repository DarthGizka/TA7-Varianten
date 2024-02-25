<Query Kind="Program" />

/////////////////////////////////////////////////////////////////////////////////////////////////////////////
///// Werteinsetzer //////////////////////////////////////////////////////////////////////// 2024-02-25 /////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////

/** MT-sichere Klasse zum wiederholten Durchführen mehrerer Ersetzungen in einem Vorlagetext<br/><br/>
 *
 * Der Werteinsetzer analysiert während der Initialisierung den Vorlagetext und die übergebenen Ersetzungen.
 * Mit den so ermittelten Daten kann der finale Text für einen übergebenen Satz von Ersatztexten dadurch
 * erzeugt werden, daß abwechselnd Textstücke aus dem Vorlagetext (i.e. Abschnitte zwischen Platzhaltern) 
 * und die einzusetzenden Ersatztexte in den Zielpuffer kopiert werden. Die Ersatztexte werden dabei aus der
 * Ersatztextquelle über Variablennamen abgerufen, die bei der Definition der Ersetzungen vergeben wurden.
 */
public class Werteinsetzer
{
	// `interface` statt `delegate` zwecks einfacherer Handhabung (zur Anwendung muß nur jeweils die
	// Referenz auf die Ersatztextquelle übergeben werden, nicht zusätzlich noch ein Methodenname)
	/** Schnittstelle zum Abrufen von Ersatztexten über Variablennamen */
	public interface IErsatztextquelle
	{
		string Ersatztext_fuer_Variablenname (string variablenname);
	}

	/** eine Ersatztextquelle, welche die Ersatztexte in öffentlichen Feldern oder Properties bereitstellt;
	 * der Zugriff über Variablennamen erfolgt via Reflektion */
	public class ReflektierendeErsatztextquelle : IErsatztextquelle
	{
		public string Ersatztext_fuer_Variablenname (string variablenname)
		{
			var typ = GetType();
			var feld = typ.GetField(variablenname);

			if (feld != null)
				return (string) feld.GetValue(this);

			var property = typ.GetProperty(variablenname);

			if (property != null)
				return (string) property.GetValue(this, null);

			throw new ArgumentException($"'{variablenname}' ist weder ein Feld noch ein Property");
		}
	}

	/** [immutabel] vom Nutzer bereitzustellende Definition für eine vorzunehmende Ersetzung */
	public class Ersetzung
	{
		/** Name, unter dem der einzusetzende Text aus der Ersatztextquelle abgerufen wird */
		public readonly string Variablenname;       // e.g. "Rezeptindex"
		/** tatsächlich zu ersetzendes Textstück innerhalb von FragmentMitKontext */
		public readonly string Platzhalter;         // e.g. "1"
		/** Textstück, welches für die Zwecke dieser Ersetzung innerhalb des Vorlagetextes gefunden werden soll */
		public readonly string FragmentMitKontext;  // e.g. "<entry><fullUrl value='http://zrbj.eu/x/Bundle/1'/>"
		/** gibt an, ob mehrfache Vorkommen von FragmentMitKontext im Vorlagetext zulässig/beabsichtigt sind;
		 * falls ja, dann wird die Ersetzung für alle Fundstellen durchgeführt; falls nein, dann führt die
		 * Existenz von mehr als einer Fundstelle zu einer Explosion (ArgumentException) */
		public readonly bool   MehrfacheErsetzung;
		/** gibt an, ob ein Nichtauftreten von FragmentMitKontext stillschweigend übergangen werden soll oder nicht */
		public readonly bool   Optional;

		/** aus den Parametern ermittelt: Offset des Platzhalters innerhalb von FragmentMitKontext */
		public readonly int    Platzhalteroffset;

		public Ersetzung 
		(
			string Variablenname, 
			string Platzhalter, 
			string FragmentMitKontext, 
			bool mehrfach = false,
			bool optional = false )
		{
			if (FragmentMitKontext == null)
				FragmentMitKontext = Platzhalter;

			this.Variablenname      = Variablenname;
			this.Platzhalter        = Platzhalter;
			this.MehrfacheErsetzung = mehrfach;
			this.Optional           = optional;
			this.FragmentMitKontext = FragmentMitKontext;

			Platzhalteroffset = FragmentMitKontext.IndexOf(Platzhalter, StringComparison.Ordinal);

			if (Platzhalteroffset < 0)
				throw new ArgumentException($"Platzhalter '{Platzhalter}' nicht in FragmentMitKontext '{FragmentMitKontext}'");

			if (FragmentMitKontext.IndexOf(Platzhalter, Platzhalteroffset + 1) >= 0)
				throw new ArgumentException($"Platzhalter '{Platzhalter}' mehrfach in FragmentMitKontext '{FragmentMitKontext}'");
		}

		public Ersetzung (string Variablenname, string Platzhalter, bool mehrfach = false, bool optional = false)
			: this(Variablenname, Platzhalter, null, mehrfach, optional)
		{
		}
	}

	/** [immutabel] eine einzelne Fundstelle für eine Ersetzungsdefinition in einem gegebenen Vorlagetext */
	private class Einzelersetzung
	{
		// Definitionen sind unveränderlich; ergo keine Notwendigkeit, Werte dort herauszuziehen und immutabel zu cachen
		private readonly Ersetzung Definition;

		/** Position dieser Fundstelle von Definition.FragmentMitKontext innerhalb des Vorlagetextes */		
		public readonly int    Fragmentoffset;
		public          int    Fragmentende       => Fragmentoffset + Definition.FragmentMitKontext.Length;
		public          int    Platzhalteroffset  => Fragmentoffset + Definition.Platzhalteroffset;
		public          int    Platzhalterende    => Platzhalteroffset + Definition.Platzhalter.Length;		
		public          string Variablenname      => Definition.Variablenname;
		public          string FragmentMitKontext => Definition.FragmentMitKontext;  // für Fehlermeldungen

		public Einzelersetzung (Ersetzung definition, int offset)
		{
			Definition = definition;
			Fragmentoffset = offset;
		}

		/** zum Sortieren nach Fragmentoffset (notwendig zum Nachweis der Überlappungsfreiheit aller Fundstellen) */
		public static int vergleiche_Fragmentoffset (Einzelersetzung lhs, Einzelersetzung rhs)
		{
			return lhs.Fragmentoffset.CompareTo(rhs.Fragmentoffset);
		}
	}

	private readonly string m_Vorlagetext;
	private readonly Einzelersetzung[] m_Einzelersetzungen;

	public static Werteinsetzer fuer_Datei (string vorlagedatei, IEnumerable<Ersetzung> definitionen)
	{
		var vorlagetext = File.ReadAllText(vorlagedatei, Encoding.UTF8);

		return new Werteinsetzer(vorlagetext, definitionen);
	}

	public Werteinsetzer (string vorlagetext, IEnumerable<Ersetzung> definitionen, bool ok_falls_nichts_zu_ersetzen = false)
	{
		m_Vorlagetext = vorlagetext;

		var einzelersetzungen = new List<Einzelersetzung>();

		foreach (var definition in definitionen)
		{
			var fundstellen = 0;

			for (int offset = 0; ; ++fundstellen)
			{
				offset = vorlagetext.IndexOf(definition.FragmentMitKontext, offset, StringComparison.Ordinal);

				if (offset < 0)
					break;

				einzelersetzungen.Add(new Einzelersetzung(definition, offset));
				offset += definition.FragmentMitKontext.Length;
			}

			if (fundstellen == 0 && !definition.Optional)
				throw new InvalidOperationException($"Fragment '{definition.FragmentMitKontext}' ist nicht im Vorlagetext");

			if (fundstellen > 1 && !definition.MehrfacheErsetzung)
				throw new InvalidOperationException($"Fragment '{definition.FragmentMitKontext}' mehrfach im Vorlagetext");
		}

		if (einzelersetzungen.Count == 0 && !ok_falls_nichts_zu_ersetzen)
			throw new InvalidOperationException("Liste der resultierenden Ersetzungen ist leer");

		einzelersetzungen.Sort(Einzelersetzung.vergleiche_Fragmentoffset);

		var vorgaenger = einzelersetzungen.First();

		foreach (var nachfolger in einzelersetzungen.Skip(1))
		{
			if (nachfolger.Fragmentoffset < vorgaenger.Fragmentende)
			{
				throw new InvalidOperationException($"Überlappung bei Fragment '{nachfolger.FragmentMitKontext}'");
			}

			vorgaenger = nachfolger;
		}

		m_Einzelersetzungen = einzelersetzungen.ToArray();
	}

	/** setzt die Werte aus der Ersatztextquelle in den Vorlagetext ein und hängt das Ergebnis an den StringBuilder an */
	public void anhaenge_Text_fuer_Werte (StringBuilder ziel, IErsatztextquelle ersatztextquelle)
	{
		var vorlagetext = m_Vorlagetext;
		var fertig_bis = 0;

		foreach (var ersetzung in m_Einzelersetzungen)
		{
			var einzusetzender_text = ersatztextquelle.Ersatztext_fuer_Variablenname(ersetzung.Variablenname);

			ziel.Append(vorlagetext, fertig_bis, ersetzung.Platzhalteroffset - fertig_bis);
			ziel.Append(einzusetzender_text);
			fertig_bis = ersetzung.Platzhalterende;
		}

		ziel.Append(vorlagetext, fertig_bis, vorlagetext.Length - fertig_bis);
	}

	/** setzt die Werte aus der Ersatztextquelle in den Vorlagetext ein und gibt den resultierenden Text zurück */
	public string Text_fuer_Werte (IErsatztextquelle ersatztextquelle)
	{
		// je Ersetzung etwas zusätzlichen Platz reservieren, um unnötige Reallokation möglichst zu vermeiden
		var string_builder = new StringBuilder(m_Vorlagetext.Length + 13 * m_Einzelersetzungen.Length);

		anhaenge_Text_fuer_Werte(string_builder, ersatztextquelle);

		return string_builder.ToString();
	}
}
