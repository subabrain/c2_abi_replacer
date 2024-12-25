using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Collections.Generic;

namespace c2_replacer
{
    public class Program
    {

        static void Main(string[] args)
        {
            // Pfad zum Ordner mit den BMP-Dateien
            string textureFolderPath = "PathtoTextureFolder";

            // Erstelle ein ABI-Objekt und lade die .ABI-Datei
            ABI abi = new ABI("PathtoABIFile");

            // Ersetze die Texturen aus den BMP-Dateien im angegebenen Ordner
            abi.ReplaceTexturesFromFolder(textureFolderPath);

            // Speichere die geänderte ABI-Datei
            abi.SaveABIFile("PathtoABIFile");

            Console.WriteLine("Texturen wurden ersetzt und die Datei gespeichert.");
        }
    }

    
}
