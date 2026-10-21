using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace PowerUI
{
    public sealed class AppState
    {
        public bool Alerts = true;
        public int Threshold = 20;
        public bool Alerted;
        public int Percent = -1;
        public bool Charging;
        public ushort ReceiverPid;
        public DateTime ReadAtUtc;
        public DateTime StatusAtUtc;

        internal static readonly string StatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerUI.state.xml");

        internal static AppState Load()
        {
            if (!File.Exists(StatePath)) return new AppState();
            using (var reader = XmlReader.Create(StatePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var state = (AppState)new XmlSerializer(typeof(AppState)).Deserialize(reader);
                if (state.Threshold < 5 || state.Threshold > 90 || state.Percent < -1 || state.Percent > 100)
                    throw new InvalidDataException("Settings contain out-of-range values.");
                return state;
            }
        }

        internal void Save()
        {
            string temporary = StatePath + ".tmp";
            using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                new XmlSerializer(typeof(AppState)).Serialize(writer, this);
            if (File.Exists(StatePath)) File.Replace(temporary, StatePath, StatePath + ".bak");
            else File.Move(temporary, StatePath);
        }
    }
}
