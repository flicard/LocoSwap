using LocoSwap.Properties;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Xml.Linq;

namespace LocoSwap
{
    static class Utilities
    {
        private static XNamespace Namespace = "http://www.kuju.com/TnT/2003/Delta";
        public static string GetTempDir()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "temp");
        }
        public static string GetSerzPath()
        {
            return Path.Combine(Properties.Settings.Default.TsPath, "serz.exe");
        }
        public static void RemoveFile(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        public static bool ChangeTsPath()
        {
            var valid = false;
            while (!valid)
            {
                using (var dialog = new CommonOpenFileDialog())
                {
                    dialog.Title = Language.Resources.select_ts_path;
                    dialog.IsFolderPicker = true;

                    var result = dialog.ShowDialog();
                    if (result != CommonFileDialogResult.Ok)
                    {
                        return false;
                    }
                    var path = dialog.FileName;
                    var tsExe = Path.Combine(path, "RailWorks.exe");
                    if (!File.Exists(tsExe))
                    {
                        MessageBox.Show(Language.Resources.msg_ts_path_invalid, Language.Resources.msg_error, MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }
                    Settings.Default.TsPath = path;
                    Settings.Default.Save();
                    valid = true;
                }
            }
            return true;
        }

        public static void CopyUserLocalisedString(XElement dest, XElement orig)
        {
            if (dest == null || orig == null) return;
            var names = new string[] { "English", "French", "Italian", "German", "Spanish", "Dutch", "Polish", "Russian", "Key" };
            foreach (var name in names)
            {
                XElement destName = dest.Element(name);
                XElement origName = orig.Element(name);
                if (destName != null && origName != null)
                {
                    destName.Value = origName.Value;
                }
            }
            dest.Element("Other").Elements().Remove();
        }

        public static string DetermineDisplayName(XElement localisedString)
        {
            if (localisedString == null) return "";

            var lang = Settings.Default.Language;
            if (lang == "") lang = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            var langConversionTable = new Dictionary<string, string>()
            {
                { "en", "English" },
                { "fr", "French" },
                { "it", "Italian" },
                { "de", "German" },
                { "es", "Spanish" },
                { "nl", "Dutch" },
                { "pl", "Polish" },
                { "ru", "Russian" }
            };
            var convertedLang = "en";
            if (langConversionTable.ContainsKey(lang))
            {
                convertedLang = langConversionTable[lang];
            }
            XElement preferredElement = localisedString.Element(convertedLang);
            if (preferredElement != null)
            {
                if (preferredElement.Value != "") return preferredElement.Value;
            }

            var result = "";
            foreach (XElement localisedName in localisedString.Elements())
            {
                if (localisedName.Name == "Other" || localisedName.Name == "Key") continue;
                if (localisedName.Value != "")
                {
                    result = localisedName.Value;
                    break;
                }
            }
            return result;
        }

        public static Tuple<ulong, ulong> GetUUIDLongs(Guid guid)
        {
            var result = new Tuple<ulong, ulong>(
                BitConverter.ToUInt64(guid.ToByteArray(), 0),
                BitConverter.ToUInt64(guid.ToByteArray(), 8));

            return result;
        }

        public static XElement GenerateCGUID()
        {
            var guid = Guid.NewGuid();
            var ulongs = GetUUIDLongs(guid);
            var cGUID = new XElement("cGUID");
            var UUID = new XElement("UUID");

            var e1 = new XElement("e");
            e1.SetAttributeValue(Namespace + "type", "sUInt64");
            e1.SetValue(ulongs.Item1);

            var e2 = new XElement("e");
            e2.SetAttributeValue(Namespace + "type", "sUInt64");
            e2.SetValue(ulongs.Item2);

            UUID.Add(e1, e2);

            var devString = new XElement("DevString");
            devString.SetAttributeValue(Namespace + "type", "cDeltaString");
            devString.SetValue(guid.ToString());

            cGUID.Add(UUID, devString);
            return cGUID;
        }

        public static XElement GenerateEntityContainerItem()
        {
            XElement newNode = new XElement("e");
            newNode.SetAttributeValue(Namespace + "numElements", "16");
            newNode.SetAttributeValue(Namespace + "elementType", "sFloat32");
            newNode.SetAttributeValue(Namespace + "precision", "string");
            newNode.SetValue("1.0000000 0.0000000 0.0000000 0.0000000 0.0000000 1.0000000 0.0000000 0.0000000 0.0000000 0.0000000 1.0000000 0.0000000 0.0000000 0.0000000 0.0000000 1.0000000");
            return newNode;
        }

        public static XElement GenerateCargoComponentItem(string val, string altEncoding)
        {
            XElement newNode = new XElement("e");
            newNode.SetAttributeValue(Namespace + "type", "sFloat32");
            newNode.SetAttributeValue(Namespace + "alt_encoding", altEncoding);
            newNode.SetAttributeValue(Namespace + "precision", "string");
            newNode.SetValue(val);
            return newNode;
        }

        public static class StaticRandom
        {
            private static int seed;

            private static ThreadLocal<Random> threadLocal = new ThreadLocal<Random>
                (() => new Random(Interlocked.Increment(ref seed)));

            static StaticRandom()
            {
                seed = Environment.TickCount;
            }

            public static Random Instance { get { return threadLocal.Value; } }
        }

        public static void OpenManual()
        {
            string manualFileName = "LocoSwap_manual.pdf";
            if (File.Exists(manualFileName))
            {
                System.Diagnostics.Process.Start(manualFileName);
            }
        }
    }
}
