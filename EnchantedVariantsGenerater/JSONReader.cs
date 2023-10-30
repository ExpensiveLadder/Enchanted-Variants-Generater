using DynamicData;
using Hjson;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Newtonsoft.Json;
using Noggog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mutagen.Bethesda.Plugins.Binary.Processing.BinaryFileProcessor;

namespace EnchantedVariantsGenerater
{
    public class JSONReader
    {
        public static List<InputJSON> GetJSONs(DirectoryPath path)
        {
            Console.WriteLine("Reading input files");
            List<InputJSON> things = new();
            foreach (var filePath in Directory.EnumerateFiles(path, "*.hjson", SearchOption.AllDirectories)) {
                Console.WriteLine(filePath);
                var parsedfile = JsonConvert.DeserializeObject<InputJSON>(HjsonValue.Load(filePath).ToString());
                if (parsedfile == null) {
                    Console.WriteLine("ERROR: Could not read HJSON file: " + filePath);
                } else {
                    things.Add(parsedfile);
                }
            }
            return things;
        }

        public static List<InputJSON> SortJSONs(List<InputJSON> unsortedList, List<string> modlist)
        {
            //var orderedList = unsortedList.OrderBy(a => a.Master.IndexOf(modlist));

            Console.WriteLine("Unordered Masters List:");
            foreach (var item in unsortedList) {
                Console.WriteLine(item.Master);
            }

            var orderedList = unsortedList.OrderBy(o => modlist.IndexOf(o.Master)).ToList();

            Console.WriteLine("Ordered Masters List:");
            foreach (var item in orderedList)
            {
                Console.WriteLine(item.Master);
            }

            //var orderedList = modlist.Select((s, p) => new { s, p }).ToList();
            return orderedList;
        }
    }
}
