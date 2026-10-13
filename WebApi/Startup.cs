using System;
using System.Buffers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WebApi.Domain; // Подключаем IUserRepository, InMemoryUserRepository, UserEntity
using WebApi.Models; // Подключаем DTO-классы

namespace WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // --- ЗАДАНИЕ 1: Регистрация InMemory-репозитория ---
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();

            services.AddControllers(options =>
                {
                    // --- ЗАДАНИЕ 1: Настройка форматов вывода (Content Negotiation) ---
                    // Добавляем поддержку XML
                    options.OutputFormatters.Add(new XmlSerializerOutputFormatter());
                    
                    // Настраиваем JSON (Newtonsoft.Json с camelCase)
                    options.OutputFormatters.Insert(0, new NewtonsoftJsonOutputFormatter(new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }, ArrayPool<char>.Shared, options));
                    
                    // Возвращаем 406 Not Acceptable при запросе неизвестного формата
                    options.ReturnHttpNotAcceptable = true;
                    // Если Accept */*, то по умолчанию используем JSON
                    options.RespectBrowserAcceptHeader = true;
                })
                .ConfigureApiBehaviorOptions(options => {
                    options.SuppressModelStateInvalidFilter = true;
                    options.SuppressMapClientErrors = true;
                })
                .AddNewtonsoftJson(options =>
                {
                    // --- ЗАДАНИЕ 2: Включение подстановки DefaultValue (для "John" и "Doe") ---
                    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    options.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Populate;
                });

            // --- ЗАДАНИЯ 1-4: Настройка AutoMapper ---
            services.AddAutoMapper(cfg =>
            {
                // Задание 1: UserEntity -> UserDto (со склеиванием имени и фамилии)
                cfg.CreateMap<UserEntity, UserDto>()
                    .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.LastName} {src.FirstName}"));
                
                // Задание 2: UserToCreateDto -> UserEntity
                cfg.CreateMap<UserToCreateDto, UserEntity>();
                
                // Задание 3: UserToUpdateDto -> UserEntity
                cfg.CreateMap<UserToUpdateDto, UserEntity>();
                
                // Задание 4: Обратный маппинг для PATCH (UserEntity -> UserToUpdateDto)
                cfg.CreateMap<UserEntity, UserToUpdateDto>();
                
            }, Array.Empty<System.Reflection.Assembly>());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}