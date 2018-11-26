using System;

namespace GitTfs
{
    //The goal of this project is only to be able to load GitTfs.Vs20xx assemblies that are .net fwk assemblies
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = ProgramHelper.MainCore(args);
            }
            catch (Exception e)
            {
                ProgramHelper.ReportException(e);
                Environment.ExitCode = GitTfsExitCodes.ExceptionThrown;
            }
        }
    }
}
