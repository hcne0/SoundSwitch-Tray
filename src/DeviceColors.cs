using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace SoundSwitch
{
    internal sealed class DeviceColors
    {
        private static readonly Color[] Palette =
        {
            Color.Red, Color.DodgerBlue, Color.Yellow, Color.Lime,
            Color.Magenta, Color.Cyan, Color.Orange, Color.BlueViolet
        };
        private readonly Dictionary<string, Color> colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        private readonly string path;
        private bool dirty;

        internal DeviceColors(string path)
        {
            this.path = path;
            if (path == null || !File.Exists(path)) return;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024 };
            var document = new XmlDocument { XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, settings)) document.Load(reader);
            if (document.DocumentElement == null || document.DocumentElement.Name != "devices")
                throw new FormatException("Invalid device color file.");
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element) continue;
                string id = node.Attributes["id"] == null ? null : node.Attributes["id"].Value;
                string value = node.Attributes["color"] == null ? null : node.Attributes["color"].Value;
                uint argb;
                if (node.Name != "device" || string.IsNullOrWhiteSpace(id) || id.Length > 1024 ||
                    !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb) ||
                    (argb >> 24) != 255 || colors.ContainsKey(id) ||
                    colors.Values.Any(color => color.ToArgb() == unchecked((int)argb)))
                    throw new FormatException("Invalid device color assignment.");
                colors.Add(id, Color.FromArgb(unchecked((int)argb)));
            }
            if (document.DocumentElement.GetAttribute("palette") != "bright-rby")
            {
                string[] ids = colors.Keys.ToArray();
                colors.Clear();
                foreach (string id in ids) Get(id);
            }
        }

        internal Color Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 1024) throw new ArgumentException("Invalid device ID.", "id");
            Color result;
            if (colors.TryGetValue(id, out result)) return result;
            var used = new HashSet<int>(colors.Values.Select(color => color.ToArgb()));
            result = Palette.FirstOrDefault(color => !used.Contains(color.ToArgb()));
            int index = colors.Count;
            while (result.IsEmpty || used.Contains(result.ToArgb()))
            {
                uint value = unchecked((uint)++index * 2654435761u);
                result = Color.FromArgb(90 + (int)(value & 127), 90 + (int)((value >> 8) & 127),
                    90 + (int)((value >> 16) & 127));
            }
            colors.Add(id, result);
            dirty = true;
            return result;
        }

        internal void Save()
        {
            if (!dirty || path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (XmlWriter writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartElement("devices");
                    writer.WriteAttributeString("palette", "bright-rby");
                    foreach (KeyValuePair<string, Color> item in colors.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        writer.WriteStartElement("device");
                        writer.WriteAttributeString("id", item.Key);
                        writer.WriteAttributeString("color", item.Value.ToArgb().ToString("X8", CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                dirty = false;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
