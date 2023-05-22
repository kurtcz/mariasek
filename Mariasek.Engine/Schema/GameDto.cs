using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Mariasek.Engine.Schema
{
    [XmlRoot(ElementName = "Hra")]
    public class GameDto
    {
        #region Namespace declarations

        //this code ensures that our the XmlSerializer will not output unneccessary namespace attributes
        //if invoked as follows: serializer.Serialize(stream, gameData, gameData.Namespaces);
        private XmlSerializerNamespaces _namespaces;

        [XmlNamespaceDeclarations]
        public XmlSerializerNamespaces Namespaces { get { return _namespaces; } }

        [Preserve]
        public GameDto()
        {
            _namespaces = new XmlSerializerNamespaces(new []
            {
                new XmlQualifiedName(string.Empty, "urn:Mariasek"), 
            });
        }

        #endregion

        [XmlAttribute]
        public int Kolo;
        [XmlAttribute]
        public Hrac Zacina;
        [XmlIgnore]
        public Hra? Typ;
        [XmlIgnore]
        public bool TypValueSpecified { get { return Typ.HasValue; } }
		public bool ShouldSerializeTypValue() { return Typ.HasValue; }
        [XmlAttribute(AttributeName = "Typ")]
        public Hra TypValue
        {
            get { return Typ.HasValue? Typ.Value : 0; }
            set { Typ = value; }
        }
        [XmlIgnore]
		public Barva? Trumf;
		[XmlAttribute(AttributeName = "Trumf")]
		public Barva TrumfValue
		{
			get { return Trumf.HasValue ? Trumf.Value : default(Barva); }
			set { Trumf = value; }
		}
		[XmlIgnore]
        public bool TrumfValueSpecified { get { return Trumf.HasValue; } }
		public bool ShouldSerializeTrumfValue() { return Trumf.HasValue; }
        [XmlAttribute]
        public Hrac Voli;
        [XmlElement]
        public string Autor;
        [XmlElement]
        public string Verze;
        [XmlElement]
        public string Komentar;
        [XmlArray]
        public Karta[] Hrac1;
        [XmlArray]
        public Karta[] Hrac2;
        [XmlArray]
        public Karta[] Hrac3;
        [XmlArray]
        public Karta[] Talon;
        [XmlArray]
        public Flek[] Fleky;
        [XmlArray]
        public Stych[] Stychy;
        [XmlElement]
        public Zuctovani Zuctovani;
        [XmlIgnore]
        public string BiddingNotes;
        public bool ShouldSerializeZuctovani()
        {
            return Zuctovani != null;
        }
#if !PORTABLE
        public void SaveGame(string filename, bool saveDebugInfo = false)
        {
            using (var fileStream = new FileStream(filename, FileMode.Create))
            {
                SaveGame(fileStream, saveDebugInfo);
            }
        }
#endif
        public void SaveGame(Stream fileStream, bool saveDebugInfo = false)
        {
            var serializer = new XmlSerializer(typeof(GameDto));

            if (!saveDebugInfo)
            {
                serializer.Serialize(fileStream, this, Namespaces);
            }
            else
            {
                var xd = new XDocument();

                using (var xmlWriter = xd.CreateWriter())
                {
                    serializer.Serialize(xmlWriter, this, Namespaces);
                }

                var i = 0;
                var verze = xd.Root.Element("Verze");
                verze.AddAfterSelf(new XComment(BiddingNotes));
                foreach(var stych in xd.Root.Descendants("Stych"))
                {
                    var hrac1 = stych.Element("Hrac1");
                    var hrac2 = stych.Element("Hrac2");
                    var hrac3 = stych.Element("Hrac3");
                    var poznamka1 = string.IsNullOrEmpty(Stychy[i]?.Hrac1?.Poznamka) ? null : new XComment($" {Stychy[i].Hrac1.Poznamka}{(string.IsNullOrEmpty(Stychy[i].Hrac1.AiDebugInfo) ? "" : Stychy[i].Hrac1.AiDebugInfo)} ");
                    var poznamka2 = string.IsNullOrEmpty(Stychy[i]?.Hrac2?.Poznamka) ? null : new XComment($" {Stychy[i].Hrac2.Poznamka}{(string.IsNullOrEmpty(Stychy[i].Hrac2.AiDebugInfo) ? "" : Stychy[i].Hrac2.AiDebugInfo)} ");
                    var poznamka3 = string.IsNullOrEmpty(Stychy[i]?.Hrac3?.Poznamka) ? null : new XComment($" {Stychy[i].Hrac3.Poznamka}{(string.IsNullOrEmpty(Stychy[i].Hrac3.AiDebugInfo) ? "" : Stychy[i].Hrac3.AiDebugInfo)} ");
                    var poznamka21 = string.IsNullOrEmpty(Stychy[i]?.Hrac1?.AiDebugInfo2) ? null : new XComment($" {Stychy[i].Hrac1.AiDebugInfo2} ");
                    var poznamka22 = string.IsNullOrEmpty(Stychy[i]?.Hrac2?.AiDebugInfo2) ? null : new XComment($" {Stychy[i].Hrac2.AiDebugInfo2} ");
                    var poznamka23 = string.IsNullOrEmpty(Stychy[i]?.Hrac3?.AiDebugInfo2) ? null : new XComment($" {Stychy[i].Hrac3.AiDebugInfo2} ");

                    if (poznamka1 != null)
                    {
                        hrac1.AddAfterSelf(poznamka1);
                    }
                    if (poznamka2 != null)
                    {
                        hrac2.AddAfterSelf(poznamka2);
                    }
                    //poznamky o pravdepodobnostech (budou na konci)
                    if (poznamka23 != null)
                    {
                        hrac3.AddAfterSelf(poznamka23);
                    }
                    if (poznamka22 != null)
                    {
                        hrac3.AddAfterSelf(poznamka22);
                    }
                    if (poznamka21 != null)
                    {
                        hrac3.AddAfterSelf(poznamka21);
                    }
                    //nakonec klasickou poznamku (bude prvni)
                    if (poznamka3 != null)
                    {
                        hrac3.AddAfterSelf(poznamka3);
                    }
                    i++;
                }
                xd.Save(fileStream);
            }
        }
    }

    [Flags]
    public enum Hrac
    {
        Hrac1 = 1,
        Hrac2 = 2,
        Hrac3 = 4
    }

    public class Karta
    {
        [XmlAttribute]
        public Barva Barva;
        [XmlAttribute]
        public Hodnota Hodnota;
        [XmlIgnore]
        public string Poznamka;
        [XmlIgnore]
        public string AiDebugInfo;
        [XmlIgnore]
        public string AiDebugInfo2;
    }

    public class Flek
    {
        [XmlAttribute]
        public Hra Hra;
        [XmlAttribute]
        public int Pocet;
        [XmlAttribute]
        public Hrac Hraci;
    }

    public class Stych
    {
        [XmlAttribute]
        public int Kolo;
        [XmlAttribute]
        public Hrac Zacina;
        [XmlElement]
        public Karta Hrac1;
        [XmlElement]
        public Karta Hrac2;
        [XmlElement]
        public Karta Hrac3;
    }

    public class Skore
    {
        [XmlAttribute]
        public int Body;
        [XmlAttribute]
        public int Zisk;
    }

    public class Zuctovani
    {
        [XmlElement]
        public Skore Hrac1;
        [XmlElement]
        public Skore Hrac2;
        [XmlElement]
        public Skore Hrac3;
    }
}
