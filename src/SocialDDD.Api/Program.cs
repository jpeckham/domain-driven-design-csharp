using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SocialDDD.Application.Social.Posts;
using SocialDDD.Application.Social.Posts.Commands;
using SocialDDD.Application.Social.Posts.Queries;
using SocialDDD.Application.Social.Follows;
using SocialDDD.Application.Identity.Accounts;
using SocialDDD.Application.Identity.Accounts.Commands;
using SocialDDD.Application.Social.Profiles.Commands;
using SocialDDD.Application.Social.Profiles.Queries;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<FollowDomainService>();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<BeginProfileImageUploadCommandHandler>();
builder.Services.AddScoped<CompleteProfileImageUploadCommandHandler>();
builder.Services.AddScoped<RemoveProfileImageCommandHandler>();
builder.Services.AddScoped<GetProfileImageQueryHandler>();
builder.Services.AddScoped<PostService>();
builder.Services.AddScoped<LikePostCommandHandler>();
builder.Services.AddScoped<UnlikePostCommandHandler>();
builder.Services.AddScoped<CreateReplyCommandHandler>();
builder.Services.AddScoped<CreateRepostCommandHandler>();
builder.Services.AddScoped<DeleteRepostCommandHandler>();
builder.Services.AddScoped<GetPostWithConversationQueryHandler>();
builder.Services.AddScoped<SearchPostsQueryHandler>();
builder.Services.AddScoped<BeginPostMediaUploadCommandHandler>();
builder.Services.AddScoped<CompletePostMediaUploadCommandHandler>();
builder.Services.AddScoped<GetPostMediaQueryHandler>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var secret = jwtSection["Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins("https://localhost:7200", "http://localhost:5200")
         .AllowAnyHeader()
         .AllowAnyMethod()));

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SocialDDD API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
