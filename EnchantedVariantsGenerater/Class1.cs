using System;
using System.Collections.Generic;
using System.Text;

namespace EnchantedVariantsGenerater
{


    public class EnchantmentsJSON
    {
        public EnchantmentJSON[]? Enchantments { get; set; }
    }

    public class WeaponsJSON
    {
        public string[]? Weapons { get; set; }
    }

    public class EnchantmentJSON
    {
        public string? FormID { get; set; }
        public string? EditorID { get; set; }
        public string? Suffix { get; set; }
        public int? EnchantmentAmount { get; set; }
        public string? Sublist { get; set; }
        public int? Level { get; set; }
    }
}
