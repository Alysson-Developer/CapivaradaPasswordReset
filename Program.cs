using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ResetPassword", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("ResetPassword");

// =====================================================
// STATUS
// =====================================================

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        message = "Capivarada Password Reset API",
        status = "online"
    });
});

// =====================================================
// CALLBACK DO PLAYFAB
// =====================================================

app.MapGet("/api/password/callback", (
    string? token,
    IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.BadRequest(new
        {
            message = "Token de recuperação não informado."
        });
    }

    string? webGlUrl =
        configuration["WEBGL_RESET_URL"];

    if (string.IsNullOrWhiteSpace(webGlUrl))
    {
        return Results.Problem(
            "WEBGL_RESET_URL não configurada no servidor."
        );
    }

    string redirectUrl =
        $"{webGlUrl}?resetToken={Uri.EscapeDataString(token)}";

    return Results.Redirect(redirectUrl);
});

// =====================================================
// RESET DA SENHA
// =====================================================

app.MapPost("/api/password/reset", async (
    ResetPasswordRequest request,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return Results.BadRequest(new
        {
            message = "Token de recuperação não informado."
        });
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new
        {
            message = "Nova senha não informada."
        });
    }

    if (request.Password.Length < 6)
    {
        return Results.BadRequest(new
        {
            message = "A senha deve ter pelo menos 6 caracteres."
        });
    }

    string? titleId =
        configuration["PLAYFAB_TITLE_ID"];

    string? secretKey =
        configuration["PLAYFAB_SECRET_KEY"];

    if (string.IsNullOrWhiteSpace(titleId) ||
        string.IsNullOrWhiteSpace(secretKey))
    {
        return Results.Problem(
            "Configuração do PlayFab não encontrada no servidor."
        );
    }

    var playFabUrl =
        $"https://{titleId}.playfabapi.com/Admin/ResetPassword";

    var playFabRequest = new
    {
        Token = request.Token,
        Password = request.Password
    };

    string json =
        JsonSerializer.Serialize(playFabRequest);

    using var httpRequest =
        new HttpRequestMessage(
            HttpMethod.Post,
            playFabUrl
        );

    httpRequest.Headers.Add(
        "X-SecretKey",
        secretKey
    );

    httpRequest.Content =
        new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

    var client =
        httpClientFactory.CreateClient();

    using HttpResponseMessage response =
        await client.SendAsync(httpRequest);

    string responseBody =
        await response.Content.ReadAsStringAsync();

    if (response.IsSuccessStatusCode)
    {
        return Results.Ok(new
        {
            message = "Senha redefinida com sucesso."
        });
    }

    return Results.Json(
        new
        {
            message =
                "Não foi possível redefinir a senha.",

            playFabResponse =
                responseBody
        },
        statusCode: (int)response.StatusCode
    );
});

app.Run();

// =====================================================
// MODELOS
// =====================================================

public record ResetPasswordRequest(
    string Token,
    string Password
);