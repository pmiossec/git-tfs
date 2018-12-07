using System;

namespace GitTfs
{
    //The goal of this project is only to be able to run Git-tfs on other platforms than windows
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
