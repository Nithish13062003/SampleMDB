using TariffSearch.Models;
using TariffSearch.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

builder.Services.Configure<SearchFieldSettings>(
    builder.Configuration.GetSection("SearchFieldSettings"));


builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IPdfService, PdfService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
