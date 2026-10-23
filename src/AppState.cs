using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace PowerUI
{
    public enum BatteryColorScheme { GreenDark, GreenWhite, BlueWhite, BlackWhite, WhiteBlack }
    public enum TrayDisplay { Separate, Mouse, Keyboard }

    public sealed class AppState
    {
        public BatteryColorScheme ColorScheme = BatteryColorScheme.GreenDark;
        public TrayDisplay TrayDisplay = TrayDisplay.Separate;
        public bool Alerts = true;
        public int Threshold = 20;
        public bool Alerted;
        public int Percent = -1;
        public bool Charging;
        public ushort ReceiverPid;
        public ushort ReceiverVid;
        public byte DeviceIndex;
        public string DeviceName;
        public DateTime ReadAtUtc;
        public DateTime StatusAtUtc;

        internal static readonly string StatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerUI.state.xml");
        internal static readonly string KeyboardStatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerUI.keyboard.state.xml");

        internal static AppState Load(string path)
        {
            if (!File.Exists(path)) return new AppState();
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var state = (AppState)new XmlSerializer(typeof(AppState)).Deserialize(reader);
                if (state.Threshold < 5 || state.Threshold > 90 || state.Percent < -1 || state.Percent > 100)
                    throw new InvalidDataException("Settings contain out-of-range values.");
                return state;
            }
        }

        internal void Save(string path)
        {
            string temporary = path + ".tmp";
            using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                new XmlSerializer(typeof(AppState)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
