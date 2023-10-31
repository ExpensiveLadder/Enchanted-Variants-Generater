using Loqui;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnchantedVariantsGenerater
{
    public class WeaponGenerator
    {
        public static readonly Weapon.TranslationMask weapontranslationmask = new(defaultOn: false)
        {
            ObjectEffect = true,
            EnchantmentAmount = true,
            Name = true,
            BasicStats = new WeaponBasicStats.TranslationMask(false) {
                Value = true
            },
            VirtualMachineAdapter = true,
            Template = true,
            FormVersion = true
        };

        public static void GenerateWeapons(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Dictionary<string, GroupInfo> groups)
        {
            foreach (var group in groups)
            {
                foreach (var weaponInfo in group.Value.Weapons)
                {
                    if (weaponInfo.Value.Item.TryResolve(state.LinkCache, out var weaponGetter))
                    {
                        Console.WriteLine("Reading Weapon: " + weaponInfo.Key);

                        uint itemvalue;
                        if (weaponInfo.Value.Value != null && weaponInfo.Value.Value > 0) {
                            itemvalue = (uint)weaponInfo.Value.Value;
                        } else {
                            if (weaponGetter.BasicStats == null) {
                                itemvalue = 0;
                            } else {
                                itemvalue = weaponGetter.BasicStats.Value;
                            }
                        }

                        VirtualMachineAdapter? scripts = null;
                        if (weaponGetter.VirtualMachineAdapter != null) {
                            scripts = weaponGetter.VirtualMachineAdapter.DeepCopy();
                        }

                        foreach (var leveledlistInfo in group.Value.LeveledLists.Values)
                        {
                            string leveledlisteditorid = leveledlistInfo.LeveledListPrefix + weaponInfo.Key + leveledlistInfo.LeveledListSuffix;
                            Console.WriteLine("Reading Leveled List: " + leveledlisteditorid);

                            LeveledItem leveledlist;
                            if (state.LinkCache.TryResolve<ILeveledItemGetter>(leveledlisteditorid, out var leveledlistGetter))
                            {
                                leveledlist = leveledlistGetter.DeepCopy();
                            } else {
                                Console.WriteLine("Creating leveled list: " + leveledlisteditorid);
                                leveledlist = new LeveledItem(state.PatchMod, leveledlisteditorid);
                            }
                            bool copyleveledlist = false;

                            foreach (var enchantmentInfo in leveledlistInfo.Enchantments) {
                                string enchanteditemeditorid = "Ench_" + weaponInfo.Key + "_" + enchantmentInfo.Key;
                                Console.WriteLine("Reading Enchanted Weapon: " + enchanteditemeditorid);

                                string enchanteditemname = enchantmentInfo.Value.Prefix + weaponGetter.Name + enchantmentInfo.Value.Suffix;
                                ushort? enchantmentamount = (ushort?)enchantmentInfo.Value.EnchantmentAmount;

                                Weapon enchanteditem;
                                if (state.LinkCache.TryResolve<IWeaponGetter>(enchanteditemeditorid, out var enchantedWeaponGetter))
                                {
                                    Console.WriteLine("Reading Enchanted Weapon Override: " + enchanteditemeditorid);
                                    bool copyitem = false;
                                    enchanteditem = enchantedWeaponGetter.ToLink().Resolve(state.LinkCache).DeepCopy(weapontranslationmask);

                                    if (enchanteditem.EnchantmentAmount != enchantmentamount)
                                    {
                                        enchanteditem.EnchantmentAmount = enchantmentamount;
                                        copyitem = true;
                                    }
                                    if (enchanteditem.Name != enchanteditemname)
                                    {
                                        enchanteditem.Name = enchanteditemname;
                                        copyitem = true;
                                    }
                                    enchanteditem.BasicStats ??= new();
                                    if (enchanteditem.BasicStats.Value != itemvalue)
                                    {
                                        enchanteditem.BasicStats.Value = itemvalue;
                                        copyitem = true;
                                    }
                                    if (scripts == null) {
                                        if (enchanteditem.VirtualMachineAdapter != null) {
                                            enchanteditem.VirtualMachineAdapter = null;
                                            copyitem = true;
                                        }
                                    } else {
                                        if (enchanteditem.VirtualMachineAdapter == null) {
                                            enchanteditem.VirtualMachineAdapter = scripts;
                                            copyitem = true;
                                        } else {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                            if (!scripts.GetEqualsMask(enchanteditem.VirtualMachineAdapter).Scripts.Overall)
                                            {
                                                enchanteditem.VirtualMachineAdapter = scripts;
                                                copyitem = true;
                                            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                        }
                                    }

                                    if (copyitem)
                                    {
                                        Console.WriteLine("Overriding Enchanted Weapon: " + enchanteditemeditorid);
                                        enchanteditem.EditorID = enchanteditemeditorid;
                                        state.PatchMod.Weapons.Set(enchanteditem);
                                    }
                                } 
                                else 
                                {
                                    Console.WriteLine("Creating Enchanted Weapon: " + enchanteditemeditorid);
                                    enchanteditem = new Weapon(state.PatchMod, enchanteditemeditorid)
                                    {   
                                        Name = enchanteditemname,
                                        BasicStats = new WeaponBasicStats() {
                                            Value = itemvalue
                                        },
                                        ObjectEffect = enchantmentInfo.Value.Enchantment,
                                        EnchantmentAmount = enchantmentamount,
                                        Template = weaponInfo.Value.Item.AsNullable()
                                    };
                                    state.PatchMod.Weapons.Set(enchanteditem);
                                }

                                leveledlist.Entries ??= new();

                                bool duplicate = true;
                                foreach (var entry in leveledlist.Entries) {
                                    if (entry.Data == null) continue;
                                    if (entry.Data.Reference.FormKey == enchanteditem.FormKey)
                                    {
                                        Console.WriteLine("Enchanted Weapon: " + enchanteditemeditorid + " already exists in Leveled List: " + leveledlisteditorid);
                                        duplicate = false;
                                        break;
                                    }
                                }
                                if (duplicate)
                                {
                                    Console.WriteLine("Adding Enchanted Weapon: " + enchanteditemeditorid + " to Leveled List: " + leveledlisteditorid);
                                    leveledlist.Entries.Add(new LeveledItemEntry()
                                    {
                                        Data = new LeveledItemEntryData()
                                        {
                                            Count = 1,
                                            Level = 1,
                                            Reference = enchanteditem.ToLink()
                                        }
                                    });
                                    copyleveledlist = true;
                                }
                            }
                            if (copyleveledlist)
                            {
                                state.PatchMod.LeveledItems.Set(leveledlist);
                            }
                        }
                    }
                    else
                    {
                        Program.DoError("Could not find weapon:" + weaponInfo.Key);
                    }
                }
            }
        }
    }
}