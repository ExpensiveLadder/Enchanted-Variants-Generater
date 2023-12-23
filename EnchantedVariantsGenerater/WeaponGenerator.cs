using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;

namespace EnchantedVariantsGenerater
{
    public class WeaponGenerator
    {
        public static readonly Weapon.TranslationMask weapontranslationmask = new(defaultOn: true)
        {
            BasicStats = new WeaponBasicStats.TranslationMask(true)
            {
                Damage = false,
                Weight = false
            },
            ScopeModel = false,
            ObjectBounds = false,
            Model = false,
            PutDownSound = false,
            PickUpSound = false,
            AlternateBlockMaterial = false,
            UnequipSound = false,
            AttackFailSound = false,
            AttackLoopSound = false,
            AttackSound = false,
            AttackSound2D = false,
            BlockBashImpact = false,
            Critical = false,
            Data = false,
            Description = false,
            Destructible = false,
            DetectionSoundLevel = false,
            EquipmentType = false,
            EquipSound = false,
            FirstPersonModel = false,
            Icons = false,
            IdleSound = false,
            Keywords = false,
            ImpactDataSet = false
        };

        public static void GenerateWeapons(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Dictionary<string, GroupInfo> groups)
        {
            foreach (var group in groups)
            {
                foreach (var weaponInfo in group.Value.Weapons)
                {
                    if (weaponInfo.Value.Item.TryResolve(state.LinkCache, out var weaponGetter))
                    {
                        Program.DoVerboseLog("Reading Weapon: " + weaponInfo.Key);

                        uint itemvalue;
                        if (weaponInfo.Value.Value != null && weaponInfo.Value.Value >= 0)
                        {
                            itemvalue = (uint)weaponInfo.Value.Value;
                        }
                        else
                        {
                            if (weaponGetter.BasicStats == null)
                            {
                                itemvalue = 0;
                            }
                            else
                            {
                                itemvalue = weaponGetter.BasicStats.Value;
                            }
                        }

                        VirtualMachineAdapter? scripts = null;
                        if (weaponGetter.VirtualMachineAdapter != null)
                        {
                            scripts = weaponGetter.VirtualMachineAdapter.DeepCopy();
                        }

                        foreach (var leveledlistInfo in group.Value.LeveledLists.Values)
                        {
                            string leveledlisteditorid = leveledlistInfo.LeveledListPrefix + weaponInfo.Key + leveledlistInfo.LeveledListSuffix;
                            Program.DoVerboseLog("Reading Leveled List: " + leveledlisteditorid);

                            ExtendedList<LeveledItemEntry>? oldleveledlist = null;
                            LeveledItem leveledlist;
                            if (state.LinkCache.TryResolveIdentifier<ILeveledItemGetter>(leveledlisteditorid, out var leveledlistGetter))
                            {
                                leveledlist = state.LinkCache.Resolve<ILeveledItemGetter>(leveledlistGetter).DeepCopy();
                                oldleveledlist = leveledlist.Entries;
                                leveledlist.EditorID = leveledlisteditorid;
                                leveledlist.Entries = new();
                            }
                            else
                            {
                                Console.WriteLine("Creating leveled list: " + leveledlisteditorid);
                                leveledlist = new LeveledItem(state.PatchMod, leveledlisteditorid)
                                {
                                    Entries = new()
                                };
                            }

                            foreach (var enchantmentInfo in leveledlistInfo.Enchantments)
                            {
                                string enchanteditemeditorid = "Ench_" + weaponInfo.Key + "_" + enchantmentInfo.Key;
                                Program.DoVerboseLog("Reading Enchanted Weapon: " + enchanteditemeditorid);

                                string enchanteditemname = enchantmentInfo.Value.Prefix + weaponGetter.Name + enchantmentInfo.Value.Suffix;
                                ushort? enchantmentamount = enchantmentInfo.Value.EnchantmentAmount;

                                Weapon enchanteditem;
                                if (state.LinkCache.TryResolveIdentifier<IWeaponGetter>(enchanteditemeditorid, out var enchantedWeaponGetter))
                                {
                                    Program.DoVerboseLog("Reading Enchanted Weapon Override: " + enchanteditemeditorid);
                                    bool copyitem = false;
                                    enchanteditem = state.LinkCache.Resolve<IWeaponGetter>(enchantedWeaponGetter).DeepCopy(weapontranslationmask);

                                    if (enchanteditem.EnchantmentAmount != enchantmentamount)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + " EnchantmentAmount does not match " + weaponInfo.Key + " Overriding!");
                                        enchanteditem.EnchantmentAmount = enchantmentamount;
                                        copyitem = true;
                                    }
                                    if (enchanteditem.Name != enchanteditemname)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + " Name does not match " + weaponInfo.Key + " Overriding!");
                                        enchanteditem.Name = enchanteditemname;
                                        copyitem = true;
                                    }
                                    enchanteditem.BasicStats ??= new();
                                    if (enchanteditem.BasicStats.Value != itemvalue)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + " Value does not match " + weaponInfo.Key + " Overriding!");
                                        enchanteditem.BasicStats.Value = itemvalue;
                                        copyitem = true;
                                    }
                                    if (scripts == null)
                                    {
                                        if (enchanteditem.VirtualMachineAdapter != null)
                                        {
                                            Console.WriteLine(enchanteditemeditorid + " Scripts does not match " + weaponInfo.Key + " Overriding!");
                                            enchanteditem.VirtualMachineAdapter = null;
                                            copyitem = true;
                                        }
                                    }
                                    else
                                    {
                                        if (enchanteditem.VirtualMachineAdapter == null)
                                        {
                                            if (scripts != null) {
                                                Console.WriteLine(enchanteditemeditorid + " Scripts does not match " + weaponInfo.Key + " Overriding!");
                                                enchanteditem.VirtualMachineAdapter = scripts;
                                                copyitem = true;
                                            }
                                        }
                                        else
                                        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                            if (!scripts.GetEqualsMask(enchanteditem.VirtualMachineAdapter).Scripts.Overall)
                                            {
                                                Console.WriteLine(enchanteditemeditorid + " Scripts does not match " + weaponInfo.Key + " Overriding!");
                                                enchanteditem.VirtualMachineAdapter = scripts;
                                                copyitem = true;
                                            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                        }
                                    }
                                    if (enchanteditem.ObjectEffect.FormKey != enchantmentInfo.Value.Enchantment.FormKey)
                                    {
                                        Console.WriteLine(enchanteditemeditorid + " Enchantment does not match " + weaponInfo.Key + " Overriding!");
                                        enchanteditem.ObjectEffect = enchantmentInfo.Value.Enchantment;
                                        copyitem = true;
                                    }

                                    if (copyitem)
                                    {
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
                                        BasicStats = new WeaponBasicStats()
                                        {
                                            Value = itemvalue
                                        },
                                        ObjectEffect = enchantmentInfo.Value.Enchantment,
                                        EnchantmentAmount = enchantmentamount,
                                        Template = weaponInfo.Value.Item.AsNullable()
                                    };
                                    state.PatchMod.Weapons.Set(enchanteditem);
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
                                if (!LeveledListComparer.AreLeveledListsEqual(leveledlist.Entries, oldleveledlist))
                                {
                                    Console.WriteLine("Setting Leveled List: " + leveledlisteditorid);
                                    state.PatchMod.LeveledItems.Set(leveledlist);
                                }
                            }
                            else
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