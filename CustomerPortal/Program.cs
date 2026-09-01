using CustomerPortal.Data;
using CustomerPortal.Exceptions;
using CustomerPortal.Models.Dtos;
using CustomerPortal.Repositories;
using CustomerPortal.Security;
using CustomerPortal.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Single error shape for automatic model-state failures (FluentValidation,
        // malformed JSON, unmapped-member rejection) -- api-conventions.md AC-6.
        options.InvalidModelStateResponseFactory = context =>
        {
            var fieldErrors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .Select(kvp => new FieldError(
                    System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(kvp.Key),
                    kvp.Value!.Errors[0].ErrorMessage))
                .ToList();

            var response = new ErrorResponse(
                Timestamp: DateTimeOffset.UtcNow,
                Status: StatusCodes.Status400BadRequest,
                Error: "Bad Request",
                Message: "Validation failed for one or more fields.",
                Path: context.HttpContext.Request.Path,
                FieldErrors: fieldErrors.Count > 0 ? fieldErrors : null);

            return new BadRequestObjectResult(response);
        };
    });
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// SQLite does not create the parent directory for its file itself.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlite(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
// SC-4 deny-by-default: every endpoint requires authentication unless
// explicitly marked [AllowAnonymous] (implementation_plan v2 Architectural
// Changes item 1 -- this Story adds the project's first endpoint).
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddAntiforgery();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Persistence:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

// api-conventions.md AC-2: any request carrying a body must be application/json,
// else 415. ASP.NET Core's own automatic content-type/formatter-selection
// short-circuit produces a framework-default body for this case (bypassing
// GlobalExceptionHandler entirely) rather than the project's ErrorResponse
// shape, so it is handled explicitly here, ahead of MVC's model binding.
app.Use(async (context, next) =>
{
    var hasBody = context.Request.ContentLength is > 0
        || string.Equals(context.Request.Headers.TransferEncoding, "chunked", StringComparison.OrdinalIgnoreCase);
    var isJson = context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? false;

    if (hasBody && !isJson)
    {
        throw new UnsupportedMediaTypeException(
            $"Content-Type '{context.Request.ContentType}' is not supported.");
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
