using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace DNDLootRoller;

public static class ImportTests
{
    public static void Main(string[] args) => Console.WriteLine(Run(args[0]));
    public static string Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception("FAILED: " + label); checks++; }
        string scratch = Path.Combine(Path.GetTempPath(), "LootTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string original = Path.Combine(root, "Loot Table.xlsx");
            var imported = SpreadsheetImporter.Read(original);
            var embedded = JsonSerializer.Deserialize<LootData>(File.ReadAllText(Path.Combine(root, "loot_tables.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            foreach (var t in embedded.Tables)
            {
                var actual = imported.Tables.Single(x => x.Die == t.Die);
                Check(actual.Entries.Count == t.Entries.Count, "row count");
                for (int roll = 1; roll <= 100; roll++)
                {
                    var a = actual.Entries.FirstOrDefault(e => e.Matches(roll));
                    var b = t.Entries.FirstOrDefault(e => e.Matches(roll));
                    Check(a?.Description == b?.Description && a?.SubDie == b?.SubDie, $"D6 {t.Die}, D100 {roll}");
                    if (b?.SubDie != null)
                        for (int sub = 1; sub <= b.SubDie; sub++)
                            Check(a!.SubEntries.FirstOrDefault(e => e.Matches(sub))?.Description == b.SubEntries.FirstOrDefault(e => e.Matches(sub))?.Description, "subtable result");
                }
            }
            Check(imported.Issues.Count == 2, "original gap and overlap warnings");
            string state = Path.Combine(scratch, "state.json");
            LootStore.Save(new SavedLoot { Name = "test.xlsx", Data = imported }, state);
            Check(LootStore.Read(state)!.Name == "test.xlsx", "saved name");
            Check(LootStore.Read(state)!.Data.Tables[4].Entries[1].SubDie == 8, "saved subtable");
            LootStore.Save(new SavedLoot { Name = "replacement.xlsx", Data = imported }, state);
            Check(LootStore.Read(state)!.Name == "replacement.xlsx", "replace saved import");
            Check(ItemLinks.For("50") == null && ItemLinks.For("50 gold pieces") == null, "currency has no item link");
            Check(ItemLinks.For(null) == null, "missing result has no item link");
            Check(ItemLinks.For("Boots of elvenkind") is { IsSearch: false }, "known item direct link");
            Check(ItemLinks.For("Bronze griffon", parent: "Figurine of wondrous power (roll d8)") is { IsSearch: false }, "subtable item link");
            Check(ItemLinks.For("My custom sword & shield") is { IsSearch: true } fallback && fallback.Url.EndsWith("My%20custom%20sword%20%26%20shield"), "unknown loot encoded search");
            string itemUrl = "https://www.dndbeyond.com/magic-items/4587-boots-of-elvenkind";
            Check(ItemLinks.For("My custom name", itemUrl)?.Url == itemUrl, "explicit item URL wins");
            foreach (var entry in embedded.Tables.SelectMany(t => t.Entries))
            {
                if (entry.SubEntries.Count == 0 && !int.TryParse(entry.Description, out _))
                    Check(ItemLinks.For(entry.Description) is { IsSearch: false }, "original item direct link: " + entry.Description);
                foreach (var sub in entry.SubEntries)
                    Check(ItemLinks.For(sub.Description, parent: entry.Description) is { IsSearch: false }, "original subtable direct link: " + sub.Description);
            }
            foreach (string invalidUrl in new[] { "http://www.dndbeyond.com/magic-items/1-test", "https://www.dndbeyond.com.evil.test/magic-items/1-test", "file:///C:/test.exe", "javascript:alert(1)", "https://user@www.dndbeyond.com/magic-items/1-test", "https://www.dndbeyond.com:1234/magic-items/1-test", "https://www.dndbeyond.com/login" })
                Check(!ItemLinks.IsItemUrl(invalidUrl), "reject non-item URL: " + invalidUrl);

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string Change(string part, Action<XDocument> edit)
            {
                string file = Path.Combine(scratch, Guid.NewGuid().ToString("N") + ".xlsx");
                File.Copy(original, file);
                using var zip = ZipFile.Open(file, ZipArchiveMode.Update);
                var entry = zip.GetEntry(part)!;
                XDocument doc;
                using (var input = entry.Open()) doc = XDocument.Load(input);
                edit(doc); entry.Delete();
                using var output = zip.CreateEntry(part).Open(); doc.Save(output);
                return file;
            }
            void Reject(string file, string label)
            {
                try { SpreadsheetImporter.Read(file); }
                catch (Exception ex) when (ex is InvalidDataException || ex is System.Xml.XmlException) { checks++; return; }
                throw new Exception("FAILED: accepted " + label);
            }
            string Cell(string value) => Change("xl/worksheets/sheet1.xml", doc => doc.Descendants(ns + "c").First().Element(ns + "v")!.Value = value);
            foreach (string value in new[] { "0", "101", "20-1", "abc", "1.5" }) Reject(Cell(value), value);
            Reject(Change("xl/workbook.xml", doc => doc.Descendants(ns + "sheet").Last().Remove()), "five sheets");
            Reject(Change("xl/worksheets/sheet1.xml", doc => doc.Descendants(ns + "sheetData").First().RemoveNodes()), "empty sheet");
            Reject(Change("xl/worksheets/sheet1.xml", doc => doc.Descendants(ns + "c").First().Add(new XElement(ns + "f", "1+1"))), "formula");
            Reject(Change("xl/worksheets/sheet1.xml", doc => doc.Descendants(ns + "c").First(c => (string?)c.Attribute("r") == "B1").Remove()), "missing loot");
            Reject(Change("xl/worksheets/sheet1.xml", doc => { var c = doc.Descendants(ns + "c").First(); c.SetAttributeValue("t", "inlineStr"); c.RemoveNodes(); c.Add(new XElement(ns + "is", new XElement(ns + "t", "-"))); }), "orphan subtable");
            var inline = Change("xl/worksheets/sheet1.xml", doc => { var c = doc.Descendants(ns + "c").First(c => (string?)c.Attribute("r") == "B1"); c.SetAttributeValue("t", "inlineStr"); c.RemoveNodes(); c.Add(new XElement(ns + "is", new XElement(ns + "t", "Custom loot"))); });
            Check(SpreadsheetImporter.Read(inline).Tables[0].Entries[0].Description == "Custom loot", "inline text");
            string WithUrl(string url) => Change("xl/worksheets/sheet1.xml", doc => doc.Descendants(ns + "row").First().Add(new XElement(ns + "c", new XAttribute("r", "C1"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", url)))));
            var linked = SpreadsheetImporter.Read(WithUrl(itemUrl));
            Check(linked.Tables[0].Entries[0].BeyondUrl == itemUrl, "column C import");
            LootStore.Save(new SavedLoot { Data = linked }, state);
            Check(LootStore.Read(state)!.Data.Tables[0].Entries[0].BeyondUrl == itemUrl, "item URL persistence");
            Reject(WithUrl("https://example.com/test"), "external domain in column C");
            File.WriteAllText(state, "{bad json");
            try { LootStore.Read(state); throw new Exception("Accepted corrupt persistence"); } catch (JsonException) { checks++; }
            return $"PASS: {checks} assertions, including all 600 original D6/D100 outcomes, subtables, persistence, and malformed imports.";
        }
        finally { Directory.Delete(scratch, true); }
    }
}
