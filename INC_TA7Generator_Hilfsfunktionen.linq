<Query Kind="Program">
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Security.Cryptography.Pkcs</Namespace>
  <Namespace>System.Security.Cryptography.X509Certificates</Namespace>
</Query>

/////////////////////////////////////////////////////////////////////////////////////////////////////////////
///// TA7Generator - Hilfsfunktionen /////////////////////////////////////////////////////// 2023-11-12 /////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////

// 2024-02-25 + extrahiere_VO_GQ_AD_aus_Rezeptbuendel()

partial class TA7Generator
{
	public static string Basispfad 
	{ 
		get 
		{ 
			var aktueller_wert = s_Basispfad_;

			return aktueller_wert != null ? aktueller_wert : s_Basispfad_ = ermittle_Basispfad();
		}

		set
		{
			if (ist_plausibel_als_Basispfad_(value))
				s_Basispfad_ = value;
			else
				throw new ArgumentException($"kein gültiger Basispfad für TA7-Varianten-Skripte: '{value}'");
		}
	}

	public static string Vorlagepfad => Path.Combine(Basispfad, "Vorlagen");
	public static string P12Pfad     => Basispfad;

	public const string VERORDNUNGSHASH_IN_VORLAGEN = "Y8nQ5of9uF0yrykBEfAfIdILUza2eOKKhqMkqDvW2bc=";
	public const string STUMMEL_BASE64_VO = "Base64/CMSVerordnung";
	public const string STUMMEL_BASE64_GQ = "Base64/XMLQuittung==";
	public const string STUMMEL_BASE64_AD = "Base64/CMSAbgabesatz";

	private static volatile string s_Basispfad_ = null;

	/** sucht das Verzeichnis, welches diese LINQ-Datei enthält und ein Unterverzeichnis 'Vorlagen' hat;
	 * zum Aufrufzeitpunkt enthält `Util.CurrentQueryPath` das aufrufende Skript, das in einem Unterpfad
	 * oder auch in einem übergeordneten Verzeichnis liegen kann */
	internal static string ermittle_Basispfad ()
	{
		for (var pfad = Util.CurrentQueryPath; (pfad = Path.GetDirectoryName(pfad)) != null; )
		{
			if (ist_plausibel_als_Basispfad_(pfad))
				return pfad;

			if (ist_plausibel_als_Basispfad_(Path.Combine(pfad, "TA7-Varianten"), out var treffer_1))
				return treffer_1;
				
			if (ist_plausibel_als_Basispfad_(Path.Combine(pfad, "v1"), out var treffer_2))
				return treffer_2;
		}
	
		throw new InvalidOperationException("Basispfad der LINQPad-Skripte (TA7-Varianten) nicht gefunden");
	}

	private static bool ist_plausibel_als_Basispfad_ (string pfad, out string getesteter_wert)
	{
		getesteter_wert = pfad;

		return Directory.Exists(pfad) && ist_plausibel_als_Basispfad_(pfad);
	}
	
	private static bool ist_plausibel_als_Basispfad_ (string pfad)
	{
		return File.Exists(Path.Combine(pfad, "INC_TA7Generator_Hilfsfunktionen.linq"))
			&& File.Exists(Path.Combine(pfad, "INC_Werteinsetzer.linq"))
			&& Directory.Exists(Path.Combine(pfad, "Vorlagen"));
	}

	internal readonly static Encoding UTF8_ohne_BOM = new UTF8Encoding(/*BOM*/ false, /*validate*/ true);

	internal static string lies_Vorlage (string dateiname_ohne_pfad, bool minifizieren = false)
	{
		var dateiname_mit_pfad = Path.Combine(Vorlagepfad, dateiname_ohne_pfad);
		var xml = File.ReadAllText(dateiname_mit_pfad, UTF8_ohne_BOM);

		return umformatiere(xml, minifizieren);
	}

	internal static X509Certificate2 lade_P12 (string dateiname_ohne_pfad)
	{
		var dateiname_mit_pfad = Path.Combine(P12Pfad, dateiname_ohne_pfad);

		return new X509Certificate2(dateiname_mit_pfad);
	}

	/** wandelt den Text zu UTF-8, signiert mit dem übergebenen P12 und kodiert das Ergebnis als Base64 */
	internal static string CMS_als_Base64 (string text, X509Certificate2 p12_fuer_signatur)
	{
		return CMS_als_Base64(UTF8_ohne_BOM.GetBytes(text), p12_fuer_signatur);
	}

	/** signiert die Bytes mit dem übergebenen P12 und kodiert das Ergebnis als Base64 */
	internal static string CMS_als_Base64 (byte[] bytes, X509Certificate2 p12_fuer_signatur)
	{
		var signed_cms = new SignedCms(new ContentInfo(bytes));

		signed_cms.ComputeSignature(new CmsSigner(p12_fuer_signatur));

		var cms_bytes = signed_cms.Encode();

		return Convert.ToBase64String(cms_bytes);		
	}

	/** extrahiert die signierten Bytes aus einer SignedCMS-Struktur und wandelt sie in Text zurück
	 * (letzteres ist ohne weiteres möglich, weil TA7 nur signiertes XML mit UTF-8 ohne BOM kennt) */
	internal static string Text_aus_SignedCMS (byte[] cms_bytes)
	{
		var xml_bytes = auswickle_SignedCMS(cms_bytes);

		return Encoding.UTF8.GetString(xml_bytes);
	}

	/** extrahiert die signierten Bytes aus einer SignedCMS-Struktur */
	internal static byte[] auswickle_SignedCMS (byte[] cms_bytes)
	{
		var signed_cms = new System.Security.Cryptography.Pkcs.SignedCms();

		signed_cms.Decode(cms_bytes);

		return signed_cms.ContentInfo.Content;
	}

	internal static string SHA256_als_Base64 (byte[] bytes)
	{
		using (var sha256 = System.Security.Cryptography.SHA256.Create())
		{
			var hash = sha256.ComputeHash(bytes);

			return Convert.ToBase64String(hash);
		}
	}

	internal const string PLATZHALTER_CMS_BASE64 = "{CMS_als_Base64}";

	internal const string VORLAGE_FUER_SIGNATURBLOCK =
	    "<signature>" +
	        "<type>" +
	            "<system value='urn:iso-astm:E1762-95:2013'/>" +
	            "<code value='1.2.840.10065.1.12.1.1'/>" +
	        "</type>" +
			// 1 ms nach Bundle.timestamp
	        "<when value='2024-02-25T12:34:56.125+00:00'/>" +
	        "<who>" +
	            "<reference value='urn:uuid:51a3ee5c-80df-4510-818a-917c2c05223d'/>" +
	        "</who>" +
	        "<sigFormat value='application/pkcs7-mime'/>" +
	        "<data value='{CMS_als_Base64}'/>" +
	    "</signature>";

	/** erzeugt die schwachsinnige XML-DSig-Struktur (signiertes CMS als Fußnote an einer Kopie des XML baumelnd);	
	 * der XML-Text der Quittung muß mit '</Bundle>' enden, punto e basta */
	public static string signiere_Quittung (string xml_quittung, X509Certificate2 p12_fuer_signatur)
	{
		const string BUNDLE_ENDE = "</Bundle>";

		if (!xml_quittung.EndsWith(BUNDLE_ENDE))
		{
			throw new ArgumentException($"XML-Text muß mit '{BUNDLE_ENDE}' enden");
		}

		var cms_base64 = CMS_als_Base64(xml_quittung, p12_fuer_signatur);
		var signaturblock = VORLAGE_FUER_SIGNATURBLOCK
			.Replace('\'', '"')
			.Replace(PLATZHALTER_CMS_BASE64, cms_base64);

		return xml_quittung.Substring(0, xml_quittung.Length - BUNDLE_ENDE.Length)
			+ signaturblock
			+ BUNDLE_ENDE;
	}

	/** findet das erste Auftreten einer E-Rezept-Id, welche auf dem Gematik-Beispiel 160.123.456.789.123.58
	 * basiert, auch bei geändertem Ablauftyp ('Workflow Type') und folglich anderen Prüfziffern */
	internal static string erster_Platzhalter_fuer_ERezeptId (string xml)
	{
		var o = xml.IndexOf(".123.456.789.123.");  // Gematik-Beispiel ohne Ablauftyp und ohne Prüfziffern

		if (o >= 3 && xml.Length >= o + 19)
		{
			return xml.Substring(o - 3, 22);
		}

		throw new ArgumentException("Text enthält keine E-Rezept-Id à la ???.123.456.789.123.??");
	}

	internal static long korrigiere_Pruefziffern_in_ERezeptId (long erezept_id)
	{
		var wert_mit_pz_00 = erezept_id - erezept_id % 100;
		var rest = wert_mit_pz_00 % 97;
		
		return wert_mit_pz_00 + 1 + 97 - rest;
	}

	internal static readonly NumberFormatInfo s_NumberFormatInfo_de_DE = 
		new CultureInfo("de-DE", useUserOverride: false).NumberFormat;

	/** liefert die gepunktete Textform der E-Rezept-Id (à la "160.123.456.789.123.58") */
	internal static string Text_fuer_ERezeptId (long erezept_id, bool leerwert_zulaessig = false)
	{
		if (10000000000000000L <= erezept_id && erezept_id <= 99999999999999999L)
		{
			return (erezept_id * 10).ToString("N", s_NumberFormatInfo_de_DE).Substring(0, 22);
		}
		else if (erezept_id == 0L && leerwert_zulaessig)
		{
			return "";
		}

		throw new ArgumentException($"E-Rezept-Id hat unzulässigen Wert: {erezept_id}");
	}

	// 900/ms -> 2 Größenordnungen langsamer als die Variante mit Xorshift64, aber für die 
	// homöopathischen Datensatzanzahlen hier immer noch mehr als schnell genug
	/** liefert die pseudozufällige UUID, welche dem angegebenen Index entspricht (gleicher Index
	 * ergibt immer die gleiche UUID) */
	internal static Guid UUID_fuer_Index (int index)
	{
		var rng = new Random((int) ((index + 1) * 0x9E3779B9u));
		var uuid_bytes = new byte[16];

		rng.NextBytes(uuid_bytes);

		uuid_bytes[7] = (byte) (0x40 | (uuid_bytes[7] & 0x0F));

		return new Guid(uuid_bytes);
	}

	/** minifiziert das übergebene XML durch Umformatieren mit einem geeignet konfigurierten XmlWriter; außer
	 * insignifikantem Whitespace werden dabei auch XML-Kommentare und Processing Instructions entfernt */
	public static string minifiziere (string xml)
	{
		return format_via_XmlWriter(xml, minified: true, keep_nuisance_blanks: false);
	}

	/** formatiert das übergebene XML durch Umformatieren mit einem geeignet konfigurierten XmlWriter */
	public static string formatiere_lesbar (string xml)
	{
		return format_via_XmlWriter(xml, minified: false, keep_nuisance_blanks: false);
	}

	/** normalisiert das übergebene XML, indem es zunächst minifiziert und anschließend ggf. lesbar formatiert wird;
	 * durch die vorangehende Minifizierung werden XML-Deklaration, XML-Kommentare und Processing Instructions 
	 * in jedem Fall eliminiert */
	public static string umformatiere (string xml, bool minifizieren = false)
	{
		var minifiziert = minifiziere(xml);

		return minifizieren
			? minifiziert
			: formatiere_lesbar(minifiziert);
	}

	private const RegexOptions REGEX_OPTIONS_ =
		RegexOptions.Compiled |
		RegexOptions.Singleline |       // needed for . to match across line endings
		RegexOptions.CultureInvariant;

	// eliminates nuisance blanks from known simple inputs (no " />" that needs preserving)
	private static readonly Regex s_Regex_for_nuisance_blanks_ = new Regex(" />", REGEX_OPTIONS_);

	internal static string format_via_XmlWriter (
		string xml,
		bool minified = false,
		bool keep_nuisance_blanks = false )
	{
		var reader_settings = new XmlReaderSettings
		{
			CheckCharacters              = true,
			ConformanceLevel             = ConformanceLevel.Document,
			DtdProcessing                = DtdProcessing.Prohibit,
			IgnoreComments               = minified,
			IgnoreProcessingInstructions = minified,
			IgnoreWhitespace             = minified
		};

		var writer_settings = new XmlWriterSettings
		{
			Indent             = !minified,
			NamespaceHandling  = NamespaceHandling.OmitDuplicates,
			OmitXmlDeclaration = true // 100% superfluous if only ASCII and UTF-8 are allowed anyway
		};

		using (var string_writer = new StringWriter())
		{
			using (var string_reader = new StringReader(xml))
			using (var xml_reader = XmlReader.Create(string_reader, reader_settings))
			using (var xml_writer = XmlWriter.Create(string_writer, writer_settings))
			{
				var xml_document = new XmlDocument();

				xml_document.Load(xml_reader);
				xml_document.WriteContentTo(xml_writer);
			}

			var preliminary_result = string_writer.ToString();

			return keep_nuisance_blanks
				? preliminary_result
				: s_Regex_for_nuisance_blanks_.Replace(preliminary_result, "/>");			
		}
	}

	/** in den 3 Base64-Klumpen eines Rezeptbündels enthaltene Daten */
	public readonly struct VO_GQ_AD
	{
		public readonly byte[] CMS_VO;
		public readonly byte[] CMS_GQ;
		public readonly byte[] CMS_AD;

		private VO_GQ_AD (byte[] CMS_VO, byte[] CMS_GQ, byte[] CMS_AD)
		{
			this.CMS_VO = CMS_VO;
			this.CMS_GQ = CMS_GQ;
			this.CMS_AD = CMS_AD;
		}

		private static readonly Regex s_Regex_fuer_Base64_ = new Regex(
			"(?:<data value=[\"'])([^\"']+)(?:[\"']/>)", 
			RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant );

		public static VO_GQ_AD extrahiere_aus (string xml)
		{
			var matches = s_Regex_fuer_Base64_.Matches(xml);

			if (matches.Count != 3)
			{
				throw new ArgumentException("WTF?!");
			}

			var cms_vo = Convert.FromBase64String(matches[0].Groups[1].Value);
			var cms_gq = Convert.FromBase64String(matches[1].Groups[1].Value);
			var cms_ad = Convert.FromBase64String(matches[2].Groups[1].Value);

			return new VO_GQ_AD(cms_vo, cms_gq, cms_ad);
		}
	}

	public static VO_GQ_AD extrahiere_VO_GQ_AD_aus_Rezeptbuendel (string rezeptbuendel_xml)
	{
		return VO_GQ_AD.extrahiere_aus(rezeptbuendel_xml);
	}

	public static VO_GQ_AD extrahiere_VO_GQ_AD_aus_Rezeptbuendel (byte[] rezeptbuendel_xml)
	{
		return extrahiere_VO_GQ_AD_aus_Rezeptbuendel(UTF8_ohne_BOM.GetString(rezeptbuendel_xml));
	}
}
