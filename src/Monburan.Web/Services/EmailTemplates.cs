namespace Monburan.Web.Services;

public static class EmailTemplates
{
    public static (string Subject, string Html) WelcomeEmail(string name, string confirmationLink)
    {
        var subject = "Bem-vindo ao Monburan!";
        var html = $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Bem-vindo ao Monburan</title>
        </head>
        <body style="margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7fa;">
            <table role="presentation" style="width: 100%; border-collapse: collapse;">
                <tr>
                    <td align="center" style="padding: 40px 0;">
                        <table role="presentation" style="width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
                            <tr>
                                <td style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px; text-align: center; border-radius: 8px 8px 0 0;">
                                    <h1 style="color: #ffffff; margin: 0; font-size: 28px;">Monburan</h1>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 40px;">
                                    <h2 style="color: #333333; margin: 0 0 20px 0;">Olá, {EscapeHtml(name)}! 👋</h2>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;">
                                        Seja bem-vindo ao Monburan! Estamos muito felizes em tê-lo conosco.
                                        Para começar a usar sua conta, por favor confirme seu email clicando no botão abaixo:
                                    </p>
                                    <table role="presentation" style="margin: 30px 0;">
                                        <tr>
                                            <td align="center" style="background-color: #667eea; border-radius: 6px;">
                                                <a href="{EscapeHtml(confirmationLink)}" style="display: inline-block; padding: 14px 32px; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600;">
                                                    Confirmar Minha Conta
                                                </a>
                                            </td>
                                        </tr>
                                    </table>
                                    <p style="color: #999999; font-size: 14px; line-height: 1.6; margin: 20px 0 0 0;">
                                        Se você não criou uma conta no Monburan, por favor ignore este email.
                                    </p>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f8f9fa; padding: 20px 40px; text-align: center; border-radius: 0 0 8px 8px;">
                                    <p style="color: #999999; font-size: 12px; margin: 0;">
                                        © {DateTime.Now.Year} Monburan. Todos os direitos reservados.
                                    </p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

        return (subject, html);
    }

    public static (string Subject, string Html) ApplicationReceivedEmail(string candidateName, string jobTitle)
    {
        var subject = "Candidatura Recebida - Monburan";
        var html = $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Candidatura Recebida</title>
        </head>
        <body style="margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7fa;">
            <table role="presentation" style="width: 100%; border-collapse: collapse;">
                <tr>
                    <td align="center" style="padding: 40px 0;">
                        <table role="presentation" style="width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
                            <tr>
                                <td style="background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); padding: 40px; text-align: center; border-radius: 8px 8px 0 0;">
                                    <h1 style="color: #ffffff; margin: 0; font-size: 28px;">Monburan</h1>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 40px;">
                                    <h2 style="color: #333333; margin: 0 0 20px 0;">Olá, {EscapeHtml(candidateName)}! ✅</h2>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;">
                                        Sua candidatura para a vaga de <strong>{EscapeHtml(jobTitle)}</strong> foi recebida com sucesso!
                                    </p>
                                    <div style="background-color: #f0f9ff; border-left: 4px solid #11998e; padding: 16px; margin: 20px 0;">
                                        <p style="color: #333333; font-size: 14px; margin: 0;">
                                            📋 <strong>Status:</strong> Candidatura em análise<br>
                                            ⏰ <strong>Próximos passos:</strong> O anunciante irá revisar seu perfil e entrará em contato se houver interesse.
                                        </p>
                                    </div>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 20px 0 0 0;">
                                        Você receberá atualizações sobre o status da sua candidatura por email.
                                    </p>
                                    <p style="color: #999999; font-size: 14px; line-height: 1.6; margin: 20px 0 0 0;">
                                        Boa sorte! 🍀
                                    </p>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f8f9fa; padding: 20px 40px; text-align: center; border-radius: 0 0 8px 8px;">
                                    <p style="color: #999999; font-size: 12px; margin: 0;">
                                        © {DateTime.Now.Year} Monburan. Todos os direitos reservados.
                                    </p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

        return (subject, html);
    }

    public static (string Subject, string Html) ApplicationStatusChangedEmail(string candidateName, string jobTitle, string newStatus)
    {
        var subject = "Status da Candidatura Atualizado - Monburan";
        var html = $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Status Atualizado</title>
        </head>
        <body style="margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7fa;">
            <table role="presentation" style="width: 100%; border-collapse: collapse;">
                <tr>
                    <td align="center" style="padding: 40px 0;">
                        <table role="presentation" style="width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
                            <tr>
                                <td style="background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 40px; text-align: center; border-radius: 8px 8px 0 0;">
                                    <h1 style="color: #ffffff; margin: 0; font-size: 28px;">Monburan</h1>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 40px;">
                                    <h2 style="color: #333333; margin: 0 0 20px 0;">Olá, {EscapeHtml(candidateName)}! 📢</h2>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;">
                                        O status da sua candidatura para a vaga de <strong>{EscapeHtml(jobTitle)}</strong> foi atualizado.
                                    </p>
                                    <div style="background-color: #fff3cd; border-left: 4px solid #f5576c; padding: 16px; margin: 20px 0; text-align: center;">
                                        <p style="color: #333333; font-size: 18px; margin: 0; font-weight: 600;">
                                            Novo Status: {EscapeHtml(newStatus)}
                                        </p>
                                    </div>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 20px 0 0 0;">
                                        Acesse sua conta no Monburan para mais detalhes.
                                    </p>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f8f9fa; padding: 20px 40px; text-align: center; border-radius: 0 0 8px 8px;">
                                    <p style="color: #999999; font-size: 12px; margin: 0;">
                                        © {DateTime.Now.Year} Monburan. Todos os direitos reservados.
                                    </p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

        return (subject, html);
    }

    public static (string Subject, string Html) JobExpiredEmail(string advertiserName, string jobTitle)
    {
        var subject = "Vaga Expirada - Monburan";
        var html = $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Vaga Expirada</title>
        </head>
        <body style="margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7fa;">
            <table role="presentation" style="width: 100%; border-collapse: collapse;">
                <tr>
                    <td align="center" style="padding: 40px 0;">
                        <table role="presentation" style="width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
                            <tr>
                                <td style="background: linear-gradient(135deg, #fa709a 0%, #fee140 100%); padding: 40px; text-align: center; border-radius: 8px 8px 0 0;">
                                    <h1 style="color: #ffffff; margin: 0; font-size: 28px;">Monburan</h1>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 40px;">
                                    <h2 style="color: #333333; margin: 0 0 20px 0;">Olá, {EscapeHtml(advertiserName)}! ⏰</h2>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;">
                                        A vaga <strong>{EscapeHtml(jobTitle)}</strong> expirou e não está mais visível para os candidatos.
                                    </p>
                                    <div style="background-color: #fff3cd; border-left: 4px solid #fa709a; padding: 16px; margin: 20px 0;">
                                        <p style="color: #333333; font-size: 14px; margin: 0;">
                                            💡 <strong>Dica:</strong> Você pode renovar a vaga para continuar recebendo candidaturas ou criar uma nova vaga com informações atualizadas.
                                        </p>
                                    </div>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 20px 0 0 0;">
                                        Acesse seu painel para gerenciar suas vagas.
                                    </p>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f8f9fa; padding: 20px 40px; text-align: center; border-radius: 0 0 8px 8px;">
                                    <p style="color: #999999; font-size: 12px; margin: 0;">
                                        © {DateTime.Now.Year} Monburan. Todos os direitos reservados.
                                    </p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

        return (subject, html);
    }

    public static (string Subject, string Html) PasswordResetEmail(string email, string resetLink)
    {
        var subject = "Recuperação de Senha - Monburan";
        var html = $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Recuperação de Senha</title>
        </head>
        <body style="margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7fa;">
            <table role="presentation" style="width: 100%; border-collapse: collapse;">
                <tr>
                    <td align="center" style="padding: 40px 0;">
                        <table role="presentation" style="width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
                            <tr>
                                <td style="background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); padding: 40px; text-align: center; border-radius: 8px 8px 0 0;">
                                    <h1 style="color: #ffffff; margin: 0; font-size: 28px;">Monburan</h1>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 40px;">
                                    <h2 style="color: #333333; margin: 0 0 20px 0;">Recuperação de Senha 🔐</h2>
                                    <p style="color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;">
                                        Recebemos uma solicitação para redefinir a senha da sua conta associada ao email <strong>{EscapeHtml(email)}</strong>.
                                    </p>
                                    <table role="presentation" style="margin: 30px 0;">
                                        <tr>
                                            <td align="center" style="background-color: #4facfe; border-radius: 6px;">
                                                <a href="{EscapeHtml(resetLink)}" style="display: inline-block; padding: 14px 32px; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600;">
                                                    Redefinir Minha Senha
                                                </a>
                                            </td>
                                        </tr>
                                    </table>
                                    <div style="background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 16px; margin: 20px 0;">
                                        <p style="color: #333333; font-size: 14px; margin: 0;">
                                            ⚠️ <strong>Importante:</strong> Este link expira em 24 horas. Se você não solicitou esta alteração, ignore este email e sua senha permanecerá a mesma.
                                        </p>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f8f9fa; padding: 20px 40px; text-align: center; border-radius: 0 0 8px 8px;">
                                    <p style="color: #999999; font-size: 12px; margin: 0;">
                                        © {DateTime.Now.Year} Monburan. Todos os direitos reservados.
                                    </p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;

        return (subject, html);
    }

    private static string EscapeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
