using Clinic.Api.Infrastructure.Errors;
using Clinic.Api.Infrastructure.Identity;
using Clinic.Application;
using Clinic.Application.Abstractions.Identity;
using Clinic.Infrastructure;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    DatabaseInitializer initializer =
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
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

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
