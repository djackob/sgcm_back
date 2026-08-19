using anin.dataAccess;
using anin.util;

namespace anin.scm
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    // Overlay privado del equipo, ignorado por Git. Debe ser
                    // el mismo que consulta UT_Configuracion para que Startup,
                    // la subida y la descarga compartan una sola ruta.
                    configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
