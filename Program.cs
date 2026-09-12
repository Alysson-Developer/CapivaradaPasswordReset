var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () =>
    Results.Ok(new
    {
        message = "Capivarada Password Reset API",
        status = "online"
    })
);

app.Run();