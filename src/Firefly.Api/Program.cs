using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Firefly.Application.Common.Interfaces;
using Firefly.Application.Dashboard.Services;
using Firefly.Domain.Entities;
using Firefly.Infrastructure.Persistence;
using Firefly.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// AWS SECRETS MANAGER INTEGRATION
if (builder.Environment.IsProduction())
{
    using var client = new AmazonSecretsManagerClient(RegionEndpoint.APSoutheast1);
    var request = new GetSecretValueRequest { SecretId = "Prod/AppSecrets" };
    var response = client.GetSecretValueAsync(request).GetAwaiter().GetResult();

    if (!string.IsNullOrEmpty(response.SecretString))
    {
        var trimmedSecret = response.SecretString.Trim();
        if (trimmedSecret.StartsWith("{"))
        {
            using var doc = JsonDocument.Parse(trimmedSecret);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var key = prop.Name.Replace("__", ":");
                var value = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.GetRawText();

                builder.Configuration[key] = value;
            }
        }
        else
        {
            // Fallback for plain connection string format
            builder.Configuration["ConnectionStrings:DefaultConnection"] = trimmedSecret;
        }
    }
}

// 1. DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Set QuestPDF License
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// 3. Register Application Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// 4. Configure CORS for React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://dcq3l24ktbbtk.cloudfront.net",
                "https://fireflycraftsph.com",
                "https://www.fireflycraftsph.com",
                "https://portal.fireflycraftsph.com"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required if passing cookie/bearer auth context
    });
});

// 5. JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key configuration is missing.");

// CRITICAL FIX: Clear default inbound claim mapping so "role" key is preserved as-is
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),

        // Force ASP.NET to read the exact claim key generated in your payload
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        NameClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/name"
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6. Configure Swagger UI to accept JWT Bearer Tokens
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Firefly Business Portal API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

var app = builder.Build();

// 7. Seed Database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await DbInitializer.SeedAsync(userManager, roleManager);
}

app.UseStaticFiles();

// 8. Configure HTTP Request Pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Firefly Business Portal API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok("Healthy"));
app.MapGet("/", () => Results.Ok("Healthy"));

app.Run();