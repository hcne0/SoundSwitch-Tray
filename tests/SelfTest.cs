using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace SoundSwitch
{
    internal static class SelfTest
    {
        private static int checks;

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "--probe-audio") { ProbeAudio(false); return 0; }
                if (args.Length == 1 && args[0] == "--switch-check") { ProbeAudio(true); return 0; }
                if (args.Length == 1 && args[0] == "--tray-smoke") { TraySmoke(); return 0; }
                if (args.Length != 0) throw new ArgumentException("Unknown self-check argument.");
                CheckSingleInstance();
                Assert(typeof(CoreAudio).GetNestedType("IMMDeviceCollection", BindingFlags.NonPublic).GUID ==
                    new Guid("0bd7a1be-7a1a-44db-8397-cc5392387b5e"), "The output collection must use the Windows audio interface ID.");
                CheckCycling();
                CheckColors();
                CheckIcons();
                string longName = new string('x', 90);
                Assert(TrayContext.ShortTooltip(longName).Length == 63, "Long tooltips must fit the Windows limit.");
                Assert(TrayContext.ShortTooltip("SoundSwitch") == "SoundSwitch", "Short tooltips must remain intact.");
                string emojiBoundary = new string('x', 61) + "\ud83c\udfa7" + longName;
                Assert(!char.IsHighSurrogate(TrayContext.ShortTooltip(emojiBoundary)[61]), "Tooltip truncation must not split an emoji.");
                Console.WriteLine("PASS: {0} self-checks (single instance, cycling, colors, icons, and tooltips).", checks);
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error);
                return 1;
            }
        }

        private static void CheckSingleInstance()
        {
            string application = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "SoundSwitch.exe"));
            Assert(File.Exists(application), "The freshly built application must exist for the single-instance check.");
            bool ownsMutex;
            using (var mutex = new Mutex(true, Program.InstanceMutexName, out ownsMutex))
            {
                try
                {
                    using (Process duplicate = Process.Start(new ProcessStartInfo(application)
                        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    {
                        bool exited = duplicate.WaitForExit(5000);
                        if (!exited) { duplicate.Kill(); duplicate.WaitForExit(); }
                        Assert(exited, "A second launch must exit before creating another tray icon.");
                        Assert(duplicate.ExitCode == 0, "A second launch must exit successfully.");
                    }
                }
                finally { if (ownsMutex) mutex.ReleaseMutex(); }
            }
        }

        private static void CheckCycling()
        {
            var speakers = new AudioDevice("speakers", "Speakers");
            var headphones = new AudioDevice("headphones", "Headphones");
            var bluetooth = new AudioDevice("bluetooth", "Bluetooth");
            Assert(new AudioSnapshot(new List<AudioDevice>(), null).NextDeviceId() == null, "No output must not switch.");
            Assert(new AudioSnapshot(new List<AudioDevice> { speakers }, speakers.Id).NextDeviceId() == null, "One output must not switch.");
            Assert(new AudioSnapshot(new List<AudioDevice> { speakers }, null).NextDeviceId() == null, "One output with no default must not switch.");
            var pair = new List<AudioDevice> { speakers, headphones };
            Assert(new AudioSnapshot(pair, speakers.Id).NextDeviceId() == headphones.Id, "Two outputs must cycle forward.");
            Assert(new AudioSnapshot(pair, headphones.Id).NextDeviceId() == speakers.Id, "Two outputs must wrap.");
            var triple = new List<AudioDevice> { speakers, headphones, bluetooth };
            Assert(new AudioSnapshot(triple, headphones.Id).NextDeviceId() == bluetooth.Id, "Three outputs must include the third device.");
            Assert(new AudioSnapshot(triple, bluetooth.Id).NextDeviceId() == speakers.Id, "Three outputs must wrap.");
            Assert(new AudioSnapshot(pair, bluetooth.Id).NextDeviceId() == speakers.Id, "A removed default must select the first remaining output.");
            var snapshot = new AudioSnapshot(pair, speakers.Id);
            Assert(snapshot.SameAs(new AudioSnapshot(pair, speakers.Id)), "An unchanged device list should be recognized.");
            Assert(!snapshot.SameAs(new AudioSnapshot(triple, speakers.Id)), "Hot-plugging must update the tray.");
            Assert(!snapshot.SameAs(new AudioSnapshot(pair, headphones.Id)), "External default changes must update the color.");
            Assert(!snapshot.SameAs(new AudioSnapshot(new List<AudioDevice> { new AudioDevice("speakers", "Renamed"), headphones }, speakers.Id)),
                "A renamed device must update the tooltip.");
        }

        private static void CheckColors()
        {
            string temporary = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "colors-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string path = Path.Combine(temporary, "colors.xml");
                var colors = new DeviceColors(path);
                Color speakers = colors.Get("speakers");
                Color headphones = colors.Get("headphones<&\"");
                Assert(speakers.ToArgb() == Color.Red.ToArgb(), "The first output must use bright red.");
                Assert(headphones.ToArgb() == Color.DodgerBlue.ToArgb(), "The second output must use vivid blue.");
                Assert(speakers.ToArgb() != headphones.ToArgb(), "Outputs must have different colors.");
                Assert(colors.Get("speakers").ToArgb() == speakers.ToArgb(), "An output must keep its color.");
                colors.Save();
                var reloaded = new DeviceColors(path);
                Assert(reloaded.Get("headphones<&\"").ToArgb() == headphones.ToArgb(), "Colors and escaped device IDs must survive restart.");
                Assert(reloaded.Get("speakers").ToArgb() == speakers.ToArgb(), "Lookup order must not change saved colors.");
                Assert(reloaded.Get("bluetooth").ToArgb() == Color.Yellow.ToArgb(), "The third output must use bright yellow.");
                reloaded.Save();
                Assert(new DeviceColors(path).Get("speakers").ToArgb() == speakers.ToArgb(), "Updating an existing color file must preserve earlier assignments.");
                var unique = new HashSet<int>();
                for (int index = 0; index < 100; index++) unique.Add(reloaded.Get("device-" + index).ToArgb());
                Assert(unique.Count == 100, "Colors must remain distinct beyond the initial palette.");

                string legacy = Path.Combine(temporary, "legacy.xml");
                File.WriteAllText(legacy, "<devices><device id='speakers' color='FF4FA3FF'/><device id='headphones' color='FFFFB347'/><device id='bluetooth' color='FF5BD990'/></devices>");
                var upgraded = new DeviceColors(legacy);
                Assert(upgraded.Get("speakers").ToArgb() == Color.Red.ToArgb() &&
                    upgraded.Get("headphones").ToArgb() == Color.DodgerBlue.ToArgb() &&
                    upgraded.Get("bluetooth").ToArgb() == Color.Yellow.ToArgb(), "Existing device assignments must upgrade to red, blue, and yellow.");
                upgraded.Save();
                var upgradedAgain = new DeviceColors(legacy);
                Assert(upgradedAgain.Get("bluetooth").ToArgb() == Color.Yellow.ToArgb() &&
                    upgradedAgain.Get("headphones").ToArgb() == Color.DodgerBlue.ToArgb(), "Upgraded colors must survive restart and lookup reordering.");

                string invalid = Path.Combine(temporary, "invalid.xml");
                File.WriteAllText(invalid, "<devices><device id='a' color='FF123456'/><device id='b' color='FF123456'/></devices>");
                string original = File.ReadAllText(invalid);
                AssertThrows<FormatException>(delegate { new DeviceColors(invalid); }, "Duplicate saved colors must be rejected.");
                Assert(File.ReadAllText(invalid) == original, "Reading an invalid file must not overwrite it.");
                File.WriteAllText(invalid, "<!DOCTYPE devices [<!ENTITY test SYSTEM 'file:///nonexistent'>]><devices>&test;</devices>");
                AssertThrows<XmlException>(delegate { new DeviceColors(invalid); }, "Color files must not load external XML entities.");
            }
            finally
            {
                foreach (string file in Directory.GetFiles(temporary)) File.Delete(file);
                Directory.Delete(temporary);
            }
        }

        private static void CheckIcons()
        {
            foreach (int size in new[] { 16, 32, 64 })
            {
                foreach (int arrows in new[] { 1, 2, 3, 4 })
                {
                    using (Bitmap bitmap = LoopIcon.Draw(arrows, Color.CornflowerBlue, size))
                    {
                        Assert(bitmap.GetPixel(0, 0).A == 0, "Tray icons must have a transparent background.");
                        Assert(InkGroups(bitmap) == arrows, "The " + size + "px icon must show " + arrows + " distinct curved arrows.");
                    }
                    using (Icon icon = LoopIcon.Create(arrows, Color.CornflowerBlue, size))
                        Assert(icon.Width == size && icon.Height == size, "Native icon handles must retain the requested size.");
                }
            }
            using (var preview = new Bitmap(400, 160))
            using (Graphics graphics = Graphics.FromImage(preview))
            using (var font = new Font("Segoe UI", 10))
            {
                var colors = new DeviceColors(null);
                graphics.Clear(Color.FromArgb(30, 32, 37));
                for (int arrows = 0; arrows <= 4; arrows++)
                {
                    Color color = arrows == 0 ? Color.Gray : colors.Get(arrows.ToString());
                    using (Bitmap large = LoopIcon.Draw(arrows, color, 64))
                    using (Bitmap small = LoopIcon.Draw(arrows, color, 16))
                    {
                        graphics.DrawImageUnscaled(large, arrows * 80 + 8, 14);
                        graphics.DrawImageUnscaled(small, arrows * 80 + 32, 94);
                        graphics.DrawString(arrows + " outputs", font, Brushes.White, arrows * 80 + 7, 127);
                    }
                }
                preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon-preview.png"), ImageFormat.Png);
            }
        }

        private static int InkGroups(Bitmap bitmap)
        {
            int groups = 0, size = bitmap.Width;
            var visited = new bool[size, size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (visited[x, y] || bitmap.GetPixel(x, y).A < 64) continue;
                groups++;
                var pending = new Queue<Point>();
                pending.Enqueue(new Point(x, y));
                visited[x, y] = true;
                while (pending.Count > 0)
                {
                    Point pixel = pending.Dequeue();
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = pixel.X + dx, ny = pixel.Y + dy;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size || visited[nx, ny] || bitmap.GetPixel(nx, ny).A < 64) continue;
                        visited[nx, ny] = true;
                        pending.Enqueue(new Point(nx, ny));
                    }
                }
            }
            return groups;
        }

        private static void ProbeAudio(bool switchCheck)
        {
            using (var audio = new CoreAudio())
            {
                AudioSnapshot snapshot = audio.Read();
                Console.WriteLine("{0} connected output(s):", snapshot.Devices.Count);
                foreach (AudioDevice device in snapshot.Devices)
                    Console.WriteLine("{0} {1}", snapshot.DefaultId == device.Id ? "*" : " ", device.Name);
                if (!switchCheck) return;
                if (snapshot.Devices.Count < 2) { Console.WriteLine("SKIPPED: real switching needs at least two connected outputs."); return; }
                string[] previous = audio.GetDefaults();
                if (previous.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("Cannot test switching without restorable defaults.");
                try
                {
                    foreach (AudioDevice device in snapshot.Devices)
                    {
                        audio.SwitchTo(device.Id);
                        Assert(audio.GetDefaults().All(id => id == device.Id), "All Windows audio roles must switch to " + device.Name);
                        Console.WriteLine("PASS: switched to " + device.Name);
                    }
                }
                finally { audio.SetDefaults(previous); }
                Assert(audio.GetDefaults().SequenceEqual(previous), "Previous Windows defaults must be restored.");
                Console.WriteLine("PASS: previous Windows defaults restored.");
            }
        }

        private static void TraySmoke()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray-smoke-colors.xml");
            using (var context = new TrayContext(path))
            using (var timer = new System.Windows.Forms.Timer { Interval = 1500 })
            {
                timer.Tick += delegate { timer.Stop(); Assert(Application.OpenForms.Count == 0, "The app must not create a main window."); context.ExitThread(); };
                timer.Start();
                Application.Run(context);
            }
            Console.WriteLine("PASS: tray starts without a main window and exits cleanly.");
        }

        private static void Assert(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertThrows<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { checks++; return; }
            throw new InvalidOperationException(message);
        }
    }
}
