using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace UOX3DfnStudio;

public static class ArtFileReader
{
    public sealed class ItemArtEntry
    {
        public int ItemId { get; set; }
        public string TileName { get; set; } = string.Empty;
        public int TileHeight { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public WriteableBitmap? PreviewBitmap { get; set; }

        public string DisplayText
        {
            get
            {
                string hexValue = "0x" + ItemId.ToString("X4");

                if (string.IsNullOrWhiteSpace(TileName))
                {
                    return hexValue;
                }

                return hexValue + " - " + TileName;
            }
        }
    }

    private sealed class StaticTileDataEntry
    {
        public string Name { get; set; } = string.Empty;
        public int Height { get; set; }
    }

    private sealed class UopEntry
    {
        public ulong Hash { get; set; }
        public long DataOffset { get; set; }
        public int CompressedLength { get; set; }
        public int DecompressedLength { get; set; }
        public ushort CompressionMethod { get; set; }
    }

    public static List<ItemArtEntry> LoadItemArtEntries(string artUopPath, string tileDataPath)
    {
        var output = new List<ItemArtEntry>();

        if (string.IsNullOrWhiteSpace(artUopPath) || !File.Exists(artUopPath))
        {
            return output;
        }

        if (string.IsNullOrWhiteSpace(tileDataPath) || !File.Exists(tileDataPath))
        {
            return output;
        }

        var staticTileData = LoadStaticTileData(tileDataPath);
        if (staticTileData.Count == 0)
        {
            return output;
        }

        var uopEntries = LoadUopEntries(artUopPath);
        if (uopEntries.Count == 0)
        {
            return output;
        }

        using (var artStream = File.OpenRead(artUopPath))
        {
            foreach (var staticTileEntry in staticTileData)
            {
                int itemId = staticTileEntry.Key;
                int artIndex = 0x4000 + itemId;

                UopEntry? matchedEntry = FindArtEntry(uopEntries, artIndex);
                if (matchedEntry == null)
                {
                    continue;
                }

                byte[]? artData = ReadUopEntryData(artStream, matchedEntry);
                if (artData == null || artData.Length == 0)
                {
                    continue;
                }

                var previewBitmap = LoadStaticArtBitmap(artData);
                if (previewBitmap == null)
                {
                    continue;
                }

                output.Add(new ItemArtEntry
                {
                    ItemId = itemId,
                    TileName = staticTileEntry.Value.Name,
                    TileHeight = staticTileEntry.Value.Height,
                    ImageWidth = previewBitmap.PixelSize.Width,
                    ImageHeight = previewBitmap.PixelSize.Height,
                    PreviewBitmap = previewBitmap
                });
            }
        }

        output.Sort((leftEntry, rightEntry) => leftEntry.ItemId.CompareTo(rightEntry.ItemId));
        return output;
    }

    private static Dictionary<int, StaticTileDataEntry> LoadStaticTileData(string tileDataPath)
    {
        var output = new Dictionary<int, StaticTileDataEntry>();

        using (var fileStream = File.OpenRead(tileDataPath))
        using (var binaryReader = new BinaryReader(fileStream))
        {
            bool useNewFormat = IsNewTileDataFormat(fileStream.Length);

            int landGroupSize = useNewFormat ? 964 : 836;
            int staticGroupSize = useNewFormat ? 1316 : 1188;
            int staticStartOffset = landGroupSize * 512;

            if (fileStream.Length <= staticStartOffset)
            {
                return output;
            }

            fileStream.Position = staticStartOffset;

            int itemId = 0;

            while (fileStream.Position + staticGroupSize <= fileStream.Length)
            {
                binaryReader.ReadUInt32();

                for (int entryIndex = 0; entryIndex < 32; entryIndex++)
                {
                    if (useNewFormat)
                    {
                        binaryReader.ReadUInt64();
                    }
                    else
                    {
                        binaryReader.ReadUInt32();
                    }

                    binaryReader.ReadByte();
                    binaryReader.ReadByte();
                    binaryReader.ReadInt32();
                    binaryReader.ReadUInt16();
                    binaryReader.ReadUInt16();
                    binaryReader.ReadUInt16();
                    byte height = binaryReader.ReadByte();

                    byte[] nameBytes = binaryReader.ReadBytes(20);
                    string name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

                    output[itemId] = new StaticTileDataEntry
                    {
                        Name = name,
                        Height = height
                    };

                    itemId++;
                }
            }
        }

        return output;
    }

    private static bool IsNewTileDataFormat(long fileLength)
    {
        long oldLandLength = 512L * 836L;
        long newLandLength = 512L * 964L;

        if (fileLength > newLandLength && (fileLength - newLandLength) % 1316L == 0)
        {
            return true;
        }

        if (fileLength > oldLandLength && (fileLength - oldLandLength) % 1188L == 0)
        {
            return false;
        }

        return fileLength >= newLandLength;
    }

    private static Dictionary<ulong, UopEntry> LoadUopEntries(string artUopPath)
    {
        var output = new Dictionary<ulong, UopEntry>();

        using (var fileStream = File.OpenRead(artUopPath))
        using (var binaryReader = new BinaryReader(fileStream))
        {
            if (fileStream.Length < 28)
            {
                return output;
            }

            uint magic = binaryReader.ReadUInt32();
            if (magic != 0x50594D)
            {
                return output;
            }

            binaryReader.ReadUInt32();
            binaryReader.ReadUInt32();
            long nextBlockAddress = binaryReader.ReadInt64();
            binaryReader.ReadUInt32();
            binaryReader.ReadUInt32();

            while (nextBlockAddress > 0 && nextBlockAddress < fileStream.Length)
            {
                fileStream.Position = nextBlockAddress;

                int filesInBlock = binaryReader.ReadInt32();
                nextBlockAddress = binaryReader.ReadInt64();

                for (int fileIndex = 0; fileIndex < filesInBlock; fileIndex++)
                {
                    long dataHeaderAddress = binaryReader.ReadInt64();
                    int dataHeaderLength = binaryReader.ReadInt32();
                    int compressedLength = binaryReader.ReadInt32();
                    int decompressedLength = binaryReader.ReadInt32();
                    ulong fileHash = binaryReader.ReadUInt64();
                    binaryReader.ReadUInt32();
                    ushort compressionMethod = binaryReader.ReadUInt16();

                    if (dataHeaderAddress <= 0 || compressedLength <= 0)
                    {
                        continue;
                    }

                    long dataOffset = dataHeaderAddress + dataHeaderLength;
                    if (dataOffset < 0 || dataOffset >= fileStream.Length)
                    {
                        continue;
                    }

                    output[fileHash] = new UopEntry
                    {
                        Hash = fileHash,
                        DataOffset = dataOffset,
                        CompressedLength = compressedLength,
                        DecompressedLength = decompressedLength,
                        CompressionMethod = compressionMethod
                    };
                }
            }
        }

        return output;
    }

    private static UopEntry? FindArtEntry(Dictionary<ulong, UopEntry> uopEntries, int artIndex)
    {
        string fileName = string.Format("build/artlegacymul/{0:D8}.tga", artIndex);
        foreach (ulong candidateHash in BuildHashCandidates(fileName))
        {
            if (uopEntries.TryGetValue(candidateHash, out UopEntry? entry))
            {
                return entry;
            }
        }

        return null;
    }

    private static IEnumerable<ulong> BuildHashCandidates(string fileName)
    {
        yield return CreateUopHash(fileName);
        yield return CreateUopHash(fileName.ToLowerInvariant());
    }

    private static ulong CreateUopHash(string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        uint a = 0xDEADBEEF + (uint)bytes.Length;
        uint b = 0xDEADBEEF + (uint)bytes.Length;
        uint c = 0xDEADBEEF + (uint)bytes.Length;

        int offset = 0;
        int remaining = bytes.Length;

        while (remaining > 12)
        {
            a += (uint)(bytes[offset + 0] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
            b += (uint)(bytes[offset + 4] | (bytes[offset + 5] << 8) | (bytes[offset + 6] << 16) | (bytes[offset + 7] << 24));
            c += (uint)(bytes[offset + 8] | (bytes[offset + 9] << 8) | (bytes[offset + 10] << 16) | (bytes[offset + 11] << 24));

            Mix(ref a, ref b, ref c);

            offset += 12;
            remaining -= 12;
        }

        switch (remaining)
        {
            case 12:
                c += (uint)bytes[offset + 11] << 24;
                goto case 11;
            case 11:
                c += (uint)bytes[offset + 10] << 16;
                goto case 10;
            case 10:
                c += (uint)bytes[offset + 9] << 8;
                goto case 9;
            case 9:
                c += bytes[offset + 8];
                goto case 8;
            case 8:
                b += (uint)bytes[offset + 7] << 24;
                goto case 7;
            case 7:
                b += (uint)bytes[offset + 6] << 16;
                goto case 6;
            case 6:
                b += (uint)bytes[offset + 5] << 8;
                goto case 5;
            case 5:
                b += bytes[offset + 4];
                goto case 4;
            case 4:
                a += (uint)bytes[offset + 3] << 24;
                goto case 3;
            case 3:
                a += (uint)bytes[offset + 2] << 16;
                goto case 2;
            case 2:
                a += (uint)bytes[offset + 1] << 8;
                goto case 1;
            case 1:
                a += bytes[offset + 0];
                break;
            case 0:
                break;
        }

        Final(ref a, ref b, ref c);

        return ((ulong)b << 32) | c;
    }

    private static void Mix(ref uint a, ref uint b, ref uint c)
    {
        a -= c; a ^= RotateLeft(c, 4); c += b;
        b -= a; b ^= RotateLeft(a, 6); a += c;
        c -= b; c ^= RotateLeft(b, 8); b += a;
        a -= c; a ^= RotateLeft(c, 16); c += b;
        b -= a; b ^= RotateLeft(a, 19); a += c;
        c -= b; c ^= RotateLeft(b, 4); b += a;
    }

    private static void Final(ref uint a, ref uint b, ref uint c)
    {
        c ^= b; c -= RotateLeft(b, 14);
        a ^= c; a -= RotateLeft(c, 11);
        b ^= a; b -= RotateLeft(a, 25);
        c ^= b; c -= RotateLeft(b, 16);
        a ^= c; a -= RotateLeft(c, 4);
        b ^= a; b -= RotateLeft(a, 14);
        c ^= b; c -= RotateLeft(b, 24);
    }

    private static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }

    private static byte[]? ReadUopEntryData(FileStream fileStream, UopEntry entry)
    {
        if (entry.DataOffset < 0 || entry.DataOffset >= fileStream.Length)
        {
            return null;
        }

        fileStream.Position = entry.DataOffset;

        byte[] compressedData = new byte[entry.CompressedLength];
        int bytesRead = fileStream.Read(compressedData, 0, compressedData.Length);

        if (bytesRead != compressedData.Length)
        {
            return null;
        }

        bool isCompressed = entry.CompressionMethod != 0 && entry.DecompressedLength > entry.CompressedLength;

        if (!isCompressed)
        {
            return compressedData;
        }

        try
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                zlibStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap? LoadStaticArtBitmap(byte[] artData)
    {
        if (artData == null || artData.Length < 8)
        {
            return null;
        }

        using (var memoryStream = new MemoryStream(artData))
        using (var binaryReader = new BinaryReader(memoryStream))
        {
            binaryReader.ReadUInt32();
            short width = binaryReader.ReadInt16();
            short height = binaryReader.ReadInt16();

            if (width <= 0 || height <= 0 || width > 2048 || height > 2048)
            {
                return null;
            }

            if (memoryStream.Position + (height * 2L) > memoryStream.Length)
            {
                return null;
            }

            ushort[] lineOffsets = new ushort[height];
            for (int lineIndex = 0; lineIndex < height; lineIndex++)
            {
                lineOffsets[lineIndex] = binaryReader.ReadUInt16();
            }

            long dataStart = memoryStream.Position;
            byte[] pixelBytes = new byte[width * height * 4];

            int x = 0;
            int y = 0;
            memoryStream.Position = dataStart + (lineOffsets[0] * 2L);

            while (y < height && memoryStream.Position + 4 <= memoryStream.Length)
            {
                ushort xOffset = binaryReader.ReadUInt16();
                ushort runLength = binaryReader.ReadUInt16();

                if (xOffset + runLength >= 2048)
                {
                    break;
                }

                if (xOffset + runLength != 0)
                {
                    x += xOffset;

                    for (int runIndex = 0; runIndex < runLength; runIndex++)
                    {
                        if (memoryStream.Position + 2 > memoryStream.Length)
                        {
                            break;
                        }

                        ushort rawColor = binaryReader.ReadUInt16();

                        if (rawColor != 0 && x >= 0 && x < width && y >= 0 && y < height)
                        {
                            WritePixel(pixelBytes, width, x, y, ConvertUoColorToAvaloniaColor(rawColor));
                        }

                        x++;
                    }
                }
                else
                {
                    x = 0;
                    y++;

                    if (y >= height)
                    {
                        break;
                    }

                    long nextLinePosition = dataStart + (lineOffsets[y] * 2L);
                    if (nextLinePosition < 0 || nextLinePosition >= memoryStream.Length)
                    {
                        break;
                    }

                    memoryStream.Position = nextLinePosition;
                }
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            using (var framebuffer = bitmap.Lock())
            {
                Marshal.Copy(pixelBytes, 0, framebuffer.Address, pixelBytes.Length);
            }

            return bitmap;
        }
    }

    private static void WritePixel(byte[] pixelBytes, int width, int x, int y, Color color)
    {
        int pixelIndex = ((y * width) + x) * 4;

        pixelBytes[pixelIndex + 0] = color.B;
        pixelBytes[pixelIndex + 1] = color.G;
        pixelBytes[pixelIndex + 2] = color.R;
        pixelBytes[pixelIndex + 3] = color.A;
    }

    private static Color ConvertUoColorToAvaloniaColor(ushort rawColor)
    {
        int red = (rawColor >> 10) & 0x1F;
        int green = (rawColor >> 5) & 0x1F;
        int blue = rawColor & 0x1F;

        byte redByte = (byte)((red * 255) / 31);
        byte greenByte = (byte)((green * 255) / 31);
        byte blueByte = (byte)((blue * 255) / 31);

        return Color.FromArgb(255, redByte, greenByte, blueByte);
    }
}