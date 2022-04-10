using System;
using System.Collections.Generic;
using System.Text;

namespace EnchantedVariantsGenerater
{
    public class Config
    {
        public bool CheckExistingGenerated { get; set; } = true;
    }

    public class InputWeaponsJSON
    {
        public string[]? WeaponFormKeys { get; set; }
        public string[]? WeaponEditorIDs { get; set; }
        public EnchantmentJSON[]? Enchantments { get; set; }
        public string LeveledListPrefix { get; set; } = "";
        public string LeveledListSuffix { get; set; } = "";
    }

    public class InputArmorsJSON
    {
        public string[]? ArmorFormKeys { get; set; }
        public string[]? ArmorEditorIDs { get; set; }
        public EnchantmentJSON[]? Enchantments { get; set; }
        public string LeveledListPrefix { get; set; } = "";
        public string LeveledListSuffix { get; set; } = "";
    }
    
    public class EnchantmentJSON
    {
        public string? FormKey { get; set; }
        public string? EditorID { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public int? EnchantmentAmount { get; set; }
        public short Level { get; set; } = 1;
    }
}
