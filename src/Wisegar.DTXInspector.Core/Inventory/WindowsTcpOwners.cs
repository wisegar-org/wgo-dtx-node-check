using System.Buffers.Binary;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Wisegar.DTXInspector.Inventory;

internal sealed record OwnedTcpEndpoint(string LocalAddress, int LocalPort, string RemoteAddress,
    int RemotePort, TcpState State, int ProcessId);

internal static class WindowsTcpOwners
{
    internal static IReadOnlyList<OwnedTcpEndpoint> Read(List<string> errors)
    {
        var endpoints = new List<OwnedTcpEndpoint>();
        foreach (var family in new[] { 2, 23 }) // AF_INET, AF_INET6 on Windows
        {
            try { endpoints.AddRange(ReadFamily(family)); }
            catch (Exception ex) when (ex is Win32Exception or InvalidDataException)
            { errors.Add($"TCP {(family == 2 ? "IPv4" : "IPv6")}: {ex.Message}"); }
        }
        return endpoints;
    }

    private static IReadOnlyList<OwnedTcpEndpoint> ReadFamily(int family)
    {
        var size = 0;
        var error = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 5, 0); // OWNER_PID_ALL
        if (error != 0 && error != 122) throw new Win32Exception((int)error);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (size < 4 || size > 64 * 1024 * 1024) throw new InvalidDataException("Dimensione tabella TCP non valida.");
            var capacity = size;
            var buffer = Marshal.AllocHGlobal(capacity);
            try
            {
                error = GetExtendedTcpTable(buffer, ref size, false, family, 5, 0);
                if (error == 122) continue;
                if (error != 0) throw new Win32Exception((int)error);
                var bytes = new byte[capacity];
                Marshal.Copy(buffer, bytes, 0, capacity);
                return Decode(bytes, family == 23);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidDataException("Tabella TCP cambiata durante la lettura; ripetere l'ispezione.");
    }

    internal static IReadOnlyList<OwnedTcpEndpoint> Decode(byte[] bytes, bool ipv6)
    {
        if (bytes.Length < 4) throw new InvalidDataException("Tabella TCP incompleta.");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var rowSize = ipv6 ? 56 : 24;
        if (count > (bytes.Length - 4) / rowSize) throw new InvalidDataException("Righe TCP incomplete.");
        var result = new List<OwnedTcpEndpoint>();
        for (var i = 0; i < count; i++)
        {
            var row = bytes.AsSpan(4 + i * rowSize, rowSize);
            if (ipv6)
                result.Add(new(new IPAddress(row[..16], BinaryPrimitives.ReadUInt32LittleEndian(row[16..])).ToString(),
                    BinaryPrimitives.ReadUInt16BigEndian(row[20..]),
                    new IPAddress(row.Slice(24, 16), BinaryPrimitives.ReadUInt32LittleEndian(row[40..])).ToString(),
                    BinaryPrimitives.ReadUInt16BigEndian(row[44..]),
                    (TcpState)BinaryPrimitives.ReadInt32LittleEndian(row[48..]), BinaryPrimitives.ReadInt32LittleEndian(row[52..])));
            else
                result.Add(new(new IPAddress(row.Slice(4, 4)).ToString(), BinaryPrimitives.ReadUInt16BigEndian(row[8..]),
                    new IPAddress(row.Slice(12, 4)).ToString(), BinaryPrimitives.ReadUInt16BigEndian(row[16..]),
                    (TcpState)BinaryPrimitives.ReadInt32LittleEndian(row), BinaryPrimitives.ReadInt32LittleEndian(row[20..])));
        }
        return result;
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size,
        [MarshalAs(UnmanagedType.Bool)] bool order, int family, int tableClass, uint reserved);
}
