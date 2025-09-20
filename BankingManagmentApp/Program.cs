using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using BankingManagmentApp.Services.Approval;
using BankingManagmentApp.Services.Forecasting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// DB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services.AddDefaultIdentity<Customers>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.User.RequireUniqueEmail = true ;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// MVC
builder.Services.AddControllersWithViews();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// DI: credit scoring & loans
builder.Services.AddScoped<ICreditScoringService, MlCreditScoringService>();
builder.Services.AddScoped<LoansService>();

// Approval/Workflow DI
builder.Services.AddSingleton<LoanApprovalPolicy>();
builder.Services.AddScoped<ILoanApprovalEngine, LoanApprovalEngine>();
builder.Services.AddScoped<ILoanWorkflow, LoanWorkflow>();

// Ако тези класове съществуват в проекта – остави редовете.
// Ако получиш build грешка за липсващи типове, просто ги изтрий.
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<LoanContractGenerator>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});


// OpenAI Chat Client – поддържа и двата начина за конфиг (OpenAIKey или OpenAI:ApiKey)
builder.Services.AddChatClient(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var apiKey =
        config["OpenAIKey"] ??
        config["OpenAI:ApiKey"] ??
        Environment.GetEnvironmentVariable("OpenAI__ApiKey")
        ?? throw new InvalidOperationException("Missing OpenAI API key in OpenAIKey or OpenAI:ApiKey (or env OpenAI__ApiKey).");

    var model =
        config["ModelName"] ??
        config["OpenAI:Model"] ??
        "gpt-4o-mini";

    var openAiClient = new OpenAIClient(apiKey);
    var chatClient = openAiClient.GetChatClient(model).AsIChatClient();
    return chatClient;
});

// Chatbot services
builder.Services.AddScoped<AiChatService>();
builder.Services.AddScoped<KnowledgeBaseService>(); // TemplateAnswer-базиран retrieval
builder.Services.AddScoped<ChatTools>();

// Forecasting service
builder.Services.AddScoped<ForecastService>();

// Background auto-retrain
builder.Services.AddHostedService<MlRetrainHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// seed/migrations
await app.PrepareDataBase();

// middleware
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

