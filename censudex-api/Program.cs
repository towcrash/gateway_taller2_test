using censudex_api.src.Services;
using InventoryService.Grpc;
using ProductService.Grpc;
using OrdersService.Grpc;
using Polly;
using Polly.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

builder.Host.UseSerilog((ctx, lc) => lc.WriteTo.Console());

builder.Services.AddHttpClient("AuthService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AuthService:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<TokenValidationInterceptor>();

// Apply to all gRPC services
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<TokenValidationInterceptor>();
});

builder.Services.AddGrpcClient<Inventory.InventoryClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Services:GrpcBalancer"]);
});


builder.Services.AddGrpcClient<ProductsService.ProductsServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Services:GrpcBalancer"]);
});

builder.Services.AddGrpcClient<OrdersService.Grpc.OrdersService.OrdersServiceClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Services:GrpcBalancer"]);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped<InventoryGrpcAdapter>();
builder.Services.AddScoped<ProductsGrpcAdapter>();
builder.Services.AddScoped<OrdersGrpcAdapter>();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
