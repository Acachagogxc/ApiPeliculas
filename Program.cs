using Ac_DevCurso.EndPonits;
using Ac_DevCurso.Entidades;
using Ac_DevCurso.Repository;
using Ac_DevCurso.Services;
using Ac_DevCurso.Swagger;
using Ac_DevCurso.Utilidades;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;



//inicio area de servicios 

var builder = WebApplication.CreateBuilder(args);
var origenesPermitidos = builder.Configuration.GetValue<string>("origenesPermitidos");
builder.Services.AddCors(opciones =>
{
    opciones.AddDefaultPolicy(configuracion =>
    {
        configuracion.WithOrigins(origenesPermitidos!).AllowAnyHeader().AllowAnyMethod();
    });

    opciones.AddPolicy("libre", configuracion =>
    {
        configuracion.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });

});
//builder.Services.AddOutputCache();
builder.Services.AddStackExchangeRedisOutputCache(opciones =>
{
    opciones.Configuration = builder.Configuration.GetConnectionString("Redis");

});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c=>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Peliculas API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name="Authorization",
        Type=SecuritySchemeType.ApiKey,
        Scheme= "Bearer",
        BearerFormat="JWT",
        In=ParameterLocation.Header
    });
    c.OperationFilter<FiltroAutorizacion>();
    //c.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference=new OpenApiReference
    //            {
    //                Type=ReferenceType.SecurityScheme,
    //                Id="Bearer"
    //            }
    //        },new string[] { }
    //    }

    //});
});
builder.Services.AddScoped<IGeneroRepository, GeneroRepository>();
builder.Services.AddScoped<IActorRepository, ActorRepository>();
builder.Services.AddScoped<IPeliculasRepository, PeliculasRepository>();
builder.Services.AddScoped<IAlmacenadorArchivos,AlmacenadorArchivosLocal>();
builder.Services.AddScoped<IComentariosRepository, ComentariosRepository>();
builder.Services.AddScoped<IErroresRepository, ErroresRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IServicioUsuarios, ServicioUsuarios>();  
builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication().AddJwtBearer(opciones =>
{
    opciones.MapInboundClaims=false;
    opciones.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        //IssuerSigningKey = Llaves.ObtenerLlave(builder.Configuration).First(),
        IssuerSigningKeys = Llaves.ObtenerTodasLlaves(builder.Configuration),
        ClockSkew=TimeSpan.Zero
    };
});
builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy("esadmin", politica => politica.RequireClaim("esadmin"));
});
builder.Services.AddTransient<IUserStore<IdentityUser>,UsuarioStore>();
builder.Services.AddIdentityCore<IdentityUser>();
builder.Services.AddTransient<SignInManager<IdentityUser>>();
var app = builder.Build();
//inicio de middleaware
app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler(exceptionHanderApp => exceptionHanderApp.Run(async context => {
var exceptionHanderFeature=context.Features.Get<IExceptionHandlerFeature>();
    var exception=exceptionHanderFeature?.Error!;
    var error = new ErroresEntity();
    error.Fecha = DateTime.UtcNow;
    error.MensajeDeError=exception.Message;
    error.StackTrace = exception.StackTrace;
    var repositorio = context.RequestServices.GetRequiredService<IErroresRepository>();
    await repositorio.Crear(error);

    await TypedResults.BadRequest(
        new { tipo = "error", mensaje = "Ha ocurrido un mensaje de error inesperado", estatus = 500 }).ExecuteAsync(context);
}));

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseCors();
app.UseOutputCache();
app.UseAuthorization();
app.MapGet("/error", () =>
{
    throw new InvalidOperationException("error de ejemplo");
});
app.MapPost("/modelbinding", ([FromHeader] string? nombre) =>
{
    if (nombre is null)
    { nombre = "vacio"; }
    return TypedResults.Ok(nombre);
});
app.MapGroup("/generos").MapGeneros();
app.MapGroup("/actores").MapActores();
app.MapGroup("/peliculas").MapPeliculas();
app.MapGroup("/pelicula/{peliculaId:int}/comentarios").MapComentario();
app.MapGroup("/usuarios").MapUsuarios();
app.Run();
//