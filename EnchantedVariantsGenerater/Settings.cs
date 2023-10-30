using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnchantedVariantsGenerater
{
    public class Settings
    {
        public bool CheckExistingGenerated { get; set; } = true;
        public bool VerboseLogging { get; set; } = false;
        public bool IgnoreErrors { get; set; } = false;
    }
}
