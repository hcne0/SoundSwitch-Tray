using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SoundSwitch
{
    internal sealed class AudioDevice
    {
        internal readonly string Id;
        internal readonly string Name;

        internal AudioDevice(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    internal sealed class AudioSnapshot
    {
        internal readonly List<AudioDevice> Devices;
        internal readonly string DefaultId;

        internal AudioSnapshot(List<AudioDevice> devices, string defaultId)
        {
            Devices = devices;
            DefaultId = defaultId;
        }

        internal string NextDeviceId()
        {
            if (Devices.Count < 2) return null;
            int current = Devices.FindIndex(device => device.Id == DefaultId);
            return Devices[(current + 1) % Devices.Count].Id;
        }

        internal bool SameAs(AudioSnapshot other)
        {
            return other != null && DefaultId == other.DefaultId &&
                Devices.Select(device => device.Id).SequenceEqual(other.Devices.Select(device => device.Id)) &&
                Devices.Select(device => device.Name).SequenceEqual(other.Devices.Select(device => device.Name));
        }
    }

    internal sealed class CoreAudio : IDisposable
    {
        private IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        private const int NotFound = unchecked((int)0x80070490);

        internal AudioSnapshot Read()
        {
            IMMDeviceCollection collection;
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 1, out collection));
            var devices = new List<AudioDevice>();
            try
            {
                uint count;
                Marshal.ThrowExceptionForHR(collection.GetCount(out count));
                for (uint index = 0; index < count; index++)
                {
                    IMMDevice device;
                    Marshal.ThrowExceptionForHR(collection.Item(index, out device));
                    try
                    {
                        string id;
                        Marshal.ThrowExceptionForHR(device.GetId(out id));
                        devices.Add(new AudioDevice(id, FriendlyName(device)));
                    }
                    finally { Marshal.ReleaseComObject(device); }
                }
            }
            finally { Marshal.ReleaseComObject(collection); }

            devices = devices.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(device => device.Id, StringComparer.Ordinal).ToList();
            return new AudioSnapshot(devices, GetDefaultId(1));
        }

        internal string GetDefaultId(int role)
        {
            IMMDevice device;
            int result = enumerator.GetDefaultAudioEndpoint(0, role, out device);
            if (result == NotFound) return null;
            Marshal.ThrowExceptionForHR(result);
            try
            {
                string id;
                Marshal.ThrowExceptionForHR(device.GetId(out id));
                return id;
            }
            finally { Marshal.ReleaseComObject(device); }
        }

        internal string[] GetDefaults()
        {
            return new[] { GetDefaultId(0), GetDefaultId(1), GetDefaultId(2) };
        }

        internal void SwitchTo(string id)
        {
            AudioSnapshot snapshot = Read();
            if (snapshot.Devices.Count < 2) return;
            if (!snapshot.Devices.Any(device => device.Id == id))
                throw new InvalidOperationException("That audio output is no longer connected.");
            SetDefaults(new[] { id, id, id });
        }

        internal void SetDefaults(string[] ids)
        {
            if (ids == null || ids.Length != 3 || ids.Any(string.IsNullOrEmpty))
                throw new ArgumentException("An output is required for each Windows audio role.", "ids");

            string[] previous = GetDefaults();
            var policy = (IPolicyConfig)new PolicyConfigClient();
            var changed = new List<int>();
            try
            {
                for (int role = 0; role < 3; role++)
                {
                    if (previous[role] == ids[role]) continue;
                    Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(ids[role], role));
                    changed.Add(role);
                }
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                foreach (int role in changed)
                {
                    if (previous[role] == null) continue;
                    int result = policy.SetDefaultEndpoint(previous[role], role);
                    if (result < 0) failures.Add(Marshal.GetExceptionForHR(result));
                }
                if (failures.Count > 1)
                    throw new AggregateException("Windows could not restore every previous output.", failures);
                throw;
            }
            finally { Marshal.ReleaseComObject(policy); }
        }

        private static string FriendlyName(IMMDevice device)
        {
            IPropertyStore properties;
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out properties));
            try
            {
                var key = new PropertyKey
                {
                    FormatId = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                    PropertyId = 14
                };
                PropVariant value;
                Marshal.ThrowExceptionForHR(properties.GetValue(ref key, out value));
                try
                {
                    return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "Audio output" : "Audio output";
                }
                finally { PropVariantClear(ref value); }
            }
            finally { Marshal.ReleaseComObject(properties); }
        }

        public void Dispose()
        {
            if (enumerator == null) return;
            Marshal.ReleaseComObject(enumerator);
            enumerator = null;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant value);

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            internal Guid FormatId;
            internal uint PropertyId;
        }

        // The native union occupies 24 bytes on 64-bit Windows, even for a string value.
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant
        {
            [FieldOffset(0)] internal ushort Type;
            [FieldOffset(8)] internal IntPtr Pointer;
        }

        [ComImport, Guid("bcde0395-e52f-467c-8e3d-c4579291692e")]
        private class MMDeviceEnumerator { }

        [ComImport, Guid("a95664d2-9614-4f35-a746-de8db63617e6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
        }

        [ComImport, Guid("0bd7a1be-7a1a-44db-8397-cc5392387b5e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("d666063f-1587-4e43-81f1-b948e807363f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid id, uint context, IntPtr parameters, out IntPtr instance);
            [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out uint state);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }

        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class PolicyConfigClient { }

        // shortcut: Windows supplies no documented default-output setter, revisit this COM interface if Windows changes it.
        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr format);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int defaults, out IntPtr format);
            [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpoint, IntPtr mix);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int defaults, IntPtr period, IntPtr minimum);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
        }
    }
}
