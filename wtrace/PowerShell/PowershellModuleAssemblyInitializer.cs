using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LowLevelDesign.WinTrace.PowerShell
{
    public class PowershellModuleAssemblyInitializer : IModuleAssemblyInitializer
    {
        public void OnImport()
        {
            Trace.WriteLine("Module load");
            Program.Unpack();
        }
    }
}
