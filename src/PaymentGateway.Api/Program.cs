using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Infrastructure.Bank;
using PaymentGateway.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BankSimulatorOptions>(
    builder.Configuration.GetSection(BankSimulatorOptions.SectionName));

builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddSingleton<IPaymentsRepository>(provider => provider.GetRequiredService<PaymentsRepository>());
builder.Services.AddSingleton<IPaymentsService, PaymentsService>();
builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
