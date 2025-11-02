// File: `Frontend/Program.cs`
using Frontend.Service.IService;
using Frontend.Service;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Frontend;
using Blazored.LocalStorage;
using Frontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddBlazoredLocalStorage();

// Set the HttpClient BaseAddress to your BACKEND URL so calls like "api/Users/opret" go to the backend.
// Replace the URI with the actual backend address and scheme you run (http/https and port).
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5132/") });

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IFriendService, FriendService>();
await builder.Build().RunAsync();