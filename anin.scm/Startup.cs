using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
                    ValidateAudience = false
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
