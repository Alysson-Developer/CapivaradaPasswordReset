using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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
// CALLBACK DO PLAYFAB
// ============================================================
//
// PlayFab envia o jogador para:
//
// /api/password/callback?token=TOKEN
//
// O backend carrega a página HTML e coloca o token nela.
//
// ============================================================

app.MapGet(
    "/api/password/callback",
    async (string? token) =>
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest(
                "Token de recuperação não informado."
            );
        }

        string webRoot =
            app.Environment.WebRootPath ?? "wwwroot";

        string filePath =
            Path.Combine(
                webRoot,
                "reset-password.html"
            );

        if (!File.Exists(filePath))
        {
            return Results.Problem(
                "Página de redefinição não encontrada no servidor."
            );
        }

        string html =
            await File.ReadAllTextAsync(filePath);

        // Protege o token antes de colocá-lo no HTML.
        string safeToken =
            System.Net.WebUtility.HtmlEncode(token);

        html = html.Replace(
            "{{RECOVERY_TOKEN}}",
            safeToken
        );

        return Results.Content(
            html,
            "text/html; charset=utf-8"
        );
    });

// ============================================================
// RESET PASSWORD
// ============================================================
//
// Recebe o formulário:
//
// token
// password
// confirmPassword
//
// Depois chama o PlayFab Admin/ResetPassword.
//
// ============================================================

app.MapPost(
    "/api/password/reset",
    async (
        HttpRequest request,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory) =>
    {
        // ----------------------------------------------------
        // Verifica se é formulário
        // ----------------------------------------------------

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(
                "Formato de requisição inválido."
            );
        }

        var form =
            await request.ReadFormAsync();

        string token =
            form["token"].ToString().Trim();

        string password =
            form["password"].ToString();

        string confirmPassword =
            form["confirmPassword"].ToString();

        // ----------------------------------------------------
        // Validações
        // ----------------------------------------------------

        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest(
                "Token de recuperação não informado."
            );
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Results.BadRequest(
                "Nova senha não informada."
            );
        }

        if (password.Length < 6)
        {
            return Results.BadRequest(
                "A senha deve ter pelo menos 6 caracteres."
            );
        }

        if (password != confirmPassword)
        {
            return Results.BadRequest(
                "As senhas não coincidem."
            );
        }

        // ----------------------------------------------------
        // Environment Variables
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
        // URL DO PLAYFAB
        // ----------------------------------------------------

        string playFabUrl =
            $"https://{titleId}.playfabapi.com/Admin/ResetPassword";

        var playFabRequest = new
        {
            Token = token,
            Password = password
        };

        string json =
            JsonSerializer.Serialize(
                playFabRequest
            );

        // ----------------------------------------------------
        // HTTP REQUEST
        // ----------------------------------------------------

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                playFabUrl
            );

        // A Secret Key fica somente no Render.
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

            return Results.Content(
                CreateSuccessPage(),
                "text/html; charset=utf-8"
            );
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

        return Results.Content(
            CreateErrorPage(
                "Não foi possível redefinir sua senha.",
                "O link pode ter expirado ou já ter sido utilizado."
            ),
            "text/html; charset=utf-8",
            statusCode: (int)response.StatusCode
        );
    });

app.Run();

// ============================================================
// PÁGINA DE SUCESSO
// ============================================================

static string CreateSuccessPage()
{
    return """
    <!DOCTYPE html>
    <html lang="pt-BR">

    <head>

        <meta charset="UTF-8">

        <meta
            name="viewport"
            content="width=device-width, initial-scale=1.0"
        >

        <meta
            name="referrer"
            content="no-referrer"
        >

        <title>Senha alterada — Capivarada!</title>

        <style>

            * {
                box-sizing: border-box;
            }

            body {
                margin: 0;
                min-height: 100vh;

                display: flex;
                align-items: center;
                justify-content: center;

                padding: 24px;

                background:
                    radial-gradient(
                        circle at top,
                        #087A3E,
                        #062E1B 60%,
                        #031B10
                    );

                font-family:
                    Arial,
                    Helvetica,
                    sans-serif;

                color: white;
            }

            .card {
                width: 100%;
                max-width: 500px;

                padding: 45px 35px;

                text-align: center;

                background: #0B4D2A;

                border-radius: 20px;

                border-top: 7px solid #F7D117;

                box-shadow:
                    0 20px 60px rgba(0, 0, 0, .4);
            }

            .logo {
                margin-bottom: 30px;

                font-size: 30px;
                font-weight: 900;

                color: #F7D117;
            }

            .icon {
                margin-bottom: 20px;

                font-size: 60px;

                color: #F7D117;
            }

            h1 {
                margin: 0 0 15px;

                font-size: 28px;
            }

            p {
                margin: 0;

                line-height: 1.6;

                color: #DDF5E7;
            }

        </style>

    </head>

    <body>

        <main class="card">

            <div class="logo">
                🦫 CAPIVARADA!
            </div>

            <div class="icon">
                ✓
            </div>

            <h1>
                Senha alterada!
            </h1>

            <p>
                Sua senha foi alterada com sucesso.
            </p>

            <p style="margin-top: 12px;">
                Agora você pode voltar para o
                <strong>Capivarada!</strong>
                e fazer login normalmente.
            </p>

        </main>

    </body>

    </html>
    """;
}

// ============================================================
// PÁGINA DE ERRO
// ============================================================

static string CreateErrorPage(
    string title,
    string message)
{
    string safeTitle =
        System.Net.WebUtility.HtmlEncode(title);

    string safeMessage =
        System.Net.WebUtility.HtmlEncode(message);

    return $$"""
    <!DOCTYPE html>
    <html lang="pt-BR">

    <head>

        <meta charset="UTF-8">

        <meta
            name="viewport"
            content="width=device-width, initial-scale=1.0"
        >

        <meta
            name="referrer"
            content="no-referrer"
        >

        <title>Erro — Capivarada!</title>

        <style>

            * {
                box-sizing: border-box;
            }

            body {
                margin: 0;
                min-height: 100vh;

                display: flex;
                align-items: center;
                justify-content: center;

                padding: 24px;

                background:
                    radial-gradient(
                        circle at top,
                        #087A3E,
                        #062E1B 60%,
                        #031B10
                    );

                font-family:
                    Arial,
                    Helvetica,
                    sans-serif;

                color: white;
            }

            .card {
                width: 100%;
                max-width: 500px;

                padding: 45px 35px;

                text-align: center;

                background: #0B4D2A;

                border-radius: 20px;

                border-top: 7px solid #F7D117;

                box-shadow:
                    0 20px 60px rgba(0, 0, 0, .4);
            }

            .logo {
                margin-bottom: 30px;

                font-size: 30px;
                font-weight: 900;

                color: #F7D117;
            }

            .icon {
                margin-bottom: 20px;

                font-size: 55px;

                color: #F7D117;
            }

            h1 {
                margin: 0 0 15px;

                font-size: 27px;
            }

            p {
                margin: 0;

                line-height: 1.6;

                color: #DDF5E7;
            }

        </style>

    </head>

    <body>

        <main class="card">

            <div class="logo">
                🦫 CAPIVARADA!
            </div>

            <div class="icon">
                ⚠
            </div>

            <h1>
                {{safeTitle}}
            </h1>

            <p>
                {{safeMessage}}
            </p>

        </main>

    </body>

    </html>
    """;
}