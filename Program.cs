using System;
using Avalonia;
using Serilog;

namespace AvaloniaMvvmDraw
{
    internal sealed class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                // .WriteTo.File("logs/log.txt") // Décommentez pour activer la sortie fichier
                .CreateLogger();

            try
            {
                Log.Information("Application starting up");
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
