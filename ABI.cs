using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;

namespace c2_replacer
{
    class ABI
    {
        public int num_texture;
        public TextureInfo[] textureinfo;
        public Bitmap[] bmps;
        public int ident;
        public int version;
        public int num_mesh;
        public int num_animation;
        public byte[] RemainingData; // Enthält die unveränderten Chunks der Datei

        public ABI(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                // Header lesen
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != "LDMB") throw new InvalidDataException("Ungültiges Datei-Format");

                version = br.ReadInt32();
                num_mesh = br.ReadInt32();
                num_animation = br.ReadInt32();
                num_texture = br.ReadInt32();

                // Texture-Chunk einlesen
                textureinfo = new TextureInfo[num_texture];
                for (int i = 0; i < num_texture; i++)
                {
                    textureinfo[i] = new TextureInfo
                    {
                        UNKNOWN = br.ReadInt32(),
                        width = br.ReadInt32(),
                        height = br.ReadInt32(),
                        name = ConvertBytesToString(br.ReadBytes(32)),
                        palette = new Color[256]
                    };

                    for (int j = 0; j < 256; j++)
                    {
                        byte r = br.ReadByte();
                        byte g = br.ReadByte();
                        byte b = br.ReadByte();
                        textureinfo[i].palette[j] = Color.FromArgb(r, g, b);
                    }

                    textureinfo[i].data = br.ReadBytes(textureinfo[i].width * textureinfo[i].height);
                }

                // Den Rest der Datei in einen Puffer laden
                long remainingBytes = fs.Length - fs.Position;
                byte[] remainingData = br.ReadBytes((int)remainingBytes);

                // Speichere die unveränderten Daten
                RemainingData = remainingData;
            }
        }

        public void GenerateAllBitmaps()
        {
            bmps = new Bitmap[textureinfo.Length];

            for (int i = 0; i < textureinfo.Length; i++)
            {
                TextureInfo p = textureinfo[i];
                bmps[i] = new Bitmap(p.width, p.height);

                BitmapData bmpdata = bmps[i].LockBits(new Rectangle(0, 0, bmps[i].Width, bmps[i].Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                IntPtr ptr = bmpdata.Scan0;
                int pitch = bmpdata.Stride / 4;

                unsafe
                {
                    int t = 0;
                    int* start = (int*)ptr;
                    for (int y = 0; y < p.height; y++)
                    {
                        for (int x = 0; x < p.width; x++)
                        {
                            int idx = p.data[t++];
                            *(start + x) = (idx == 0xFE) ? Color.Black.ToArgb() : p.palette[idx].ToArgb();
                        }
                        start += pitch;
                    }
                }

                bmps[i].UnlockBits(bmpdata);
            }
        }

        public void ReplaceTexturesFromFolder(string folderPath)
        {
            string[] bmpFiles = Directory.GetFiles(folderPath, "*.bmp");

            foreach (string bmpFile in bmpFiles)
            {
                string bmpFileName = Path.GetFileName(bmpFile).Trim().ToLower();

                for (int i = 0; i < textureinfo.Length; i++)
                {
                    if (textureinfo[i].name.Trim().ToLower() == bmpFileName)
                    {
                        Bitmap bmp = new Bitmap(bmpFile);

                        textureinfo[i].width = bmp.Width;
                        textureinfo[i].height = bmp.Height;

                        Color[] palette = GeneratePaletteFromBitmap(bmp);
                        byte[] textureData = ConvertBitmapToByteArray(bmp, palette);

                        textureinfo[i].data = textureData;
                        textureinfo[i].palette = palette;

                        Console.WriteLine($"Ersetzte Textur: {textureinfo[i].name}, Neue Größe: {textureinfo[i].width}x{textureinfo[i].height}");
                    }
                }
            }

            GenerateAllBitmaps();
        }

        public void SaveABIFile(string outputFilename)
        {
            using (FileStream fs = new FileStream(outputFilename, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // Schreibe den Header
                bw.Write(Encoding.ASCII.GetBytes("LDMB"));
                bw.Write(version);
                bw.Write(num_mesh);
                bw.Write(num_animation);
                bw.Write(num_texture);

                // Schreibe den Texture-Chunk
                foreach (var texture in textureinfo)
                {
                    bw.Write(texture.UNKNOWN);
                    bw.Write(texture.width);
                    bw.Write(texture.height);

                    byte[] nameBytes = Encoding.ASCII.GetBytes(texture.name);
                    Array.Resize(ref nameBytes, 32);
                    bw.Write(nameBytes);

                    foreach (var color in texture.palette)
                    {
                        bw.Write(color.R);
                        bw.Write(color.G);
                        bw.Write(color.B);
                    }

                    bw.Write(texture.data);
                }

                // Schreibe die restlichen unveränderten Daten
                if (RemainingData != null)
                {
                    bw.Write(RemainingData);
                }
            }

            Console.WriteLine("ABI-Datei erfolgreich gespeichert.");
        }

        private byte[] ConvertBitmapToByteArray(Bitmap bmp, Color[] palette)
        {
            List<byte> byteList = new List<byte>();

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color pixelColor = bmp.GetPixel(x, y);
                    byte paletteIndex = 0;
                    for (int i = 0; i < palette.Length; i++)
                    {
                        if (palette[i] == pixelColor)
                        {
                            paletteIndex = (byte)i;
                            break;
                        }
                    }
                    byteList.Add(paletteIndex);
                }
            }

            return byteList.ToArray();
        }

        private Color[] GeneratePaletteFromBitmap(Bitmap bmp)
        {
            HashSet<Color> uniqueColors = new HashSet<Color>();

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    uniqueColors.Add(bmp.GetPixel(x, y));
                }
            }

            Color[] palette = new Color[256];
            int index = 0;
            foreach (Color color in uniqueColors)
            {
                if (index < 256)
                {
                    palette[index++] = color;
                }
                else
                {
                    break;
                }
            }

            return palette;
        }

        private string ConvertBytesToString(byte[] buf)
        {
            int i;
            for (i = 0; i < buf.Length; i++)
                if (buf[i] == 0) break;

            return Encoding.ASCII.GetString(buf, 0, i);
        }
    }

    public class TextureInfo
    {
        public int UNKNOWN;
        public int width;
        public int height;
        public string name;
        public Color[] palette;
        public byte[] data;
    }
}
