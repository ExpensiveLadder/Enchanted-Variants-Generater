using System;
using System.Collections.Generic;
using System.Text;

namespace EnchantedVariantsGenerater
{
    public class Config
    {
        public bool LItemEnchWeaponAll { get; set; }
    }
    public class InputFileJSON
    {
        public InputWeaponsJSON? InputWeapons { get; set; }
        public InputArmorsJSON? InputArmors { get; set; }
    }

    public class InputWeaponsJSON
    {
        public string[]? WeaponFormKeys { get; set; }
        public string[]? WeaponEditorIDs { get; set; }
        public EnchantmentJSON[]? Enchantments { get; set; }

        public bool LItemEnchWeaponAll { get; set; } = false;
        public bool LItemEnchWeaponAny { get; set; } = false;
        public bool LItemEnchWeaponBest { get; set; } = false;
    }

    public class InputArmorsJSON
    {
        public string[]? ArmorFormKeys { get; set; }
        public string[]? ArmorEditorIDs { get; set; }
        public EnchantmentJSON[]? Enchantments { get; set; }

        public bool Create_LItemEnchAll { get; set; }
    }
    
    public class EnchantmentJSON
    {
        public string? FormKey { get; set; }
        public string? EditorID { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? Sublist { get; set; }
        public int? Level { get; set; }
        public int? EnchantmentAmount { get; set; }
        public bool Boss { get; set; } = true;
    }
}
