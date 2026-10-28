namespace CineVibe.API.Templates
{
    public static class EmailTemplates
    {
        public static string GetPasswordResetHtml(string resetLink)
        {
            return $@"
                <h2>Password Reset Request</h2>
                <p>We received a request to reset your password.</p>
                <p>Click the button below to set a new password. 
                   The link is valid for <strong>1 hour</strong>.</p>
                <a href=""{resetLink}"" 
                   style=""
                      display:inline-block;
                      padding:12px 24px;
                      background:#e50914;
                      color:#fff;
                      text-decoration:none;
                      border-radius:4px;
                      font-weight:bold;"">
                   Reset Password
                </a>
                <p>If you did not request a password reset, ignore this email.</p>
            ";
        }
    }
}