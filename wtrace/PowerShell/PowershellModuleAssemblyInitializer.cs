using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LowLevelDesign.WinTrace.PowerShell
{
    public class PowerShellModuleAssemblyInitializer : IModuleAssemblyInitializer
    {
        public void OnImport()
        {
            Program.Unpack();
        }
    }
}
