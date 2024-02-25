<Query Kind="Program">
  <Namespace>System.Security.Cryptography.X509Certificates</Namespace>
</Query>

/////////////////////////////////////////////////////////////////////////////////////////////////////////////
///// Rezeptgenerator ////////////////////////////////////////////////////////////////////// 2024-02-25 /////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////

#load ".\INC_TA7Generator_Hilfsfunktionen"
#load ".\INC_Werteinsetzer"

partial class Rezeptgenerator
{
	public static readonly Werteinsetzer.Ersetzung[] ERSETZUNGEN_VO =
	{
		new Werteinsetzer.Ersetzung("ERezeptId", "160.123.456.789.123.58", true),
		new Werteinsetzer.Ersetzung("BundleId", ".", "<id value=\".\"/>"),
		new Werteinsetzer.Ersetzung("Id", "1",       "<id value=\"1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1",       "Composition/1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1",           "Patient/1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1",      "Practitioner/1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1",      "Organization/1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1", "MedicationRequest/1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1",          "Coverage/1\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "1",        "Medication/1\"/>", true),
	};

	public static readonly Werteinsetzer.Ersetzung[] ERSETZUNGEN_GQ =
	{
		new Werteinsetzer.Ersetzung("ERezeptId"    , "160.123.456.789.123.58", true),
		new Werteinsetzer.Ersetzung("BundleId"     , "a0000001-0000-0000-0003-000000000000"),
		new Werteinsetzer.Ersetzung("UID_GQ_Binary", "a1bd4cd9-7f11-43e0-b485-3eb20ae52a42", mehrfach: true),
		new Werteinsetzer.Ersetzung("Base64_VOHASH", Rezeptgenerator.VERORDNUNGSHASH_IN_VORLAGEN)
		// Device-Eintrag braucht keine neue UUID (da immer die gleiche Entität referenzierend)
	};

	public static readonly Werteinsetzer.Ersetzung[] ERSETZUNGEN_AD =
	{
		new Werteinsetzer.Ersetzung("ERezeptId", "160.123.456.789.123.58", true),
		new Werteinsetzer.Ersetzung("Id", "3",        "<id value=\"3\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "3",        "Composition/3\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "3",       "Organization/3\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "3", "MedicationDispense/3\"/>", true),
		new Werteinsetzer.Ersetzung("Id", "3",            "Invoice/3\"/>", true),
	};
	
	public static readonly Werteinsetzer.Ersetzung[] ERSETZUNGEN_RB =
	{
		// xmlns-Attribut wird entfernt für Bündeleinträge (i.e. ersetzt durch Leerstring)
		new Werteinsetzer.Ersetzung("_xmlns"     , " xmlns=\"http://hl7.org/fhir\""),
		new Werteinsetzer.Ersetzung("ERezeptId"  , "160.123.456.789.123.58"),
		new Werteinsetzer.Ersetzung("Belegnummer", "2311000000011234561"),
		// nur bei URL-basierter Referenzierung:
		new Werteinsetzer.Ersetzung("BundleId", "1", "value=\"http://zrbj.eu/x/Bundle/1\"/>", optional: true),
		new Werteinsetzer.Ersetzung("BundleId", "1", "<id value=\"1\"/>", optional: true),
		// eingebettete Ressourcen:
		new Werteinsetzer.Ersetzung("Base64_CMS_VO", Rezeptgenerator.STUMMEL_BASE64_VO),
		new Werteinsetzer.Ersetzung("Base64_CMS_GQ", Rezeptgenerator.STUMMEL_BASE64_GQ),
		new Werteinsetzer.Ersetzung("Base64_CMS_AD", Rezeptgenerator.STUMMEL_BASE64_AD)
	};

	[Flags]
	public enum Stil
	{
		NULL,		
		/** gibt an, ob kleine Stummel für die Base64-kodierten Daten verwendet werden sollen; falls nicht dann
		 * werden für jedes Rezept die Daten für Verordnung, Quittung und Abgabedaten aus Vorlagen generiert */
		Base64Stummel   = 1,
		/** gibt an, absolute URLs im FHIR-Stil als `fullUrl` verwendet werden sollen anstelle von URN:UUID */
		FHIRlicheURLs   = 2,
		/** gibt an, daß UUIDs als resource.id verwendet werden sollen; bei URN:UUID als `fullUrl` bewirkt das
		 * auch die Emission der ansonsten unterdrückten `id`-Elemente */
		UUID_als_Id     = 4,
		/** XML aus Vorlagedateien wird entweder nur minifiziert (einschließlich Eliminierung von XML-Deklaration,
		 * Kommentaren und Processing Instructions) oder minifiziert und dann lesbar formatiert */
		XML_minifiziert = 8
	}
	
	/** Verordnung des Arztes */
	public readonly Werteinsetzer VO;
	/** Gematik-Quittung */
	public readonly Werteinsetzer GQ;
	/** Abgabedaten der Apotheke */
	public readonly Werteinsetzer AD;
	/** TA7-Rezeptbündel */
	public readonly Werteinsetzer RB;

	public readonly Stil StilBits;

	/** gibt an, ob kleine Stummel für die Base64-kodierten Daten verwendet werden sollen; falls nicht dann
	 * werden für jedes Rezept die Daten für Verordnung, Quittung und Abgabedaten aus Vorlagen generiert */
	public bool Base64Stummel  => (StilBits & Stil.Base64Stummel  ) != 0;
	public bool FHIRlicheURLs  => (StilBits & Stil.FHIRlicheURLs  ) != 0;
	public bool UUID_als_Id    => (StilBits & Stil.UUID_als_Id    ) != 0;
	public bool XMLMinifiziert => (StilBits & Stil.XML_minifiziert) != 0;

	public readonly X509Certificate2 P12_VO;
	public readonly X509Certificate2 P12_GQ;
	public readonly X509Certificate2 P12_AD;

	public Rezeptgenerator 
	(
		Werteinsetzer VO, 
		Werteinsetzer GQ, 
		Werteinsetzer AD, 
		Werteinsetzer RB,
		Stil StilBits,
		X509Certificate2 P12_VO = null,
	   	X509Certificate2 P12_GQ = null,
		X509Certificate2 P12_AD = null 
	)
	{
		this.VO = VO;
		this.GQ = GQ;
		this.AD = AD;
		this.RB = RB;
		this.StilBits = StilBits;
		this.P12_VO = P12_VO ?? (Base64Stummel ? null : TA7Generator.lade_P12("Arzt.p12"      )); 
		this.P12_GQ = P12_VO ?? (Base64Stummel ? null : TA7Generator.lade_P12("Fachdienst.p12")); 
		this.P12_AD = P12_VO ?? (Base64Stummel ? null : TA7Generator.lade_P12("Apotheke.p12"  )); 		
	}

	public static Rezeptgenerator mit_Standardwerten (Stil stilbits)
	{
		var fhirliche_urls = (stilbits & Stil.FHIRlicheURLs  ) != 0;
		var uuid_als_id    = (stilbits & Stil.UUID_als_Id    ) != 0;
		var minifiziert    = (stilbits & Stil.XML_minifiziert) != 0;

		var vorlage_vo = TA7Generator.lies_Vorlage("VO.xml", minifiziert);
		var vorlage_gq = TA7Generator.lies_Vorlage("GQ.xml", minifiziert);
		var vorlage_ad = TA7Generator.lies_Vorlage("AD.xml", minifiziert);
		var vorlage_rb = TA7Generator.lies_Vorlage("RB.xml", minifiziert);

		if (!fhirliche_urls && !uuid_als_id)
		{
			vorlage_rb = vorlage_rb
				.Replace("  <id value=\"1\"/>\r\n", "")
				.Replace("<id value=\"1\"/>", "");
		}

		// nacheinander initialisieren, damit bei fliegenden Exceptions die Quelle leichter zu finden ist
		var werteinsetzer_vo = new Werteinsetzer(vorlage_vo, ERSETZUNGEN_VO);
		var werteinsetzer_gq = new Werteinsetzer(vorlage_gq, ERSETZUNGEN_GQ);
		var werteinsetzer_ad = new Werteinsetzer(vorlage_ad, ERSETZUNGEN_AD);
		var werteinsetzer_rb = new Werteinsetzer(vorlage_rb, ERSETZUNGEN_RB);

		return new Rezeptgenerator
		(
			VO: werteinsetzer_vo,
			GQ: werteinsetzer_gq,
			AD: werteinsetzer_ad,
			RB: werteinsetzer_rb,
			stilbits
		);
	}

	private class Ersatztextquelle : Werteinsetzer.ReflektierendeErsatztextquelle
	{
		/** ' xmlns="http://hl7.org/fhir"' für freistehende Rezeptbündel, '' sonst */
		public string _xmlns;

		/** Id für das Bundle selbst, falls es eine hat (e.g. "vo-123", "ad-123" usw.) */
		public string BundleId;
		/** Id für im Bundle eingebettete Strukturen (ergibt Referenzen wie "Patient/vo-123" usw.); 
		 * derzeit de facto immer wertgleich mit BundleId, da jeder Ressource-Typ max. 1 x vorhanden */
		public string Id;

		/** e.g. "160.123.456.789.123.58" */
		public string ERezeptId;
		/** e.g. "2311000000011234561", also die neue 19stellige Form und nicht die 18stellige gem. TA1/TA3/TA4 */
		public string Belegnummer;

		/** UUID für den Quittungs-Eintrag mit dem Verordnungshash */
		public string UID_GQ_Binary;

		/** UUID für Bündeleintrag mit Verordnung */
		public string UID_VO;
		/** UUID für Bündeleintrag mit Gematik-Quittung */
		public string UID_GQ;
		/** UUID für Bündeleintrag mit Abgabedaten */
		public string UID_AD;
		/** UUID für Bündeleintrag mit Rechnungswerten (a.k.a. 'eAbrechnungsdaten') */
		public string UID_RW;
		/** UUID für das gesamte Rezeptbündel (falls über UUID statt URL referenziert) */
		public string UID_RB;

		/** signierte Verordnung als Base64 */
		public string Base64_CMS_VO;
		/** Hash der Verordnungsbytes, die auch an den Signierer weitergereicht wurden */
		public string Base64_VOHASH;
		/** signierte Quittung als Base64 */
		public string Base64_CMS_GQ;
		/** signierte Abgabedaten als Base64 */
		public string Base64_CMS_AD;
	}

	public const string VERORDNUNGSHASH_IN_VORLAGEN = TA7Generator.VERORDNUNGSHASH_IN_VORLAGEN;
	public const string STUMMEL_BASE64_VO           = TA7Generator.STUMMEL_BASE64_VO;
	public const string STUMMEL_BASE64_GQ           = TA7Generator.STUMMEL_BASE64_GQ;
	public const string STUMMEL_BASE64_AD           = TA7Generator.STUMMEL_BASE64_AD;

	// UIDs im Rezeptbündel
	internal const int UID_INDEXBASIS_VO = 1_000_000;
	internal const int UID_INDEXBASIS_GQ = 2_000_000;
	internal const int UID_INDEXBASIS_AD = 3_000_000;
	internal const int UID_INDEXBASIS_RW = 4_000_000;
	internal const int UID_INDEXBASIS_RB = 5_000_000;

	internal const int UID_INDEXBASIS_GQ_Binary = 6_000_000;

	public class Rezeptbuendel
	{
		/** zum Generieren der Daten verwendeter Index */
		public readonly int    Index;
		/** XML in Byte-Form (kompakter als die interne Unicode-Form) */
		public readonly byte[] XML;
		/** entweder URN:UUID oder absolute FHIRliche URL */
		public readonly string AbsoluteURL;
		/** relative Referenz (e.g. "Bundle/12345"), falls nicht via UUID-Variante */
		public readonly string RelativeURL;
		/** für alternatives Referenzierungschema */
		public readonly string Belegnummer;
		/** beim Aufruf der Generatorfunktion angegebener Wert (geht in Dump-Dateiname ein) */
		public readonly bool Freistehend;

		public string XMLText () => TA7Generator.UTF8_ohne_BOM.GetString(XML);

		public Rezeptbuendel 
		(
			int Index, 
			byte[] XML, 
			string AbsoluteURL, 
			string RelativeURL,
			string Belegnummer,
			bool   Freistehend
		)
		{
			this.Index       = Index;
			this.XML         = XML;
			this.AbsoluteURL = AbsoluteURL;
			this.RelativeURL = RelativeURL;
			this.Belegnummer = Belegnummer;
			this.Freistehend = Freistehend;
		}
	}

	public Rezeptbuendel generiere (int index, bool freistehend = false)
	{
		var ew = new Ersatztextquelle();
		var erezept_id = TA7Generator.korrigiere_Pruefziffern_in_ERezeptId((160_999_999_000_000 + index) * 100);

		ew.ERezeptId     = TA7Generator.Text_fuer_ERezeptId(erezept_id);
		ew.Belegnummer   = $"2311{index:D8}1234567";
		ew.UID_VO        = TA7Generator.UUID_fuer_Index(UID_INDEXBASIS_VO + index).ToString();
		ew.UID_GQ        = TA7Generator.UUID_fuer_Index(UID_INDEXBASIS_GQ + index).ToString();
		ew.UID_AD        = TA7Generator.UUID_fuer_Index(UID_INDEXBASIS_AD + index).ToString();
		ew.UID_RW        = TA7Generator.UUID_fuer_Index(UID_INDEXBASIS_RW + index).ToString();
		ew.UID_RB        = TA7Generator.UUID_fuer_Index(UID_INDEXBASIS_RB + index).ToString();
		ew.UID_GQ_Binary = TA7Generator.UUID_fuer_Index(UID_INDEXBASIS_GQ_Binary + index).ToString();

		if (Base64Stummel)
		{
			ew.Base64_CMS_VO = STUMMEL_BASE64_VO;
			ew.Base64_CMS_GQ = STUMMEL_BASE64_GQ;
			ew.Base64_CMS_AD = STUMMEL_BASE64_AD;
			ew.Base64_VOHASH = VERORDNUNGSHASH_IN_VORLAGEN;
		}
		else
		{
			ew.BundleId = ew.Id = $"vo-{index:D5}";

			var vo_xml = VO.Text_fuer_Werte(ew);
			// Bytes materialisieren zwecks Hashermittlung
			var vo_bytes = TA7Generator.UTF8_ohne_BOM.GetBytes(vo_xml);

			ew.Base64_CMS_VO = TA7Generator.CMS_als_Base64(vo_bytes, P12_VO);
			ew.Base64_VOHASH = TA7Generator.SHA256_als_Base64(vo_bytes);
			ew.BundleId = $"a{index + 1:X7}-0000-0000-0003-000000000000";

			var gq_xml = GQ.Text_fuer_Werte(ew);

			// wir könnten hier die Matrioschka erzeugen und dann das eingebettete CMS
			// daraus extrahieren, aber der direkte Weg ist einfacher & effizienter ...
			ew.Base64_CMS_GQ = TA7Generator.CMS_als_Base64(gq_xml, P12_GQ);
			ew.BundleId = ew.Id = $"ad-{index:D5}";

			var ad_xml = AD.Text_fuer_Werte(ew);

			ew.Base64_CMS_AD = TA7Generator.CMS_als_Base64(vo_bytes, P12_AD);
		}

		ew._xmlns = freistehend ? " xmlns=\"http://hl7.org/fhir\"" : string.Empty;

		string full_url, relative_url = null;

		if (FHIRlicheURLs)
		{
			ew.BundleId = ew.Id = UUID_als_Id ? ew.UID_RB : index.ToString("D5");
			relative_url = "Bundle/" + ew.BundleId;
			full_url = "http://zrbj.eu/x/" + relative_url;
		}
		else
		{
			ew.BundleId = ew.Id = ew.UID_RB;
			full_url = "urn:uuid:" + ew.BundleId;
		}

		var sb = new StringBuilder(10000);  // *fixme* durch Messung besseren Wert ermitteln

		if (!freistehend)
			sb.Append("<entry><fullUrl value=\"").Append(full_url).Append("\"/><resource>");

		RB.anhaenge_Text_fuer_Werte(sb, ew);

		if (!freistehend)
			sb.Append("</resource></entry>");

		return new Rezeptbuendel
		(
			Index      : index,
			XML        : TA7Generator.UTF8_ohne_BOM.GetBytes(sb.ToString()),
			AbsoluteURL: full_url,
			RelativeURL: relative_url,
			Belegnummer: ew.Belegnummer,
			Freistehend: freistehend
		);
	}

	public static string Dateiname_fuer_Dumps (Stil stilbits, int index, bool freistehend)
	{
		return $"Rezept{(freistehend ? "buendel" : "eintrag")}_S{(int) stilbits:X1}_{index:D5}.xml";
	}

	public string schreibe_XML (Rezeptbuendel rezeptbuendel, string zielpfad)
	{
		var dateiname_ohne_pfad = Dateiname_fuer_Dumps(StilBits, rezeptbuendel.Index, rezeptbuendel.Freistehend);
		var zieldatei = Path.Combine(zielpfad, dateiname_ohne_pfad);

		File.WriteAllBytes(zieldatei, rezeptbuendel.XML);

		return zieldatei;
	}
}