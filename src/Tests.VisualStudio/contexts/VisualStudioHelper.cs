using System;
using System.Globalization;
using Microsoft.Win32;

namespace Tests.VisualStudio.contexts
{
    /// <summary>
    /// Gets the installation path of a given VS version.
    /// 
    /// Supported versions 2005, 2008 and 2010, .Net 2003 and .Net 2002
    /// </summary>
    internal static class VisualStudioHelper
    {
        /// <summary>
        /// Gets the installation directory of a given Visual Studio Version
        /// </summary>
        /// <param name="version">Visual Studio Version</param>
        /// <returns>Null if not installed the installation directory otherwise</returns>
        internal static string GetVisualStudioInstallationDir(string version)
        {
            string registryKeyString = String.Format(@"SOFTWARE{0}Microsoft\VisualStudio\{1}",
                                                     Environment.Is64BitProcess ? @"\Wow6432Node\" : "",
                                                     version);

            using (RegistryKey localMachineKey = Registry.LocalMachine.OpenSubKey(registryKeyString))
            {
                return localMachineKey.GetValue("InstallDir") as string;
            }
        }

        /// <summary>
        /// Gets the version number as used by Visual Studio to identify it's version internally.
        ///
        /// eg: Visual Studio 2010 is 10.0 and Visual Studio 2008 is 9.0
        /// </summary>
        /// <param name="version">Visual Studio Version</param>
        /// <returns>A string with the VS internal number version</returns>
        private static string GetVersionNumber(VisualStudioVersion version)
        {
            if (version == VisualStudioVersion.Other) throw new Exception("Not supported version");

            return ((int)version / 10).ToString("{0:0.0}", CultureInfo.InvariantCulture);
        }
    }
}