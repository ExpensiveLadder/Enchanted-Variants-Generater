using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;

namespace EnchantedVariantsGenerater
{
    public class ArmorGenerator
    {
        public static readonly Armor.TranslationMask armortranslationmask = new(defaultOn: true)
        {
            AlternateBlockMaterial = false,
            Armature = false,
            Weight = false,
            Race = false,
            PutDownSound = false,
            PickUpSound = false,
            ArmorRating = false,
            BashImpactDataSet = false,
            BodyTemplate = false,
            Description = false,
            Destructible = false,
            EquipmentType = false,
            Keywords = false,
            ObjectBounds = false,
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
                        Program.DoVerboseLog("Reading Armor: " + armorInfo.Key);

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
                            Program.DoVerboseLog("Reading Leveled List: " + leveledlisteditorid);

                            ExtendedList<LeveledItemEntry>? oldleveledlist = null;
                            LeveledItem leveledlist;
                            if (state.LinkCache.TryResolveIdentifier<ILeveledItemGetter>(leveledlisteditorid, out var leveledlistGetter))
                            {
                                leveledlist = state.LinkCache.Resolve<ILeveledItemGetter>(leveledlistGetter).DeepCopy();
                                oldleveledlist = leveledlist.Entries;
                                leveledlist.EditorID = leveledlisteditorid;
                                leveledlist.Entries = new();
                            } else {
                                Console.WriteLine("Creating leveled list: " + leveledlisteditorid);
                                leveledlist = new LeveledItem(state.PatchMod, leveledlisteditorid) {
                                    Entries = new()
                                };
                            }

                            foreach (var enchantmentInfo in leveledlistInfo.Enchantments) {
                                string enchanteditemeditorid = "Ench_" + armorInfo.Key + "_" + enchantmentInfo.Key;
                                Program.DoVerboseLog("Reading Enchanted Armor: " + enchanteditemeditorid);

                                string enchanteditemname = enchantmentInfo.Value.Prefix + armorGetter.Name + enchantmentInfo.Value.Suffix;
                                //ushort? enchantmentamount = enchantmentInfo.Value.EnchantmentAmount;

                                Armor enchanteditem;
                                if (state.LinkCache.TryResolveIdentifier<IArmorGetter>(enchanteditemeditorid, out var enchantedArmorGetter))
                                {
                                    Program.DoVerboseLog("Reading Enchanted Armor Override: " + enchanteditemeditorid);
                                    bool copyitem = false;
                                    enchanteditem = state.LinkCache.Resolve<IArmorGetter>(enchantedArmorGetter).DeepCopy(armortranslationmask);

                                    /*
                                    if (enchanteditem.EnchantmentAmount != enchantmentamount)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + ": EnchantmentAmount does not match: " + armorInfo.Key + " Overriding!");
                                        enchanteditem.EnchantmentAmount = enchantmentamount;
                                        copyitem = true;
                                    }
                                    */
                                    if (enchanteditem.Name != enchanteditemname)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + ": Name does not match: " + armorInfo.Key + " Overriding!");
                                        enchanteditem.Name = enchanteditemname;
                                        copyitem = true;
                                    }
                                    if (enchanteditem.Value != itemvalue)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + " Value does not match " + armorInfo.Key + " Overriding!");
                                        enchanteditem.Value = itemvalue;
                                        copyitem = true;
                                    }
                                    if (scripts == null) {
                                        if (enchanteditem.VirtualMachineAdapter != null)
                                        {
                                            Console.WriteLine(enchanteditemeditorid + " Scrips does not match " + armorInfo.Key + " Overriding!");
                                            enchanteditem.VirtualMachineAdapter = null;
                                            copyitem = true;
                                        }
                                    } else {
                                        if (enchanteditem.VirtualMachineAdapter == null) {
                                            if (scripts != null) {

                                                Console.WriteLine(enchanteditemeditorid + " Scripts does not match " + armorInfo.Key + " Overriding!");
                                                enchanteditem.VirtualMachineAdapter = scripts;
                                                copyitem = true;
                                            }
                                        } else {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                            if (!scripts.GetEqualsMask(enchanteditem.VirtualMachineAdapter).Scripts.Overall)
                                            {
                                                Console.WriteLine(enchanteditemeditorid + " Scripts does not match " + armorInfo.Key + " Overriding!");
                                                enchanteditem.VirtualMachineAdapter = scripts;
                                                copyitem = true;
                                            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                        }
                                    }
                                    if (enchanteditem.ObjectEffect.FormKey != enchantmentInfo.Value.Enchantment.FormKey)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + " Enchantment does not match " + armorInfo.Key + " Overriding!");
                                        enchanteditem.ObjectEffect = enchantmentInfo.Value.Enchantment;
                                        copyitem = true;
                                    }

                                    if (copyitem)
                                    {
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
                                        //EnchantmentAmount = enchantmentamount,
                                        TemplateArmor = armorInfo.Value.Item.AsNullable()
                                    };
                                    state.PatchMod.Armors.Set(enchanteditem);
                                }

                                leveledlist.Entries.Add(new LeveledItemEntry()
                                {
                                    Data = new LeveledItemEntryData()
                                    {
                                        Count = 1,
                                        Level = 1,
                                        Reference = enchanteditem.ToLink()
                                    }
                                });
                            }
                            if (oldleveledlist != null)
                            {
                                    /*
                                    Console.WriteLine("Old Entrys: ");
                                    foreach (var entry in oldleveledlist)
                                    {
                                        if (entry.Data == null) throw new Exception();
                                        Console.WriteLine("Entry: " + entry.Data.Reference.ToString());
                                    }
                                    Console.WriteLine("New Entrys: ");
                                    foreach (var entry in leveledlist.Entries)
                                    {
                                        if (entry.Data == null) throw new Exception();
                                        Console.WriteLine("Entry: " + entry.Data.Reference.ToString());
                                    }
                                    */
                                if (!LeveledListComparer.AreLeveledListsEqual(leveledlist.Entries, oldleveledlist))
                                {
                                    Console.WriteLine("Setting Leveled List: " + leveledlisteditorid);
                                    state.PatchMod.LeveledItems.Set(leveledlist);
                                }
                            } else
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