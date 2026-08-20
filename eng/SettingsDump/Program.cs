// Offline reader for a packaged app's Settings\settings.dat (Windows registry hive, "regf").
// ApplicationData.LocalSettings values live under the "LocalState" key. Values use custom
// REG types (0x5f5e101.. etc.) encoding WinRT property types, each value followed by an 8-byte timestamp.
// Usage: dotnet run -- <settings.dat> [out.json]
using System.Text;
using System.Text.Json;

var path = args.Length > 0 ? args[0] : "settings.dat";
var outPath = args.Length > 1 ? args[1] : null;
var data = File.ReadAllBytes(path);
if (Encoding.ASCII.GetString(data, 0, 4) != "regf") throw new Exception("Not a regf hive");

const int hbinBase = 0x1000;
int rootCellOffset = BitConverter.ToInt32(data, 0x24);

int Cell(int off) => hbinBase + off; // offsets are relative to first hbin; cell starts with int32 size
string Sig(int abs) => Encoding.ASCII.GetString(data, abs + 4, 2);

var result = new Dictionary<string, Dictionary<string, object?>>();
WalkKey(rootCellOffset, "");
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (outPath != null) File.WriteAllText(outPath, json);

void WalkKey(int cellOff, string pathSoFar)
{
    int abs = Cell(cellOff);
    if (Sig(abs) != "nk") return;
    int nameLen = BitConverter.ToUInt16(data, abs + 4 + 0x48);
    ushort flags = BitConverter.ToUInt16(data, abs + 4 + 0x02);
    string name = (flags & 0x20) != 0
        ? Encoding.Latin1.GetString(data, abs + 4 + 0x4C, nameLen)
        : Encoding.Unicode.GetString(data, abs + 4 + 0x4C, nameLen);
    string full = string.IsNullOrEmpty(pathSoFar) ? name : pathSoFar + "\\" + name;

    int valueCount = BitConverter.ToInt32(data, abs + 4 + 0x24);
    int valueListOff = BitConverter.ToInt32(data, abs + 4 + 0x28);
    if (valueCount > 0 && valueListOff != -1)
    {
        var values = new Dictionary<string, object?>();
        int listAbs = Cell(valueListOff) + 4;
        for (int i = 0; i < valueCount; i++)
        {
            int vkOff = BitConverter.ToInt32(data, listAbs + i * 4);
            ReadValue(vkOff, values);
        }
        result[full] = values;
    }

    int subCount = BitConverter.ToInt32(data, abs + 4 + 0x14);
    int subListOff = BitConverter.ToInt32(data, abs + 4 + 0x1C);
    if (subCount > 0 && subListOff != -1) WalkSubkeyList(subListOff, full);
}

void WalkSubkeyList(int cellOff, string parent)
{
    int abs = Cell(cellOff);
    string sig = Sig(abs);
    int count = BitConverter.ToUInt16(data, abs + 4 + 2);
    int entries = abs + 4 + 4;
    switch (sig)
    {
        case "lf": case "lh":
            for (int i = 0; i < count; i++) WalkKey(BitConverter.ToInt32(data, entries + i * 8), parent);
            break;
        case "li":
            for (int i = 0; i < count; i++) WalkKey(BitConverter.ToInt32(data, entries + i * 4), parent);
            break;
        case "ri":
            for (int i = 0; i < count; i++) WalkSubkeyList(BitConverter.ToInt32(data, entries + i * 4), parent);
            break;
    }
}

void ReadValue(int cellOff, Dictionary<string, object?> into)
{
    int abs = Cell(cellOff);
    if (Sig(abs) != "vk") return;
    int nameLen = BitConverter.ToUInt16(data, abs + 4 + 2);
    int dataLen = BitConverter.ToInt32(data, abs + 4 + 4);
    int dataOff = BitConverter.ToInt32(data, abs + 4 + 8);
    uint type = BitConverter.ToUInt32(data, abs + 4 + 12);
    ushort flags = BitConverter.ToUInt16(data, abs + 4 + 16);
    string name = nameLen == 0 ? "(default)" : (flags & 1) != 0
        ? Encoding.Latin1.GetString(data, abs + 4 + 20, nameLen)
        : Encoding.Unicode.GetString(data, abs + 4 + 20, nameLen);

    byte[] bytes;
    bool inline = (dataLen & 0x80000000) != 0;
    int len = dataLen & 0x7FFFFFFF;
    if (inline) { bytes = new byte[len]; Array.Copy(data, abs + 4 + 8, bytes, 0, len); }
    else { bytes = new byte[len]; Array.Copy(data, Cell(dataOff) + 4, bytes, 0, len); }

    // WinRT ApplicationDataContainer types: REG type = 0x5f5e100 + PropertyType; trailing 8 bytes = FILETIME.
    int payloadLen = Math.Max(0, bytes.Length - 8);
    var p = bytes.AsSpan(0, payloadLen);
    object? value;
    string typeName;
    switch (type)
    {
        case 0x5f5e101: typeName = "UInt8"; value = p[0]; break;
        case 0x5f5e102: typeName = "Int16"; value = BitConverter.ToInt16(p); break;
        case 0x5f5e103: typeName = "UInt16"; value = BitConverter.ToUInt16(p); break;
        case 0x5f5e104: typeName = "Int32"; value = BitConverter.ToInt32(p); break;
        case 0x5f5e105: typeName = "UInt32"; value = BitConverter.ToUInt32(p); break;
        case 0x5f5e106: typeName = "Int64"; value = BitConverter.ToInt64(p); break;
        case 0x5f5e107: typeName = "UInt64"; value = BitConverter.ToUInt64(p); break;
        case 0x5f5e108: typeName = "Single"; value = BitConverter.ToSingle(p); break;
        case 0x5f5e109: typeName = "Double"; value = BitConverter.ToDouble(p); break;
        case 0x5f5e10a: typeName = "Char16"; value = BitConverter.ToChar(p); break;
        case 0x5f5e10b: typeName = "Boolean"; value = p[0] != 0; break;
        case 0x5f5e10c: typeName = "String"; value = Encoding.Unicode.GetString(p).TrimEnd('\0'); break;
        default: typeName = $"0x{type:x}"; value = Convert.ToHexString(p); break;
    }
    into[name] = new { type = typeName, value };
}
