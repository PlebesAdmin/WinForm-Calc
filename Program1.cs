// Program.cs
using System;
using System.Windows.Forms;

namespace Calc_App
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            /*
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            */
        }
    }
}
