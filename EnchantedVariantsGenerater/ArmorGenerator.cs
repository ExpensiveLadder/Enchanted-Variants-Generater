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
    public class ArmorGenerator
    {
        public static readonly Armor.TranslationMask armortranslationmask = new(defaultOn: false)
        {
            ObjectEffect = true,
            EnchantmentAmount = true,
            VirtualMachineAdapter = true,
            Name = true,
            Value = true,
            TemplateArmor = true,
            FormVersion = true,
            WorldModel = new GenderedItem<ArmorModel.TranslationMask>(new ArmorModel.TranslationMask(defaultOn: false) { Model = new Model.TranslationMask(false) { } }, new ArmorModel.TranslationMask(defaultOn: false) { Model = new Model.TranslationMask(false) { } })
        };

        public static void GenerateArmors(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Dictionary<string, GroupInfo> groups)
        {
            foreach (var group in groups)
            {
                foreach (var armorInfo in group.Value.Armors)
                {
                    if (armorInfo.Value.Item.TryResolve(state.LinkCache, out var armorGetter))
                    {
                        Console.WriteLine("Reading Armor: " + armorInfo.Key);

                        uint itemvalue;
                        if (armorInfo.Value.Value != null && armorInfo.Value.Value > 0) {
                            itemvalue = (uint)armorInfo.Value.Value;
                        } else
                        {
                            itemvalue = armorGetter.Value;
                        }

                        VirtualMachineAdapter? scripts = null;
                        if (armorGetter.VirtualMachineAdapter != null) {
                            scripts = armorGetter.VirtualMachineAdapter.DeepCopy();
                        }

                        foreach (var leveledlistInfo in group.Value.LeveledLists.Values)
                        {
                            string leveledlisteditorid = leveledlistInfo.LeveledListPrefix + armorInfo.Key + leveledlistInfo.LeveledListSuffix;
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
                                string enchanteditemeditorid = "Ench_" + armorInfo.Key + "_" + enchantmentInfo.Key;
                                Console.WriteLine("Reading Enchanted Armor: " + enchanteditemeditorid);

                                string enchanteditemname = enchantmentInfo.Value.Prefix + armorGetter.Name + enchantmentInfo.Value.Suffix;
                                ushort? enchantmentamount = (ushort?)enchantmentInfo.Value.EnchantmentAmount;

                                Armor enchanteditem;
                                if (state.LinkCache.TryResolve<IArmorGetter>(enchanteditemeditorid, out var enchantedArmorGetter))
                                {
                                    Console.WriteLine("Reading Enchanted Armor Override: " + enchanteditemeditorid);
                                    bool copyitem = false;
                                    enchanteditem = enchantedArmorGetter.ToLink().Resolve(state.LinkCache).DeepCopy(armortranslationmask);

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
                                    if (enchanteditem.Value != itemvalue)
                                    {
                                        enchanteditem.Value = itemvalue;
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
                                        Console.WriteLine("Overriding Enchanted Armor: " + enchanteditemeditorid);
                                        enchanteditem.EditorID = enchanteditemeditorid;
                                        state.PatchMod.Armors.Set(enchanteditem);
                                    }
                                } 
                                else 
                                {
                                    Console.WriteLine("Creating Enchanted Armor: " + enchanteditemeditorid);
                                    enchanteditem = new Armor(state.PatchMod, enchanteditemeditorid)
                                    {   
                                        Name = enchanteditemname,
                                        Value = itemvalue,
                                        ObjectEffect = enchantmentInfo.Value.Enchantment,
                                        EnchantmentAmount = enchantmentamount,
                                        TemplateArmor = armorInfo.Value.Item.AsNullable()
                                    };
                                    state.PatchMod.Armors.Set(enchanteditem);
                                }

                                leveledlist.Entries ??= new();

                                bool duplicate = true;
                                foreach (var entry in leveledlist.Entries) {
                                    if (entry.Data == null) continue;
                                    if (entry.Data.Reference.FormKey == enchanteditem.FormKey)
                                    {
                                        Console.WriteLine("Enchanted Armor: " + enchanteditemeditorid + " already exists in Leveled List: " + leveledlisteditorid);
                                        duplicate = false;
                                        break;
                                    }
                                }
                                if (duplicate)
                                {
                                    Console.WriteLine("Adding Enchanted Armor: " + enchanteditemeditorid + " to Leveled List: " + leveledlisteditorid);
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
                        Program.DoError("Could not find Armor:" + armorInfo.Key);
                    }
                }
            }
        }
    }
}