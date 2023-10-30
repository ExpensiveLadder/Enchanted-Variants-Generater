using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnchantedVariantsGenerater
{
    public class ItemGenerator
    {
        public static void GenerateWeapons(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Dictionary<string, EnchantmentInfo> enchantments, Dictionary<string, GroupInfo> groups) {
            foreach (var group in groups) {
                foreach (var leveledlist in group.Value.LeveledLists) {
                    foreach (var enchantment in leveledlist.Value.Enchantments) {
                        
                    }
                }
            }
        }
    }
}
