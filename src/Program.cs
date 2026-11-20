using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

[assembly: AssemblyTitle("SoundSwitch")]
[assembly: AssemblyDescription("Switch audio outputs from the Windows tray.")]
[assembly: AssemblyProduct("SoundSwitch")]
[assembly: AssemblyVersion("1.0.0.0")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

namespace SoundSwitch
{
    internal static class Program
    {
        internal const string InstanceMutexName = "Local\\SoundSwitch.Tray.8DA1483B";

        [STAThread]
        private static void Main()
        {
            bool firstInstance;
            using (var mutex = new Mutex(true, InstanceMutexName, out firstInstance))
            {
                // Both the startup task and Start-menu launch share this session's tray instance.
                if (!firstInstance) return;
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SoundSwitch", "colors.xml");
                    using (var context = new TrayContext(settingsPath)) Application.Run(context);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };
        private DeviceColors colors;
        private CoreAudio audio;
        private AudioSnapshot snapshot;
        private Icon icon;
        private bool saveColors = true;
        private bool disposed;
        private string audioError;

        internal TrayContext(string settingsPath)
        {
            icon = LoopIcon.Create(0, Color.Gray, 32);
            tray = new NotifyIcon { Icon = icon, Text = "SoundSwitch", ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += OnMouseClick;
            menu.Items.Add("Exit SoundSwitch", null, delegate { ExitThread(); });
            try { colors = new DeviceColors(settingsPath); }
            catch (Exception error)
            {
                if (!(error is IOException || error is UnauthorizedAccessException || error is XmlException || error is FormatException)) throw;
                colors = new DeviceColors(null);
                saveColors = false;
                Notice("Saved colors could not be read. New colors will be used for this session.");
            }
            RefreshDevices();
            timer.Tick += delegate { RefreshDevices(); };
            timer.Start();
        }

        private void OnMouseClick(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left || !RefreshDevices()) return;
            string next = snapshot.NextDeviceId();
            if (next != null) SwitchTo(next);
        }

        private void SwitchTo(string id)
        {
            try { audio.SwitchTo(id); }
            catch (Exception) { Notice("Windows could not switch audio output. Reconnect the device and try again."); }
            RefreshDevices();
        }

        internal bool RefreshDevices()
        {
            try
            {
                if (audio == null) audio = new CoreAudio();
                AudioSnapshot next = audio.Read();
                if (next.SameAs(snapshot) && audioError == null) return true;
                audioError = null;
                snapshot = next;
                foreach (AudioDevice device in snapshot.Devices) colors.Get(device.Id);
                if (saveColors)
                {
                    try { colors.Save(); }
                    catch (Exception error)
                    {
                        if (!(error is IOException || error is UnauthorizedAccessException)) throw;
                        saveColors = false;
                        Notice("Device colors could not be saved. They will last for this session.");
                    }
                }
                UpdateTray();
                return true;
            }
            catch (Exception error)
            {
                if (audio != null) { audio.Dispose(); audio = null; }
                snapshot = new AudioSnapshot(new System.Collections.Generic.List<AudioDevice>(), null);
                bool report = audioError != error.Message;
                audioError = error.Message;
                UpdateTray();
                if (report) Notice("Windows audio outputs are unavailable. SoundSwitch will retry automatically.");
                return false;
            }
        }

        private void UpdateTray()
        {
            AudioDevice current = snapshot.Devices.FirstOrDefault(device => device.Id == snapshot.DefaultId);
            Color color = current == null ? Color.Gray : colors.Get(current.Id);
            int count = snapshot.Devices.Count;
            Icon nextIcon = LoopIcon.Create(count, color, Math.Max(32, SystemInformation.SmallIconSize.Width));
            tray.Icon = nextIcon;
            icon.Dispose();
            icon = nextIcon;

            string text = audioError != null ? "SoundSwitch | Audio unavailable" :
                count == 0 ? "SoundSwitch | No audio outputs connected" :
                count == 1 ? "SoundSwitch | 1 output (switching disabled) | " + (current == null ? snapshot.Devices[0].Name : current.Name) :
                "SoundSwitch | " + count + " outputs | " + (current == null ? "Click to select an output" : current.Name);
            tray.Text = ShortTooltip(text);
        }

        internal static string ShortTooltip(string text)
        {
            if (text.Length <= 63) return text;
            int length = char.IsHighSurrogate(text[61]) ? 61 : 62;
            return text.Substring(0, length) + "\u2026";
        }

        private void Notice(string text)
        {
            tray.ShowBalloonTip(4000, "SoundSwitch", text, ToolTipIcon.Warning);
        }

        protected override void ExitThreadCore()
        {
            timer.Stop();
            tray.Visible = false;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                timer.Stop();
                timer.Dispose();
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                icon.Dispose();
                if (audio != null) audio.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
