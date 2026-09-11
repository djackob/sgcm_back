using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using anin.scm.Services;

namespace anin.scm
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddOptions<IntegracionSigaOptions>()
                .Bind(Configuration.GetSection(IntegracionSigaOptions.Seccion))
                .Validate(opciones => !opciones.Habilitado
                                      || opciones.Modo.Equals("simulacion", StringComparison.OrdinalIgnoreCase)
                                      || opciones.Modo.Equals("real", StringComparison.OrdinalIgnoreCase),
                          "IntegracionSiga:Modo debe ser 'simulacion' o 'real'.")
                .Validate(opciones => opciones.IntervaloSegundos >= 5,
                          "IntegracionSiga:IntervaloSegundos debe ser al menos 5.")
                .Validate(opciones => opciones.Limite is >= 1 and <= 50,
                          "IntegracionSiga:Limite debe estar entre 1 y 50.")
                .Validate(opciones => opciones.TimeoutSegundos >= 15,
                          "IntegracionSiga:TimeoutSegundos debe ser al menos 15.")
                .ValidateOnStart();

            services.AddHostedService<IntegracionSigaWorker>();

            services.AddOptions<PadronSsoOptions>()
                .Bind(Configuration.GetSection(PadronSsoOptions.Seccion))
                .Validate(opciones => !opciones.Habilitado || opciones.IntervaloSegundos >= 60,
                          "PadronSso:IntervaloSegundos debe ser al menos 60.")
                .ValidateOnStart();

            services.AddHostedService<PadronSsoWorker>();

            //services.AddHttpClient();

            var key = Encoding.ASCII.GetBytes(Settings.Secret);

            //var encryption = Encoding.ASCII.GetBytes(Encryption.Secret); //agregue esto

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    // lo que agregue por el reporte
                    //tokendecryptionkey = new symmetricsecuritykey(encryption),

                    ValidateIssuer = false,
                    ValidateAudience = false,
                    /* Homologacion: la jornada supera la hora del token. Sin
                       este margen, el API responde 401 y el front muestra
                       "La sesion ha caducado" en plena bandeja. */
                    ClockSkew = TimeSpan.FromHours(8)
                };

            });
            services.AddCors();
        }

        //public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        //{
        //    if (env.IsDevelopment())
        //    {
        //        app.UseDeveloperExceptionPage();
        //    }

        //    app.UseAuthentication();
        //    app.UseRouting();
        //    app.UseAuthorization();
        //    app.UseCors(x => x
        //       .AllowAnyMethod()
        //       .AllowAnyHeader()
        //       .SetIsOriginAllowed(origin => true)
        //       .AllowCredentials());

        //    app.UseEndpoints(endpoints =>
        //    {
        //        endpoints.MapControllerRoute(
        //         name: "default",
        //         pattern: "api/{controller}/{action}");
        //    });
        //}


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // ----- [AMBIENTE] file server -----------------------------------
            // Publica la carpeta de archivos bajo /files para que el navegador
            // pueda abrir los PDF que genera el sistema. La ruta es la misma
            // que appSettings:rutafile, y /files la misma que appSettings:urlfile.
            //
            // Solo aplica cuando rutafile es una carpeta de ESTA maquina. Si
            // apunta a un recurso compartido —el de vasg, por ejemplo— quien
            // publica es IIS, y montarlo tambien aqui serviria los archivos por
            // una via que no esta bajo las reglas de ese servidor.
            //
            // Nada de esto puede impedir que el servicio arranque: si la carpeta
            // no existe o la red no responde, el sistema sigue en pie y lo unico
            // que falla es subir archivos, con su propio mensaje.
            var rutaFile = (Configuration["appSettings:rutafile"] ?? @"C:\jack\prueba\hub\files\")
                .TrimEnd('\\', '/');

            if (!rutaFile.StartsWith(@"\\"))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(rutaFile);

                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(rutaFile),
                        RequestPath = "/files"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"No se pudo publicar el file server local en '{rutaFile}': {ex.Message}");
                }
            }

            app.UseRouting();

            app.UseCors(x => x
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(origin => true)
                .AllowCredentials());

            app.UseAuthentication();   
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                 name: "default",
                 pattern: "api/{controller}/{action}");
            });
        }




    }
}
