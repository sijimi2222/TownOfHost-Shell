using System.Text.Json;
using ShellDebugController.Shared;
namespace ShellDebugController;
internal static class Program
{
    [STAThread]static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2&&args[0]=="--ui-smoke")
        {
            try
            {
                using var form=new ControllerForm(true){ShowInTaskbar=false,Opacity=0};form.Show();Application.DoEvents();
                form.SmokeTest();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(args[1]);
                form.SimulatorPreview(Path.ChangeExtension(args[1],"sim.png"));form.Close();
                File.WriteAllText(args[1]+".result.json",JsonSerializer.Serialize(new{passed=true,checks=new[]{"snapshot Fungle selects eleven", "snapshot normal selects ten", "Fungle map sets eleven", "other maps set ten", "toggle ordering","master OFF clears children","disabled child controls","request binding","measured tab layout","simulation tab rendering","no game or IPC start"},gameStarted=false,ipcStarted=false}));
            }
            catch(Exception e){File.WriteAllText(args[1]+".error.txt",e.ToString());Environment.ExitCode=1;}
            return;
        }
        using var window=new ControllerForm(false);Application.Run(window);
    }
}
