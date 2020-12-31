using System;
using Utilities;

namespace LowLevelDesign.WTrace
{
    class Shim
    {
        [STAThread()]
        static int Main(string[] args)
        {
            Unpack();

            return DoMain(args);
        }

        /// <summary>
        /// Unpacks all the support files associated with this program.   
        /// </summary>
        public static bool Unpack()
        {
            return SupportFiles.UnpackResourcesIfNeeded();
        }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int DoMain(string[] args)
        {
            return Program.main(args);
        }
    }
}
