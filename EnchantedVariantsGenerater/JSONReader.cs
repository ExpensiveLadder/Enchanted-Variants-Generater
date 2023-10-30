using DynamicData;
using Hjson;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Oblivion;
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
        public static Dictionary<string, InputJSON> GetJSONs(DirectoryPath path, List<string> modlist)
        {
            Console.WriteLine("Reading input files");
            Dictionary<string, InputJSON> jsons = new();
            foreach (var filePath in Directory.EnumerateFiles(path, "*.hjson", SearchOption.AllDirectories))
            {
                var parsedfile = JsonConvert.DeserializeObject<InputJSON>(HjsonValue.Load(filePath).ToString());
                if (parsedfile == null)
                {
                    Program.DoError("ERROR: Could not read HJSON file: " + filePath);
                    continue;
                }
                if (modlist.Contains(parsedfile.Master))
                {
                    jsons.Add(filePath, parsedfile);
                }
                else
                {
                    Console.WriteLine("Skipping HJSON with missing master: " + filePath);
                }
            }
            return jsons;
        }

        public static Dictionary<string, EnchantmentInfo> GetEnchantments(Dictionary<string, InputJSON> jsons)
        {
            Dictionary<string, EnchantmentInfo> enchantments = new();
            foreach (var json in jsons)
            {
                if (json.Value.Enchantments == null) continue;
                Console.WriteLine("Reading enchantment JSON: " + json.Key);
                foreach (var enchantment in json.Value.Enchantments)
                {
                    if (enchantment.FormKey == null)
                    {
                        Program.DoError("Error: " + enchantment.EditorID + " has missing FormKey");
                        continue;
                    }
                    if (enchantment.EditorID == null)
                    {
                        Program.DoError("Error: " + enchantment.FormKey + " has missing FormKey");
                        continue;
                    }
                    if (enchantments.TryGetValue(enchantment.EditorID, out var oldenchantment))
                    {
                        if (oldenchantment.Prefix != null || oldenchantment.Suffix != null)
                        {
                            enchantment.Prefix = oldenchantment.Prefix;
                            enchantment.Suffix = oldenchantment.Suffix;
                        }
                        if (oldenchantment.EnchantmentAmount != null && oldenchantment.EnchantmentAmount != enchantment.EnchantmentAmount)
                        {
                            enchantment.EnchantmentAmount = oldenchantment.EnchantmentAmount;
                        }
                    }
                    else
                    {
                        enchantments.Add(enchantment.EditorID, new EnchantmentInfo(enchantment));
                    }
                }
            }
            return enchantments;
        }

        public static Dictionary<string, GroupInfo> GetGroups(Dictionary<string, InputJSON> jsons, Dictionary<string, EnchantmentInfo> enchantments)
        {
            Dictionary<string, GroupInfo> groups = new();
            foreach (var json in jsons)
            {
                if (json.Value.Groups == null) continue;
                Console.WriteLine("Reading group JSON: " + json.Key);
                foreach (var group in json.Value.Groups)
                {
                    if (group.GroupName == null)
                    {
                        Program.DoError("Error: Group has no name");
                        continue;
                    }

                    if (groups.TryGetValue(group.GroupName, out var oldgroup))
                    {
                        if (oldgroup.Weapons.Any() && group.RemoveWeapons != null)
                        {
                            oldgroup.Weapons.Remove(group.RemoveWeapons);
                        }
                        if (group.Weapons != null)
                        {
                            foreach (var weapon in group.Weapons)
                            {
                                if (weapon.EditorID == null)
                                {
                                    Program.DoError("Error: Weapon has no editorid");
                                    continue;
                                }
                                oldgroup.Weapons.Add(weapon.EditorID, new ItemInfo(weapon));
                            }
                        }

                        if (oldgroup.Armors.Any() && group.RemoveArmors != null)
                        {
                            oldgroup.Armors.Remove(group.RemoveArmors);
                        }
                        if (group.Armors != null)
                        {
                            foreach (var armor in group.Armors)
                            {
                                if (armor.EditorID == null)
                                {
                                    Program.DoError("Error: Armor has no editorid");
                                    continue;
                                }
                                oldgroup.Armors.Add(armor.EditorID, new ItemInfo(armor));
                            }
                        }

                        if (group.LeveledLists != null)
                        {
                            foreach (var leveledlist in group.LeveledLists)
                            {
                                if (oldgroup.LeveledLists.TryGetValue(leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix, out var oldleveledlist))
                                {
                                    if (leveledlist.RemoveEnchantments != null)
                                    {
                                        oldleveledlist.Enchantments.Remove(leveledlist.RemoveEnchantments);
                                    }
                                    if (leveledlist.Enchantments != null)
                                    {
                                        foreach (var enchantment in leveledlist.Enchantments) {

                                            if (enchantments.ContainsKey(enchantment)) 
                                            {
                                                Program.DoError("Leveled List: " + leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix + " already contains enchantment: " + enchantment);
                                            } else
                                            {
                                                if (enchantments.TryGetValue(enchantment, out var enchantmentdefinition))
                                                {
                                                    oldleveledlist.Enchantments.Add(enchantment, enchantmentdefinition);
                                                }
                                                else
                                                {
                                                    Program.DoError("Could not find enchantment definition: " + enchantment);
                                                }
                                            }
                                        }
                                    }
                                } else
                                {
                                    oldgroup.LeveledLists.Add(leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix, new LeveledListInfo(leveledlist, enchantments));
                                }
                            }
                        }
                    }
                    else
                    {
                        groups.Add(group.GroupName, new GroupInfo(group, enchantments));
                    }
                }
            }
            return groups;
        }
    }
}