using TiengAnh.Models;
using TiengAnh.Repositories;
using TiengAnh.Services;
using TiengAnh.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using MongoDB.Bson.Serialization.Conventions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using TiengAnh.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TiengAnh API",
        Version = "v1",
        Description = "API cho hệ thống học tiếng Anh - Hỗ trợ quản lý người dùng, từ vựng, ngữ pháp, bài test và bài tập",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "TiengAnh Support",
            Email = "support@tienganh.com"
        }
    });
    
    // Thêm XML comments để có mô tả chi tiết hơn
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
    
    // Cấu hình JWT Authentication cho Swagger
    c.AddSecurityDefinition("Cookie", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Cookie Authentication - Sử dụng cookie để xác thực người dùng",
        Name = "Cookie",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Cookie"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Cookie"
                }
            },
            new string[] { }
        }
    });
    
    // Phân nhóm API theo tags
    c.TagActionsBy(api =>
    {
        var controllerName = api.ActionDescriptor.RouteValues["controller"];
        var actionName = api.ActionDescriptor.RouteValues["action"];
        
        if (controllerName == "ApiV1")
        {
            if (actionName?.Contains("health", StringComparison.OrdinalIgnoreCase) == true ||
                actionName?.Contains("system", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "🔧 Hệ thống & Giám sát" };
            else if (actionName?.Contains("auth", StringComparison.OrdinalIgnoreCase) == true ||
                     actionName?.Contains("user", StringComparison.OrdinalIgnoreCase) == true ||
                     actionName?.Contains("password", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "👤 Quản lý người dùng" };
            else if (actionName?.Contains("vocabular", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "📚 Từ vựng" };
            else if (actionName?.Contains("grammar", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "📖 Ngữ pháp" };
            else if (actionName?.Contains("topic", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "🏷️ Chủ đề" };
            else if (actionName?.Contains("test", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "📝 Bài kiểm tra" };
            else if (actionName?.Contains("exercise", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "💪 Bài tập" };
            else if (actionName?.Contains("favorite", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "❤️ Yêu thích" };
            else if (actionName?.Contains("statistic", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "📊 Thống kê" };
            else if (actionName?.Contains("search", StringComparison.OrdinalIgnoreCase) == true)
                return new[] { "🔍 Tìm kiếm" };
        }
        else if (controllerName == "AvatarApi")
        {
            return new[] { "🖼️ Avatar" };
        }
        
        return new[] { controllerName ?? "Other" };
    });
    
    // Sắp xếp operations theo tags
    c.OrderActionsBy((apiDesc) => $"{apiDesc.GroupName}_{apiDesc.HttpMethod}_{apiDesc.RelativePath}");
});

// Register MongoDB services
builder.Services.Configure<TiengAnh.Services.MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoDbService>();

// Register repositories
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<GrammarRepository>();
builder.Services.AddScoped<VocabularyRepository>();
builder.Services.AddScoped<TestRepository>();
builder.Services.AddScoped<UserTestRepository>(); // Add this line

// Fix registration of ITestRepository - ensure TestRepository is registered first, then register the interface
builder.Services.AddScoped<ITestRepository>(sp => sp.GetRequiredService<TestRepository>());

builder.Services.AddScoped<TopicRepository>();
builder.Services.AddScoped<ITopicRepository, TopicRepository>();
builder.Services.AddScoped<ExerciseRepository>(provider =>
{
    var mongoDbService = provider.GetRequiredService<MongoDbService>();
    var hostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();
    return new ExerciseRepository(mongoDbService, hostEnvironment.ContentRootPath);
});

// Register DataSeeder and DataImportService
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped<DataImportService>();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.CallbackPath = "/signin-google";
    
    // Fix correlation issues by configuring events
    options.Events.OnTicketReceived = ctx =>
    {
        Console.WriteLine("Google authentication ticket received successfully");
        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = ctx =>
    {
        Console.WriteLine($"Google authentication failed: {ctx.Failure?.Message}");
        ctx.Response.Redirect("/Account/Login?error=Google_auth_failed");
        ctx.HandleResponse();
        return Task.CompletedTask;
    };

    // Override the default CORS validation to fix correlation issues
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    
    options.SaveTokens = true;
});

// Add session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Suppress nullable warnings
builder.Services.SuppressNullableWarnings();

// Configure Kestrel server
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

// Configure HTTP client timeout
builder.Services.AddHttpClient(string.Empty, client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Configure IIS to allow synchronous IO
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AllowSynchronousIO = true;
});

var app = builder.Build();

// Configure MongoDB serialization
var pack = new ConventionPack
{
    new IgnoreExtraElementsConvention(true)
};
ConventionRegistry.Register("IgnoreExtraElements", pack, t => true);

// Seed data during startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Starting application initialization");

        // Run DataSeeder
        var dataSeeder = services.GetRequiredService<DataSeeder>();
        logger.LogInformation("Running data seeder");
        await dataSeeder.SeedAllDataAsync(); // Changed from SeedDataAsync to SeedAllDataAsync

        // Check seeded data
        var topicRepo = services.GetRequiredService<TopicRepository>();
        var hasTopics = await topicRepo.HasDataAsync();
        logger.LogInformation($"Topics data exists: {hasTopics}");

        var vocabRepo = services.GetRequiredService<VocabularyRepository>();
        var topicVocabs = await vocabRepo.GetByTopicIdAsync(1);
        logger.LogInformation($"Vocabularies for Topic 1: {topicVocabs.Count}");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during application startup");
    }
}

// Import exercises during startup (development only)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var dataImportService = services.GetRequiredService<DataImportService>();
            string exercisesJsonPath = Path.Combine(app.Environment.ContentRootPath, "json", "exercise.json");
            if (File.Exists(exercisesJsonPath))
            {
                await dataImportService.ImportExercisesFromJson(exercisesJsonPath);
                Console.WriteLine($"Imported exercises from {exercisesJsonPath}");
            }
            else
            {
                Console.WriteLine($"Exercise JSON file not found at: {exercisesJsonPath}");
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while importing exercises");
        }
    }
}

// Seed exercises from JSON
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var exerciseRepository = services.GetRequiredService<ExerciseRepository>();
        await exerciseRepository.SeedExercisesFromJsonAsync();
        Console.WriteLine("Seeded exercises from JSON");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding exercises");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    
    // Enable Swagger only in development
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TiengAnh API V1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "TiengAnh API - Tài liệu API";
        c.DefaultModelsExpandDepth(-1);
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        c.DisplayRequestDuration();
        c.EnableFilter();
        c.EnableDeepLinking();
        c.EnableValidator();
        
        // Custom CSS để làm đẹp giao diện
        c.InjectStylesheet("/css/swagger-ui-custom.css");
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Add debug middleware to help with OAuth troubleshooting
app.UseMiddleware<OAuthDebugMiddleware>();

// Add session middleware
app.UseSession();

// Redirect index.html to home
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value == "/index.html")
    {
        context.Response.Redirect("/");
        return;
    }
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Configure MVC routes - Bỏ route api
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Fallback route
app.MapFallbackToController("Index", "Home");

app.Run();