using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Lou.Diagnostics;

// Standard Windows metadata/descriptor queries only. No WinUSB initialization, vendor request,
// HID report input/output, bus reset, driver installation, or LOU/RGB runtime dependency.
public static class WindowsInventory
{
    private sealed record Node(uint DevInst, uint Parent, string Id, string Name, string Class, string Service, string Driver, uint Port, uint Problem);
    private sealed record DevicePath(uint DevInst, string Path);
    private static readonly Guid HubGuid = new("f18a0e88-c30c-11d0-8815-00a0c906bed8");
    private static readonly Guid HidGuid = new("4d1e55b2-f16f-11cf-88cb-001111000030");
    public static DisplayReport Collect(Action<DisplayReport> checkpoint)
    {
        var report = new DisplayReport();
        checkpoint(report);
        var nodes = Nodes();
        var aliases = new Dictionary<uint, string>();
        string Alias(uint id) { if (!aliases.TryGetValue(id, out var name)) aliases[id] = name = "node-" + (aliases.Count + 1); return name; }
        var selected = new Dictionary<uint, DisplayInfo>();
        var descriptions = new Dictionary<uint, DisplayInfo>();
        foreach (var n in nodes.Where(n => n.Id.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase)))
        {
            string id = DisplayRules.UsbId(n.Id);
            // MI_ children are represented as interfaces of their physical USB parent.
            if (n.Id.Contains("&MI_", StringComparison.OrdinalIgnoreCase) || DisplayRules.Excluded(n.Name, n.Class)) continue;
            bool known = DisplayRules.Known(id), named = DisplayRules.NamedScreen(n.Name);
            // Generic WinUSB devices can be proprietary LCDs. Keep these as explicitly unconfirmed candidates.
            bool generic = n.Service.Equals("WinUSB", StringComparison.OrdinalIgnoreCase) ||
                n.Service.Equals("libusbK", StringComparison.OrdinalIgnoreCase) || n.Name.Contains("Unknown USB", StringComparison.OrdinalIgnoreCase) ||
                n.Name.Contains("USB inconnu", StringComparison.OrdinalIgnoreCase);
            if (!known && !named && !generic) continue;
            selected[n.DevInst] = Make(n, known, named);
        }
        DisplayInfo Make(Node n, bool known, bool named) => new()
        {
            Alias = Alias(n.DevInst), Parent = Alias(n.Parent), UsbId = DisplayRules.UsbId(n.Id),
            Product = DisplayRules.ProductLabel(n.Name), DriverService = DisplayRules.ServiceLabel(n.Service),
            DriverVersion = DriverVersion(n.Driver), WindowsProblemCode = n.Problem,
            InterfaceDrivers = nodes.Where(child => child.Parent == n.DevInst && System.Text.RegularExpressions.Regex.IsMatch(child.Id, @"&MI_[0-9A-F]{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Select(child => new InterfaceDriverInfo(System.Text.RegularExpressions.Regex.Match(child.Id, @"MI_[0-9A-F]{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Value.ToUpperInvariant(), DisplayRules.ServiceLabel(child.Service), DriverVersion(child.Driver), child.Problem)).ToList(),
            Classification = known ? "Known LCD USB family; model not validated" : named ? "Display name hint; not validated" : "Unidentified proprietary USB candidate; may not be a screen",
            LouSupport = known ? "USB family recognized by LOU; model, geometry and format not tested" : "Not in LOU's two recognized USB families"
        };
        report.Devices = selected.Values.ToList(); checkpoint(report);
        int inaccessibleHubs = 0;
        foreach (var hub in Paths(HubGuid).Take(64))
        {
            // Zero desired access: metadata IOCTLs only, do not claim the device or change configuration.
            using var handle = CreateFile(hub.Path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) { inaccessibleHubs++; continue; }
            var info = new byte[76];
            if (!Query(handle, 0x220408, info, out _)) { inaccessibleHubs++; continue; }
            int ports = Math.Min(127, (int)info[6]);
            for (uint port = 1; port <= ports; port++)
            {
                var connection = new byte[4096]; BitConverter.GetBytes(port).CopyTo(connection, 0);
                if (!Query(handle, 0x220448, connection, out int received) || !DisplayRules.ConnectionPresent(connection.AsSpan(0, received))) continue;
                // SPDRP_ADDRESS is the USB device address, NOT a hub port number. Match the
                // port's driver key to the PnP node instead; never guess by enumeration order.
                var key = new byte[4096]; BitConverter.GetBytes(port).CopyTo(key, 0);
                if (!Query(handle, 0x220420, key, out int keyBytes) || keyBytes < 10) continue;
                string driverKey = Encoding.Unicode.GetString(key, 8, Math.Min(keyBytes, key.Length) - 8).TrimEnd('\0');
                var matches = nodes.Where(n => n.Parent == hub.DevInst && n.Driver.Equals(driverKey, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length != 1) continue;
                var node = matches[0];
                if (DisplayRules.Excluded(node.Name, node.Class)) continue;
                // Device class 0/FF can hide proprietary screens. Other classes are only inspected for an already selected screen.
                if (!selected.ContainsKey(node.DevInst) && connection[8] is not (0 or 0xff)) continue;
                var config = Configuration(handle, port);
                var parsed = new DisplayInfo();
                if (config.Length > 0) DisplayRules.ParseConfiguration(config, parsed);
                parsed.Revision = $"bcdDevice=0x{BitConverter.ToUInt16(connection, 16):X4}; bcdUSB=0x{BitConverter.ToUInt16(connection, 6):X4}";
                parsed.UsbSpeed = connection[23] switch { 0 => "Low (1.5 Mbit/s)", 1 => "Full (12 Mbit/s)", 2 => "High (480 Mbit/s)", 3 => "SuperSpeed (EX does not distinguish later variants)", _ => "Unknown" };
                descriptions[node.DevInst] = parsed;
                if (!selected.TryGetValue(node.DevInst, out var display))
                {
                    bool vendorBulk = parsed.Interfaces.Any(i => i.Class == 255 && parsed.Endpoints.Any(e => e.Interface == i.Number && e.TransferType == 2));
                    if (!vendorBulk) continue;
                    display = Make(node, false, false); selected.Add(node.DevInst, display);
                }
                display.Revision = parsed.Revision; display.UsbSpeed = parsed.UsbSpeed;
                display.Interfaces = parsed.Interfaces; display.Endpoints = parsed.Endpoints; display.Notes.AddRange(parsed.Notes);
                if (config.Length == 0) display.Notes.Add("Configuration descriptor unavailable; no proprietary request attempted.");
                report.Devices = selected.Values.ToList(); checkpoint(report);
            }
        }
        if (inaccessibleHubs > 0) report.Notes.Add("Some USB hub metadata was inaccessible; descriptor coverage is partial. No driver was changed.");
        // Vendor HID usage pages can also carry screens. Never read HID reports or input events.
        foreach (var hid in Paths(HidGuid).Take(512))
        {
            var branch = Branch(nodes, hid.DevInst).TakeWhile(n => !n.Id.StartsWith("USB\\ROOT_", StringComparison.OrdinalIgnoreCase) && !n.Service.Contains("hub", StringComparison.OrdinalIgnoreCase)).ToList();
            if (branch.Any(n => DisplayRules.Excluded(n.Name, n.Class))) continue;
            var root = branch.LastOrDefault(n => n.Id.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase));
            if (root == null) continue;
            using var handle = CreateFile(hid.Path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid || !HidD_GetPreparsedData(handle, out var pre)) continue;
            try
            {
                if (HidP_GetCaps(pre, out var caps) != 0x110000) continue;
                bool already = selected.TryGetValue(root.DevInst, out var display);
                if (caps.UsagePage < 0xff00 || (!already && caps.OutputReportByteLength < 64)) continue;
                if (!already) selected[root.DevInst] = display = Make(root, DisplayRules.Known(DisplayRules.UsbId(root.Id)), DisplayRules.NamedScreen(root.Name));
                if (!already && descriptions.TryGetValue(root.DevInst, out var descriptor))
                { display!.Revision = descriptor.Revision; display.UsbSpeed = descriptor.UsbSpeed; display.Interfaces = descriptor.Interfaces; display.Endpoints = descriptor.Endpoints; display.Notes.AddRange(descriptor.Notes); }
                display!.HidCapabilities.Add($"usagePage=0x{caps.UsagePage:X4}; usage=0x{caps.Usage:X4}; inputBytes={caps.InputReportByteLength}; outputBytes={caps.OutputReportByteLength}; featureBytes={caps.FeatureReportByteLength}");
            }
            finally { HidD_FreePreparsedData(pre); }
        }
        report.Devices = selected.Values.ToList();
        foreach (var d in report.Devices) if (d.Interfaces.Count == 0) d.Notes.Add("USB interface descriptors unavailable. PnP/HID metadata only; device access was not forced.");
        report.Notes.Add("Monitors, recognized RGB/input/storage/audio/network peripherals are excluded. Generic proprietary USB/HID devices cannot always be classified without a documented protocol.");
        report.Notes.Add("No serial, container ID, raw device path, USB string descriptor, process inventory or personal LOU settings collected for export.");
        report.Notes.Add("This tool cannot identify screens hidden behind an unrecognized proprietary controller, GPU interface, Bluetooth or network service. An empty result does not prove absence.");
        report.Complete = true; checkpoint(report); return report;
    }
    private static List<Node> Branch(List<Node> nodes, uint id)
    {
        var result = new List<Node>();
        for (int i = 0; i < 16; i++) { var n = nodes.FirstOrDefault(n => n.DevInst == id); if (n == null || result.Contains(n)) break; result.Add(n); id = n.Parent; }
        return result;
    }
    private static byte[] Configuration(SafeFileHandle hub, uint port)
    {
        byte[] Request(int count)
        {
            var data = new byte[12 + count]; BitConverter.GetBytes(port).CopyTo(data, 0);
            data[4] = 0x80; data[5] = 6; data[7] = 2; BitConverter.GetBytes((ushort)count).CopyTo(data, 10);
            if (!Query(hub, 0x220410, data, out int received) || received < 14) return [];
            return data.AsSpan(12, Math.Min(count, received - 12)).ToArray();
        }
        var header = Request(9); if (header.Length < 9 || header[1] != 2) return [];
        int length = header[2] | header[3] << 8; if (length is < 9 or > 16384) return [];
        return Request(length);
    }
    private static bool Query(SafeFileHandle h, uint code, byte[] buffer, out int received) => DeviceIoControl(h, code, buffer, buffer.Length, buffer, buffer.Length, out received, IntPtr.Zero);
    private static string DriverVersion(string key)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^\{[0-9A-Fa-f-]{36}\}\\\d{4}$")) return "Unavailable";
        try { using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\" + key); return DisplayRules.VersionLabel(k?.GetValue("DriverVersion") as string ?? ""); } catch { return "Unavailable"; }
    }
    private static List<Node> Nodes()
    {
        var result = new List<Node>(); var set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 6);
        if (set == new IntPtr(-1)) throw new InvalidOperationException("Windows device inventory unavailable.");
        try
        {
            for (uint i = 0; i < 8192; i++)
            {
                var d = new DevInfo { Size = Marshal.SizeOf<DevInfo>() };
                if (!SetupDiEnumDeviceInfo(set, i, ref d)) break;
                var id = new StringBuilder(1024); if (CM_Get_Device_ID(d.DevInst, id, id.Capacity, 0) != 0) continue;
                CM_Get_Parent(out uint parent, d.DevInst, 0); CM_Get_DevNode_Status(out _, out uint problem, d.DevInst, 0);
                string Property(uint property) { var b = new byte[8192]; return SetupDiGetDeviceRegistryProperty(set, ref d, property, out _, b, b.Length, out _) ? Encoding.Unicode.GetString(b).TrimEnd('\0') : ""; }
                var port = new byte[4]; uint address = SetupDiGetDeviceRegistryProperty(set, ref d, 28, out _, port, 4, out _) ? BitConverter.ToUInt32(port) : 0;
                var busKey = new PropertyKey { Format = new("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), Id = 4 };
                var busBuffer = new byte[4096];
                string busName = SetupDiGetDeviceProperty(set, ref d, ref busKey, out _, busBuffer, busBuffer.Length, out _, 0) ? Encoding.Unicode.GetString(busBuffer).TrimEnd('\0') : "";
                result.Add(new(d.DevInst, parent, id.ToString(), Property(0) + " " + busName, Property(7), Property(4), Property(9), address, problem));
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }
    private static IEnumerable<DevicePath> Paths(Guid guid)
    {
        var set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 18); if (set == new IntPtr(-1)) yield break;
        try
        {
            for (uint i = 0; i < 1024; i++)
            {
                var face = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref face)) break;
                var node = new DevInfo { Size = Marshal.SizeOf<DevInfo>() };
                SetupDiGetDeviceInterfaceDetail(set, ref face, IntPtr.Zero, 0, out int required, ref node);
                if (required is < 8 or > 16384) continue;
                var buffer = Marshal.AllocHGlobal(required);
                try
                {
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetail(set, ref face, buffer, required, out _, ref node))
                        yield return new(node.DevInst, Marshal.PtrToStringUni(buffer + 4) ?? "");
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct DevInfo { public int Size; public Guid Class; public uint DevInst; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct InterfaceData { public int Size; public Guid Class; public uint Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] private struct HidCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort LinkCollectionNodes, InputButtonCaps, InputValueCaps, InputDataIndices, OutputButtonCaps, OutputValueCaps, OutputDataIndices, FeatureButtonCaps, FeatureValueCaps, FeatureDataIndices;
    }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SetupDiGetClassDevs(IntPtr guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DevInfo info);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref DevInfo info, uint property, out uint type, byte[] buffer, int size, out int required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceProperty(IntPtr set, ref DevInfo info, ref PropertyKey key, out uint type, byte[] buffer, int size, out int required, uint flags);
    [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, int size, out int required, ref DevInfo device);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID(uint node, StringBuilder id, int length, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);
    [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);
}


