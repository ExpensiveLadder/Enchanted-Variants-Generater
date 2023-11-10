using Hjson;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Newtonsoft.Json;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                    jsons.Add(filePath.ToString().Replace(path, ""), parsedfile);
                }
                else
                {
                    Console.WriteLine("Skipping HJSON with missing master: " + filePath);
                }
            }
            jsons = jsons.OrderBy(o => modlist.IndexOf(o.Value.Master)).ToDictionary();
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
                        if (enchantment.FormKey != null && FormKey.Factory(enchantment.FormKey) != oldenchantment.Enchantment.FormKey)
                        {
                            oldenchantment.Enchantment = FormKey.Factory(enchantment.FormKey).ToNullableLink<IEffectRecordGetter>();
                        }
                    }
                    else
                    {
                        if (enchantment.FormKey == null)
                        {
                            Program.DoError("Error: " + enchantment.EditorID + " has missing FormKey");
                            continue;
                        }
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
                        Console.WriteLine("Group: " + group.GroupName + " already exists");

                        if (oldgroup.Weapons.Any() && group.RemoveWeapons != null)
                        {
                            foreach (var weapon in group.RemoveWeapons) {
                                if (oldgroup.Weapons.ContainsKey(weapon)) {
                                    Console.WriteLine("Removing Weapon: " + weapon + " from Group: " + group.GroupName);
                                    oldgroup.Weapons.Remove(weapon);
                                }
                            }
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
                                oldgroup.Weapons.Add(weapon.EditorID, new WeaponInfo(weapon));
                            }
                        }

                        if (oldgroup.Armors.Any() && group.RemoveArmors != null)
                        {
                            foreach (var armor in group.RemoveArmors)
                            {
                                if (oldgroup.Armors.ContainsKey(armor))
                                {
                                    Console.WriteLine("Removing Armor: " + armor + " from Group: " + group.GroupName);
                                    oldgroup.Armors.Remove(armor);
                                }
                            }
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
                                oldgroup.Armors.Add(armor.EditorID, new ArmorInfo(armor));
                            }
                        }

                        if (group.LeveledLists != null)
                        {
                            foreach (var leveledlist in group.LeveledLists)
                            {
                                if (oldgroup.LeveledLists.TryGetValue(leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix, out var oldleveledlist))
                                {
                                    Console.WriteLine("Leveled List: " + leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix + " already exists in group: " + group.GroupName);
                                    if (leveledlist.RemoveEnchantments != null)
                                    {
                                        foreach (var enchantment in leveledlist.RemoveEnchantments)
                                        {
                                            if (oldleveledlist.Enchantments.ContainsKey(enchantment))
                                            {
                                                Console.WriteLine("Removing Enchantment: " + enchantment + "from Leveled List: " + leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix + " in Group: " + group.GroupName);
                                                oldleveledlist.Enchantments.Remove(enchantment);
                                            }
                                        }
                                    }
                                    if (leveledlist.Enchantments != null)
                                    {
                                        foreach (var enchantment in leveledlist.Enchantments) {

                                            if (oldleveledlist.Enchantments.ContainsKey(enchantment)) 
                                            {
                                                Program.DoError("Leveled List: " + leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix + " in group: " + group.GroupName + " already contains enchantment: " + enchantment);
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
                        Console.WriteLine("Creating Group: " + group.GroupName);
                        groups.Add(group.GroupName, new GroupInfo(group, enchantments));
                    }
                }
            }
            return groups;
        }
    }
}