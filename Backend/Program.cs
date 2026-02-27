using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Backend.Repositories;
using Backend.Repositories.IRepository;

var builder = WebApplication.CreateBuilder(args);
Env.Load();

// Configure services
builder.Services.AddControllers();

// OpenAPI / Swagger for easy endpoint testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow the frontend origin(s) - adjust ports if different
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Configure MongoDB
var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
                       ?? throw new InvalidOperationException("MONGO_CONNECTION_STRING is not set.");
var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME")
                   ?? throw new InvalidOperationException("MONGO_DATABASE_NAME is not set.");

var mongoClient = new MongoClient(connectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

// Register repositories
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IFriendsRepository, FriendsRepository>();
builder.Services.AddScoped<IParticipantsRepository, ParticipantsRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();

// In development: use Swagger and do NOT force HTTPS redirection (avoids the https-port warning).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

// Use CORS before controllers
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();