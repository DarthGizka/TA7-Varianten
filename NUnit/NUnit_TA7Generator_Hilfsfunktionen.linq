<Query Kind="Program">
  <NuGetReference>NUnitLite</NuGetReference>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Security.Cryptography.Pkcs</Namespace>
  <Namespace>System.Security.Cryptography.X509Certificates</Namespace>
  <Namespace>NUnit.Framework</Namespace>
</Query>

// NUnit_TA7Generator_Hilfsfunktionen.linq
// 2024-02-24
// 2024-02-25 + extrahiere_VO_GQ_AD_aus_Rezeptbuendel()

#load "..\INC_TA7Generator_Hilfsfunktionen"

void Main ()
{
	new NUnitLite.AutoRun().Execute(new[]{"--noheader","--noresult","--workers=1","--labels=ALL"});
}

// testet Basispfad indirekt gleich mit
[Test]
public void Vorlagepfad ()
{
	var pfad = TA7Generator.Vorlagepfad;

	Assert.That(
		File.Exists(Path.Combine(pfad, "GQ.xml")), 
		Is.True, 
		() => "GQ.xml soll existieren in " + pfad );
}

[Test]
public void P12Pfad ()
{
	var pfad = TA7Generator.P12Pfad;

	Assert.That(
		File.Exists(Path.Combine(pfad, "Apotheke.p12")), 
		Is.True, 
		() => "Apotheke.p12 soll existieren in " + pfad );
}

[Test]
public void lies_Vorlage ()
{
	var gelesen = TA7Generator.lies_Vorlage("VO.xml");
	var rohdaten = File.ReadAllText(Path.Combine(TA7Generator.Vorlagepfad, "VO.xml"), TA7Generator.UTF8_ohne_BOM);
	var sollwert = TA7Generator.umformatiere(rohdaten, minifizieren: false);

	Assert.That(gelesen, Is.EqualTo(sollwert));

	var minifiziert = TA7Generator.lies_Vorlage("VO.xml", minifizieren: true);

	Assert.That(minifiziert, Is.EqualTo(TA7Generator.minifiziere(sollwert)));
}

/** prüft Funktionieren eines geladenen P12 durch Signieren eines Textes */
[Test]
public void lade_P12 ()
{
	const string AUSGANGSTEXT = "Zwölf laxe Typen qualmen verdächtig süße Objekte.";
	var p12 = TA7Generator.lade_P12("Arzt.p12");
	var cms_signer = new CmsSigner(p12);
	var textbytes = TA7Generator.UTF8_ohne_BOM.GetBytes(AUSGANGSTEXT);
	var signed_cms = new SignedCms(new ContentInfo(textbytes));
	
	signed_cms.ComputeSignature(cms_signer);

	var cms_bytes = signed_cms.Encode();
	var rundgereist = TA7Generator.Text_aus_SignedCMS(cms_bytes);

	Assert.That(rundgereist, Is.EqualTo(AUSGANGSTEXT));
}

// testet indirekt auch Text_aus_SignedCMS()
[Test]
public void CMS_als_Base64 ()
{
	// bei uns enthält das CMS zwar grundsätzlich XML, aber der zu testenden Funktion ist das egal
	const string AUSGANGSTEXT = "Schweißgequält vom öden Text zürnt Typograf Jakob.";
	var p12 = new X509Certificate2(Path.Combine(TA7Generator.P12Pfad, "Arzt.p12"));
	var base64 = TA7Generator.CMS_als_Base64(AUSGANGSTEXT, p12);
	var cms_bytes = Convert.FromBase64String(base64);
//	var signed_cms = new System.Security.Cryptography.Pkcs.SignedCms();
//
//	signed_cms.Decode(cms_bytes);
//
//	var rundgereist = Encoding.UTF8.GetString(signed_cms.ContentInfo.Content);
	var rundgereist = TA7Generator.Text_aus_SignedCMS(cms_bytes);
	
	Assert.That(rundgereist, Is.EqualTo(AUSGANGSTEXT));
}

[Test]
public void signiere_Quittung ()
{
	var quittung = TA7Generator.lies_Vorlage("GQ.xml");
	var p12      = TA7Generator.lade_P12("Fachdienst.p12");
	var signiert = TA7Generator.signiere_Quittung(quittung, p12);
	var sig_anfang = signiert.IndexOf("<signature>");
	var sig_ende   = signiert.IndexOf("</signature>") + "</signature>".Length;
	var signatur   = signiert.Substring(sig_anfang, sig_ende - sig_anfang);
	var huelle_ohne_signatur = signiert.Substring(0, sig_anfang) + signiert.Substring(sig_ende);

	Assert.That(huelle_ohne_signatur, Is.EqualTo(quittung));

	var base64_anfang = signatur.IndexOf("<data value=\"") + "<data value=\"".Length;
	var base64_ende   = signatur.IndexOf("\"/>", base64_anfang);
	var reskelettiert = signatur.Substring(0, base64_anfang) + TA7Generator.PLATZHALTER_CMS_BASE64 + signatur.Substring(base64_ende);

	Assert.That(reskelettiert, Is.EqualTo(TA7Generator.VORLAGE_FUER_SIGNATURBLOCK.Replace('\'', '"')));

	var cms_base64 = signatur.Substring(base64_anfang, base64_ende - base64_anfang);
	var cms_bytes  = Convert.FromBase64String(cms_base64);
	var sig_inhalt = TA7Generator.Text_aus_SignedCMS(cms_bytes);

	Assert.That(sig_inhalt, Is.EqualTo(quittung));
}

[Test]
public void erster_Platzhalter_fuer_ERezeptId ()
{
	const string GEMATIKBEISPIEL  = "160.123.456.789.123.58";
	const string DIREKTZUWEIUSUNG = "169.123.456.789.123.66";

	Assert.That(TA7Generator.erster_Platzhalter_fuer_ERezeptId(GEMATIKBEISPIEL), Is.EqualTo(GEMATIKBEISPIEL));
	Assert.That(TA7Generator.erster_Platzhalter_fuer_ERezeptId("7" + GEMATIKBEISPIEL + "7"), Is.EqualTo(GEMATIKBEISPIEL));
	Assert.That(TA7Generator.erster_Platzhalter_fuer_ERezeptId(DIREKTZUWEIUSUNG), Is.EqualTo(DIREKTZUWEIUSUNG));
}

[Test]
public void korrigiere_Pruefziffern_in_ERezeptId ()
{
	Assert.That(TA7Generator.korrigiere_Pruefziffern_in_ERezeptId(16012345678912399), Is.EqualTo(16012345678912358));
	Assert.That(TA7Generator.korrigiere_Pruefziffern_in_ERezeptId(16912345678912399), Is.EqualTo(16912345678912366));

	// Das vereinfachte Verfahren nach ISO 7064 ergibt Werte im Bereich 02..98. Für drei dieser Werte würden
	// 00, 01 bzw. 99 als Prüfziffern ebenfalls einen Rest von 1 modulo 97 produzieren. Wir dürfen aber nur 
	// die Werte gemäß ISO erzeugen.
	Assert.That(9701 % 97, Is.EqualTo(1));
	Assert.That(TA7Generator.korrigiere_Pruefziffern_in_ERezeptId(9701), Is.EqualTo(9798));

	for (var n = 0; n <= 9700; n += 100)
	{
		Assert.That(TA7Generator.korrigiere_Pruefziffern_in_ERezeptId(n) % 100, Is.InRange(2, 98), () => $"n = {n}");
	}
}

[Test]
public void Text_fuer_ERezeptId ()
{
	// Prüfziffern bewußt falsch, um Abwesenheit von diesbezüglichen Explosionen zu verifizieren
	Assert.That(TA7Generator.Text_fuer_ERezeptId(16012345678912399), Is.EqualTo("160.123.456.789.123.99"));
}

[Test]
public void UUID_fuer_Index ()
{
	var rng = new Random();

	for (var i = 0; i < 1000; ++i)
	{
		var zufallsindex = rng.Next();
		var uuid = TA7Generator.UUID_fuer_Index(zufallsindex);

		// gleiches Argument ergibt gleiche UUID
		Assert.That(TA7Generator.UUID_fuer_Index(zufallsindex), Is.EqualTo(uuid));

		// UUID-Variante korrekt gekennzeichnet ('-' hinzugenommen als Orientierungshilfe)
		Assert.That(uuid.ToString().Substring(13, 2), Is.EqualTo("-4"));
	}
}

[Test]
public void minifiziere ()
{
	const string XML = @"<?xml version='1.0'?>
<?pi1?><!--1-->
<a>
	<?pi2?><!--2-->
	<b><?pi3?><!--3--></b>
	<?pi4?><!--4-->
	<c/>
	<?pi5?><!--5-->	
</a>
<?pi6?><!--6-->";

	Assert.That(TA7Generator.minifiziere(XML), Is.EqualTo("<a><b></b><c/></a>"));
}

private const string ROH_XML = "<?xml version='1.0'?> <!--A--> <?pi?> <a> <b> </b> <c/> </a> <?pi?> <!--Z--> ";

[Test]
public void formatiere_lesbar ()
{
	var formatiert = TA7Generator.formatiere_lesbar(ROH_XML);
	// Kommentare und Processing Instructions werden *nicht* vernichtet, nur XML-Deklarationen
	var erwartet = @"
<!--A-->
<?pi?>
<a>
  <b></b>
  <c/>
</a>
<?pi?>
<!--Z-->
".Trim();

	Assert.That(formatiert, Is.EqualTo(erwartet));
}

[Test]
public void umformatiere ()
{
	var lesbar = TA7Generator.umformatiere(ROH_XML);
	// Kommentare und Processing Instructions verschwinden hier
	var erwartet = @"
<a>
  <b></b>
  <c/>
</a>
".Trim();

	Assert.That(lesbar, Is.EqualTo(erwartet));

	var minifiziert = TA7Generator.umformatiere(ROH_XML, minifizieren: true);
	
	Assert.That(minifiziert, Is.EqualTo("<a><b></b><c/></a>"));
}

private static void assertiere_ist_CMS_fuer_VO_oder_GQ_oder_AD_ (byte[] cms_bytes, string kuerzel, string erwarteter_stummel)
{
	if (cms_bytes.Length < 1000)
	{
		Assert.That(Convert.ToBase64String(cms_bytes), Is.EqualTo(erwarteter_stummel));
	}
	else
	{
		var xml = TA7Generator.Text_aus_SignedCMS(cms_bytes);
		// Umformatieren prüft implizit, ob es sich um wohlgeformtes XML handelt
		var minifiziert = TA7Generator.minifiziere(xml);

		Assert.That(minifiziert, Does.StartWith("<Bundle"), () => kuerzel);
		Assert.That(minifiziert, Does.EndWith("Bundle>"), () => kuerzel);			
	}
}

[Test]
public void extrahiere_VO_GQ_AD_aus_Rezeptbuendel ()
{
	var quelldateien_ohne_pfad = new []
	{
		"Rezeptbuendel_S8_12345.xml",
		"Rezeptbuendel_S9_12345.xml"
	};

	foreach (var dateiname_ohne_pfad in quelldateien_ohne_pfad)
	{
		var quelldatei = Path.Combine(TA7Generator.Basispfad, "Testdaten", dateiname_ohne_pfad);
		var vqa = TA7Generator.extrahiere_VO_GQ_AD_aus_Rezeptbuendel(File.ReadAllBytes(quelldatei));

		assertiere_ist_CMS_fuer_VO_oder_GQ_oder_AD_(vqa.CMS_VO, "VO", TA7Generator.STUMMEL_BASE64_VO);
		assertiere_ist_CMS_fuer_VO_oder_GQ_oder_AD_(vqa.CMS_GQ, "GQ", TA7Generator.STUMMEL_BASE64_GQ);
		assertiere_ist_CMS_fuer_VO_oder_GQ_oder_AD_(vqa.CMS_AD, "AD", TA7Generator.STUMMEL_BASE64_AD);
	}
}