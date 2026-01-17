namespace Boxty.ServerBase.Config
{
    public class AppOptions
    {
        public required ConnectionStringsOptions ConnectionStrings { get; set; }
        public required LoggingOptions Logging { get; set; }
        public required CorsOptions Cors { get; set; }
        public required string AllowedHosts { get; set; }
        public required JwtOptions Jwt { get; set; }
        public required KeycloakAdminClientOptions KeycloakAdminClient { get; set; }
        public required AuthModuleOptions AuthModule { get; set; }
        public required string[] Modules { get; set; }
        public required EmailOptions Email { get; set; }
    }

    public class ConnectionStringsOptions
    {
        public required string DefaultConnection { get; set; }
        public required string AzureBlobStorage { get; set; }
    }

    public class LoggingOptions
    {
        public required LogLevelOptions LogLevel { get; set; }
    }

    public class LogLevelOptions
    {
        public required string Default { get; set; }
        public required string MicrosoftAspNetCore { get; set; }
    }

    public class CorsOptions
    {
        public required string AllowedClientOrigin { get; set; }
        public required string AllowedServerOrigin { get; set; }
        public required string AllowedMethods { get; set; }
        public required string AllowedHeaders { get; set; }
        public required string ExposedHeaders { get; set; }
    }

    public class JwtOptions
    {
        public required string MetadataAddress { get; set; }
        public required string Issuer { get; set; }
        public required string Audience { get; set; }
        public required bool RequireHttpsMetadata { get; set; }
    }

    public class KeycloakAdminClientOptions
    {
        public required string KeycloakUrl { get; set; }
        public required string Realm { get; set; }
        public required string ClientId { get; set; }
        public required string ClientSecret { get; set; }
    }

    public class AuthModuleOptions
    {
        public required string BaseUrl { get; set; }
    }

    public class EmailOptions
    {
        public required string SenderAddress { get; set; }
        public required string SenderName { get; set; }
        public required string SmtpHost { get; set; }
        public required int SmtpPort { get; set; }
        public required string SmtpUsername { get; set; }
        public required string SmtpPassword { get; set; }
        public required bool EnableEmailSending { get; set; }
    }
}
