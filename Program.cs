using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// HttpClient para comunicação com PlayFab
builder.Services.AddHttpClient();

// CORS para permitir chamadas vindas do Unity/WebGL
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

// ============================================================
// STATUS
// ============================================================

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        message = "Capivarada Password Reset API",
        status = "online"
    });
});

// ============================================================
// RESET PASSWORD
// ============================================================

app.MapPost(
    "/api/password/reset",
    async (
        ResetPasswordRequest request,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory) =>
    {
        // ----------------------------------------------------
        // Validação básica
        // ----------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new
            {
                message =
                    "Token de recuperação não informado."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new
            {
                message =
                    "Nova senha não informada."
            });
        }

        if (request.Password.Length < 6)
        {
            return Results.BadRequest(new
            {
                message =
                    "A nova senha deve ter pelo menos 6 caracteres."
            });
        }

        // ----------------------------------------------------
        // Configurações do PlayFab
        // ----------------------------------------------------

        string? titleId =
            configuration["PLAYFAB_TITLE_ID"];

        string? secretKey =
            configuration["PLAYFAB_SECRET_KEY"];

        if (string.IsNullOrWhiteSpace(titleId))
        {
            return Results.Problem(
                "PLAYFAB_TITLE_ID não configurado."
            );
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Results.Problem(
                "PLAYFAB_SECRET_KEY não configurado."
            );
        }

        // ----------------------------------------------------
        // URL do PlayFab
        // ----------------------------------------------------

        string playFabUrl =
            $"https://{titleId}.playfabapi.com/Admin/ResetPassword";

        // ----------------------------------------------------
        // Corpo enviado ao PlayFab
        // ----------------------------------------------------

        var playFabRequest = new
        {
            Token = request.Token,
            Password = request.Password
        };

        string json =
            JsonSerializer.Serialize(
                playFabRequest
            );

        // ----------------------------------------------------
        // Request HTTP
        // ----------------------------------------------------

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                playFabUrl
            );

        // IMPORTANTE:
        // A Secret Key fica SOMENTE no servidor.
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

        // ----------------------------------------------------
        // Envia para o PlayFab
        // ----------------------------------------------------

        var client =
            httpClientFactory.CreateClient();

        using HttpResponseMessage response =
            await client.SendAsync(
                httpRequest
            );

        string responseBody =
            await response.Content.ReadAsStringAsync();

        // ----------------------------------------------------
        // SUCESSO
        // ----------------------------------------------------

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                "[PlayFab] Senha redefinida com sucesso."
            );

            return Results.Ok(new
            {
                message =
                    "Senha redefinida com sucesso."
            });
        }

        // ----------------------------------------------------
        // ERRO
        // ----------------------------------------------------

        Console.WriteLine(
            "[PlayFab] Erro ao redefinir senha."
        );

        Console.WriteLine(
            responseBody
        );

        return Results.Json(
            new
            {
                message =
                    "Não foi possível redefinir a senha.",

                playFabResponse =
                    responseBody
            },
            statusCode:
                (int)response.StatusCode
        );
    });

app.Run();

// ============================================================
// REQUEST
// ============================================================

public record ResetPasswordRequest(
    string Token,
    string Password
);