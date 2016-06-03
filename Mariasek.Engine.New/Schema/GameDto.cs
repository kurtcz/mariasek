using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Mariasek.Engine.New.Schema
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
        [XmlAttribute(AttributeName = "Typ")]
        public Hra TypValue
        {
            get { return Typ.Value; }
            set { Typ = value; }
        }
        [XmlIgnore]
        public Barva? Trumf;
        [XmlIgnore]
        public bool TrumfValueSpecified { get { return Trumf.HasValue; } }
        [XmlAttribute(AttributeName = "Trumf")]
        public Barva TrumfValue
        {
            get { return Trumf.Value; }
            set { Trumf = value; }
        }
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
        public Stych[] Stychy;
        [XmlElement]
        public Zuctovani Zuctovani;

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

                foreach(var stych in xd.Root.Descendants("Stych"))
                {
                    var hrac1 = stych.Element("Hrac1");
                    var hrac2 = stych.Element("Hrac2");
                    var hrac3 = stych.Element("Hrac3");

                    if (!string.IsNullOrEmpty(Stychy[i].Hrac1.Poznamka))
                    {
                        hrac1.AddAfterSelf(new XComment(Stychy[i].Hrac1.Poznamka));
                    }
                    if (!string.IsNullOrEmpty(Stychy[i].Hrac2.Poznamka))
                    {
                        hrac2.AddAfterSelf(new XComment(Stychy[i].Hrac2.Poznamka));
                    }
                    if (!string.IsNullOrEmpty(Stychy[i].Hrac3.Poznamka))
                    {
                        hrac3.AddAfterSelf(new XComment(Stychy[i].Hrac3.Poznamka));
                    }
                    i++;
                }
                xd.Save(fileStream);
            }
        }
    }

    public enum Hrac
    {
        Hrac1,
        Hrac2,
        Hrac3
    }

    public class Karta
    {
        [XmlAttribute]
        public Barva Barva;
        [XmlAttribute]
        public Hodnota Hodnota;
        [XmlIgnore]
        public string Poznamka;
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
