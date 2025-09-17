using QuestPDF.Infrastructure;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using BankingManagmentApp.Services.Approval;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using Microsoft.Extensions.Configuration;
using BankingManagmentApp.Services.Forecasting;

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
    //options.SignIn.RequireConfirmedPhoneNumber = true;
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// MVC
builder.Services.AddControllersWithViews();

// Session (конфигурация)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// DI: услуги за кредитен скоринг и кредити
builder.Services.AddScoped<ICreditScoringService, MlCreditScoringService>();
builder.Services.AddScoped<LoansService>();

// Approval/Workflow DI
builder.Services.AddSingleton<LoanApprovalPolicy>();
builder.Services.AddScoped<ILoanApprovalEngine, LoanApprovalEngine>();
builder.Services.AddScoped<ILoanWorkflow, LoanWorkflow>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<LoanContractGenerator>();


builder.Services.AddChatClient(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var key = config["OpenAIKey"] ?? throw new InvalidOperationException("OpenAIKey not set");
    var model = config["ModelName"] ?? "gpt-4o-mini";

    // create the provider client and adapt to IChatClient
    var openAiClient = new OpenAI.OpenAIClient(key);
    var chatClient = openAiClient.GetChatClient(model).AsIChatClient(); // adapt to ME.AI abstraction
    return chatClient;
});

// register your wrapper service so controllers can inject it
builder.Services.AddScoped<AiChatService>();
builder.Services.AddScoped<ForecastService>();

// Background авто-претрениране (стартира на boot и по график)
builder.Services.AddHostedService<MlRetrainHostedService>();

// register your wrapper service so controllers can inject it
builder.Services.AddScoped<AiChatService>();


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

// seed/миграции
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
