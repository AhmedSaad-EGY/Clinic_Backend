using Microsoft.AspNetCore.Diagnostics.HealthChecks;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ProductionConfigurationValidator.Validate(
    builder.Configuration,
    builder.Environment.IsProduction());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "clinic.xsrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, ApiCurrentUser>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(
        AuthorizationPolicyNames.PasswordChanged,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(IdentityClaimNames.MustChangePassword, bool.FalseString.ToLowerInvariant()))
    .AddPolicy(
        AuthorizationPolicyNames.AdminOnly,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(RoleNames.Admin)
            .RequireClaim(IdentityClaimNames.MustChangePassword, bool.FalseString.ToLowerInvariant()))
    .AddPolicy(
        AuthorizationPolicyNames.SecretaryOnly,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(RoleNames.Secretary)
            .RequireClaim(IdentityClaimNames.MustChangePassword, bool.FalseString.ToLowerInvariant()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sql-server", tags: ["ready"]);

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    DatabaseInitializer initializer =
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseExceptionHandler();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();

//}
//else
//{
    app.UseHsts();
//}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/health"))
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
    }

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
    }

    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();

app.Run();
