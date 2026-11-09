using System.Buffers.Binary;
using System.Text;

namespace OneBase.Infrastructure.Recruitment.Documents;

/// <summary>
/// Чтение составного файла OLE (Compound File Binary, MS-CFB) — контейнер старых документов Office (.doc). Только чтение потоков по имени;
/// каждая цепочка секторов ограничена числом секторов файла (защита от петель), все смещения проверяются на выход за границы.
/// </summary>
internal sealed class CompoundFile
{
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FreeSector = 0xFFFFFFFF;
    private const int MaxStreamBytes = 64 * 1024 * 1024;

    private readonly byte[] _data;
    private readonly int _sectorSize;
    private readonly int _miniSectorSize;
    private readonly uint _miniCutoff;
    private readonly uint[] _fat;
    private readonly uint[] _miniFat;
    private readonly List<(string Name, int Type, uint Start, long Size)> _entries = [];
    private readonly byte[] _miniStream;

    public CompoundFile(byte[] data)
    {
        _data = data;
        if (data.Length < 512 || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
        {
            throw new InvalidDataException("Не составной файл OLE.");
        }

        var sectorShift = U16(0x1E);
        var miniShift = U16(0x20);
        if (sectorShift is not (9 or 12) || miniShift != 6)
        {
            throw new InvalidDataException("Неизвестный размер сектора.");
        }

        _sectorSize = 1 << sectorShift;
        _miniSectorSize = 1 << miniShift;
        _miniCutoff = U32(0x38);
        var fatSectors = U32(0x2C);
        var firstDirectory = U32(0x30);
        var firstMiniFat = U32(0x3C);
        var miniFatSectors = U32(0x40);
        var firstDifat = U32(0x44);
        var difatSectors = U32(0x48);
        var totalSectors = (data.Length - _sectorSize) / _sectorSize + 1;
        if (fatSectors > totalSectors || miniFatSectors > totalSectors || difatSectors > totalSectors)
        {
            throw new InvalidDataException("Повреждённый заголовок.");
        }

        // DIFAT: 109 номеров в заголовке, дальше — цепочка секторов DIFAT.
        var fatSectorList = new List<uint>();
        for (var i = 0; i < 109 && fatSectorList.Count < fatSectors; i++)
        {
            var s = U32(0x4C + i * 4);
            if (s != FreeSector)
            {
                fatSectorList.Add(s);
            }
        }

        var difat = firstDifat;
        for (var n = 0; n < difatSectors && difat < EndOfChain && fatSectorList.Count < fatSectors; n++)
        {
            var offset = SectorOffset(difat);
            var perSector = _sectorSize / 4 - 1;
            for (var i = 0; i < perSector && fatSectorList.Count < fatSectors; i++)
            {
                var s = U32(offset + i * 4);
                if (s != FreeSector)
                {
                    fatSectorList.Add(s);
                }
            }

            difat = U32(offset + perSector * 4);
        }

        _fat = new uint[fatSectorList.Count * (_sectorSize / 4)];
        for (var i = 0; i < fatSectorList.Count; i++)
        {
            var offset = SectorOffset(fatSectorList[i]);
            for (var j = 0; j < _sectorSize / 4; j++)
            {
                _fat[i * (_sectorSize / 4) + j] = U32(offset + j * 4);
            }
        }

        var miniFatBytes = Chain(firstMiniFat, (long)miniFatSectors * _sectorSize);
        _miniFat = new uint[miniFatBytes.Length / 4];
        for (var i = 0; i < _miniFat.Length; i++)
        {
            _miniFat[i] = BinaryPrimitives.ReadUInt32LittleEndian(miniFatBytes.AsSpan(i * 4));
        }

        var directory = Chain(firstDirectory, long.MaxValue);
        for (var offset = 0; offset + 128 <= directory.Length; offset += 128)
        {
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(offset + 64));
            var type = directory[offset + 66];
            if (type == 0 || nameLength is < 2 or > 64)
            {
                continue;
            }

            var name = Encoding.Unicode.GetString(directory, offset, nameLength - 2);
            var start = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(offset + 116));
            var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(directory.AsSpan(offset + 120));
            if (U16(0x1A) == 3)
            {
                size &= 0xFFFFFFFF; // версия 3: старшие 4 байта не используются
            }

            _entries.Add((name, type, start, size));
        }

        var root = _entries.FirstOrDefault(e => e.Type == 5);
        _miniStream = root.Name is null ? [] : Chain(root.Start, root.Size);
    }

    public bool Has(string name) => _entries.Any(e => e.Type == 2 && e.Name == name);

    /// <summary>Поток по имени (WordDocument, 0Table, 1Table…); нет такого — null.</summary>
    public byte[]? Stream(string name)
    {
        var entry = _entries.FirstOrDefault(e => e.Type == 2 && e.Name == name);
        if (entry.Name is null)
        {
            return null;
        }

        if (entry.Size > MaxStreamBytes)
        {
            throw new InvalidDataException("Слишком большой поток.");
        }

        return entry.Size < _miniCutoff ? MiniChain(entry.Start, entry.Size) : Chain(entry.Start, entry.Size);
    }

    private byte[] Chain(uint start, long size)
    {
        using var buffer = new MemoryStream();
        var sector = start;
        var guard = 0;
        while (sector < EndOfChain && buffer.Length < size)
        {
            if (++guard > _fat.Length + 1 || sector >= _fat.Length)
            {
                throw new InvalidDataException("Повреждённая цепочка секторов.");
            }

            var offset = SectorOffset(sector);
            buffer.Write(_data, offset, _sectorSize);
            sector = _fat[sector];
        }

        var bytes = buffer.ToArray();
        return size < bytes.Length ? bytes[..(int)size] : bytes;
    }

    private byte[] MiniChain(uint start, long size)
    {
        using var buffer = new MemoryStream();
        var sector = start;
        var guard = 0;
        while (sector < EndOfChain && buffer.Length < size)
        {
            var offset = (long)sector * _miniSectorSize;
            if (++guard > _miniFat.Length + 1 || sector >= _miniFat.Length || offset + _miniSectorSize > _miniStream.Length)
            {
                throw new InvalidDataException("Повреждённая цепочка мини-секторов.");
            }

            buffer.Write(_miniStream, (int)offset, _miniSectorSize);
            sector = _miniFat[sector];
        }

        var bytes = buffer.ToArray();
        return size < bytes.Length ? bytes[..(int)size] : bytes;
    }

    private int SectorOffset(uint sector)
    {
        var offset = ((long)sector + 1) * _sectorSize;
        if (offset + _sectorSize > _data.Length)
        {
            throw new InvalidDataException("Сектор за концом файла.");
        }

        return (int)offset;
    }

    private ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset));

    private uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset));
}

/// <summary>
/// Текст документа Word 97–2003 (.doc, MS-DOC): FIB из потока WordDocument → CLX в потоке таблиц (0Table или 1Table) → таблица кусков
/// (PlcPcd) → основной текст (первые ccpText символов): куски в Unicode или «сжатые» (Windows-1252). Поля: код поля убирается, остаётся
/// результат; конец ячейки — « | », конец строки таблицы — перевод строки. Макросы не выполняются — читается только текст. Зашифрованный
/// документ — понятная ошибка.
/// </summary>
internal static class WordBinary
{
    static WordBinary() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string Extract(byte[] data)
    {
        var file = new CompoundFile(data);
        var word = file.Stream("WordDocument") ?? throw new InvalidDataException("В файле нет потока WordDocument — это не документ Word.");
        if (word.Length < 0x1AA || BinaryPrimitives.ReadUInt16LittleEndian(word) != 0xA5EC)
        {
            throw new InvalidDataException("Повреждённый документ Word (нет заголовка FIB).");
        }

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(word.AsSpan(0x0A));
        if ((flags & 0x0100) != 0)
        {
            throw new NotSupportedException("Документ Word защищён паролем — снимите защиту и пришлите снова.");
        }

        var table = file.Stream((flags & 0x0200) != 0 ? "1Table" : "0Table") ?? throw new InvalidDataException("В документе Word нет таблицы кусков текста.");
        var ccpText = BinaryPrimitives.ReadInt32LittleEndian(word.AsSpan(0x4C));
        var fcClx = BinaryPrimitives.ReadInt32LittleEndian(word.AsSpan(0x1A2));
        var lcbClx = BinaryPrimitives.ReadInt32LittleEndian(word.AsSpan(0x1A6));
        if (fcClx < 0 || lcbClx <= 0 || (long)fcClx + lcbClx > table.Length || ccpText < 0)
        {
            throw new InvalidDataException("Повреждённый документ Word (CLX за пределами потока).");
        }

        // CLX: Prc (0x01, размер int16, данные)…, затем Pcdt (0x02, размер int32, PlcPcd).
        var pos = fcClx;
        var end = fcClx + lcbClx;
        while (pos < end && table[pos] == 0x01)
        {
            pos += 3 + BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(pos + 1));
        }

        if (pos + 5 > end || table[pos] != 0x02)
        {
            throw new InvalidDataException("Повреждённый документ Word (нет таблицы кусков).");
        }

        var lcb = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(pos + 1));
        var plc = pos + 5;
        if (lcb < 4 || plc + lcb > table.Length)
        {
            throw new InvalidDataException("Повреждённый документ Word (таблица кусков).");
        }

        var pieces = (lcb - 4) / 12;
        var sb = new StringBuilder();
        var cp1252 = Encoding.GetEncoding(1252);
        for (var i = 0; i < pieces && sb.Length < ccpText; i++)
        {
            var cpStart = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(plc + i * 4));
            var cpEnd = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(plc + (i + 1) * 4));
            var pcd = plc + (pieces + 1) * 4 + i * 8;
            var fcValue = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(pcd + 2));
            var compressed = (fcValue & 0x40000000) != 0;
            var fc = (int)(fcValue & 0x3FFFFFFF);
            var count = Math.Min(cpEnd - cpStart, ccpText - sb.Length);
            if (count <= 0)
            {
                continue;
            }

            if (compressed)
            {
                var offset = fc / 2;
                if (offset < 0 || offset + count > word.Length)
                {
                    throw new InvalidDataException("Повреждённый документ Word (кусок текста за концом).");
                }

                sb.Append(cp1252.GetString(word, offset, count));
            }
            else
            {
                if (fc < 0 || (long)fc + count * 2L > word.Length)
                {
                    throw new InvalidDataException("Повреждённый документ Word (кусок текста за концом).");
                }

                sb.Append(Encoding.Unicode.GetString(word, fc, count * 2));
            }
        }

        return Clean(sb.ToString());
    }

    /// <summary>Служебные символы Word: абзац, ячейка, поле (код убирается, результат остаётся), объекты, мягкий перенос.</summary>
    private static string Clean(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var fieldDepth = 0; // внутри кода поля (между 0x13 и 0x14)
        var codeLevels = new Stack<bool>();
        foreach (var c in raw)
        {
            switch (c)
            {
                case '\u0013':
                    codeLevels.Push(true);
                    fieldDepth++;
                    continue;
                case '\u0014':
                    if (codeLevels.Count > 0 && codeLevels.Peek())
                    {
                        codeLevels.Pop();
                        codeLevels.Push(false);
                        fieldDepth--;
                    }

                    continue;
                case '\u0015':
                    if (codeLevels.Count > 0 && codeLevels.Pop())
                    {
                        fieldDepth--;
                    }

                    continue;
            }

            if (fieldDepth > 0)
            {
                continue;
            }

            switch (c)
            {
                case '\r' or '\u000B' or '\u000C':
                    sb.Append('\n');
                    break;
                case '\u0007':
                    sb.Append(" | ");
                    break;
                case '\u001E':
                    sb.Append('-');
                    break;
                case ' ':
                    sb.Append(' ');
                    break;
                case '\t':
                    sb.Append('\t');
                    break;
                case < ' ':
                    break; // объекты, сноски, мягкий перенос
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString().Replace(" | \n", "\n").Trim();
    }
}
