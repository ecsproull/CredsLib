namespace CredsLib
{
    using Newtonsoft.Json;
    using Renci.SshNet;
    using System;
    using System.Configuration;
    using System.Data.Entity.Core.EntityClient;
    using System.Data.SqlClient;
    using System.IO;

    public class CredentialManager
    {
        public static string GetConnectionString(string requestedPerms, string databaseName, string connName, string sshPath = "")
        {
            string username = string.Empty;
            string password = string.Empty;
            string authentication = string.Empty;
            string server = string.Empty;
            string database = string.Empty;

            if (string.IsNullOrEmpty(sshPath))
            {
                sshPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".ssh");
            }

            var keyFile = new PrivateKeyFile(Path.Combine(sshPath, "id_ed25519"));
            var client = new SshClient("credserver", "credserver", keyFile);

            client.Connect();

            var cmd = client.CreateCommand($"~/credserver/{requestedPerms}");

            var result = cmd.Execute();
            var credentials = JsonConvert.DeserializeObject<Credentials>(result);
            if (credentials != null)
            {
                username = credentials.username;
                password = credentials.password;
                authentication = credentials.authentication;
                server = credentials.server;
                database = credentials.database;
            }
            client.Disconnect();

            // Build the SQL connection string using SqlConnectionStringBuilder
            var sqlBuilder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                Encrypt = false,
                TrustServerCertificate = true,
                MultipleActiveResultSets = true,
                ApplicationName = "EntityFramework"
            };

            if (authentication.Equals("SQL", StringComparison.OrdinalIgnoreCase))
            {
                sqlBuilder.UserID = username;
                sqlBuilder.Password = password;
            }
            else if (authentication.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                sqlBuilder.IntegratedSecurity = true;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported authentication type: {authentication}");
            }

            // Build the Entity Framework connection string using EntityConnectionStringBuilder
            var entityBuilder = new EntityConnectionStringBuilder
            {
                Provider = "System.Data.SqlClient",
                ProviderConnectionString = sqlBuilder.ToString(),
                Metadata = String.IsNullOrEmpty(connName) ? null : GetEntityMetadata(connName)
            };

            return entityBuilder.ToString();
        }

        public static string GetEntityMetadata(string connectionStringName)
        {
            var settings = ConfigurationManager.ConnectionStrings[connectionStringName];
            if (settings == null)
                throw new InvalidOperationException($"Connection string '{connectionStringName}' not found.");

            var cs = settings.ConnectionString ?? string.Empty;

            // Try using EntityConnectionStringBuilder for a proper EntityClient connection string.
            try
            {
                var builder = new EntityConnectionStringBuilder(cs);
                // EntityConnectionStringBuilder.Metadata returns the metadata value (or empty string if none).
                return string.IsNullOrEmpty(builder.Metadata) ? null : builder.Metadata;
            }
            catch (ArgumentException)
            {
                // Not an EntityClient-style connection string; fall back to manual parsing.
                var parts = cs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var kv = part.Trim();
                    if (kv.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                        return kv.Substring("metadata=".Length);
                }

                return null;
            }
        }

        private class Credentials
        {
            public string username { get; set; }
            public string password { get; set; }
            public string server { get; set; }
            public string authentication { get; set; }
            public string database { get; set; }
        }
    }
}