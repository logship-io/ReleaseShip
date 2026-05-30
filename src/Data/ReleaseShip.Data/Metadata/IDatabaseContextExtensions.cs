using Dapper;
using Microsoft.Extensions.Logging;
using ReleaseShip.Data.Relational;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReleaseShip.Data.Metadata
{
    public static partial class IDatabaseContextExtensions
    {
        [LoggerMessage(LogLevel.Information, "Created {Total} Tables for database initialization.")]
        internal static partial void log_TablesInitialized(ILogger logger, int total);

        public static async Task InitializeDatabaseAsync(this IDatabaseContext ctx, ILogger logger, CancellationToken token)
        {
            await ctx.InitializeAsync(token);
            using var conn = await ctx.GetConnection(token);
            int total = await conn.ExecuteAsync(
                @"CREATE TABLE IF NOT EXISTS projects (id TEXT PRIMARY KEY, name TEXT);
                CREATE TABLE IF NOT EXISTS platforms (id TEXT PRIMARY KEY);
                CREATE TABLE IF NOT EXISTS bin_release (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    project_id TEXT, 
                    platform_id TEXT, 
                    publish_date_utc INTEGER,
                    path TEXT,
                    FOREIGN KEY (project_id) REFERENCES projects (id),
                    FOREIGN KEY (platform_id) REFERENCES platforms (id)
                );
                CREATE TABLE IF NOT EXISTS bin_release_tags (
                    id TEXT,
                    bin_release_id INTEGER,
                    PRIMARY KEY (id, bin_release_id),
                    FOREIGN KEY (bin_release_id) REFERENCES bin_release (id)
                );
                CREATE TABLE IF NOT EXISTS registry_repositories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    namespace TEXT NOT NULL DEFAULT '',
                    name TEXT NOT NULL,
                    full_name TEXT NOT NULL UNIQUE,
                    description TEXT,
                    allow_anonymous_pull INTEGER NOT NULL DEFAULT 0,
                    allow_delete INTEGER NOT NULL DEFAULT 0,
                    default_tag_mutability TEXT NOT NULL DEFAULT 'mutable',
                    created_date_utc INTEGER NOT NULL,
                    updated_date_utc INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS registry_tag_rules (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    repository_id INTEGER NOT NULL,
                    pattern TEXT NOT NULL,
                    is_immutable INTEGER NOT NULL DEFAULT 1,
                    created_date_utc INTEGER NOT NULL,
                    FOREIGN KEY (repository_id) REFERENCES registry_repositories (id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_blobs (
                    digest TEXT PRIMARY KEY,
                    algorithm TEXT NOT NULL,
                    size_bytes INTEGER NOT NULL,
                    media_type TEXT,
                    storage_key TEXT NOT NULL,
                    created_date_utc INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS registry_blob_links (
                    repository_id INTEGER NOT NULL,
                    digest TEXT NOT NULL,
                    created_date_utc INTEGER NOT NULL,
                    PRIMARY KEY (repository_id, digest),
                    FOREIGN KEY (repository_id) REFERENCES registry_repositories (id) ON DELETE CASCADE,
                    FOREIGN KEY (digest) REFERENCES registry_blobs (digest) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_manifests (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    repository_id INTEGER NOT NULL,
                    digest TEXT NOT NULL,
                    media_type TEXT NOT NULL,
                    schema_version INTEGER NOT NULL,
                    artifact_type TEXT,
                    subject_digest TEXT,
                    config_digest TEXT,
                    content_bytes BLOB NOT NULL,
                    created_date_utc INTEGER NOT NULL,
                    UNIQUE (repository_id, digest),
                    FOREIGN KEY (repository_id) REFERENCES registry_repositories (id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_manifest_blobs (
                    manifest_id INTEGER NOT NULL,
                    digest TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (manifest_id, digest, kind),
                    FOREIGN KEY (manifest_id) REFERENCES registry_manifests (id) ON DELETE CASCADE,
                    FOREIGN KEY (digest) REFERENCES registry_blobs (digest) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_tags (
                    repository_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    manifest_id INTEGER NOT NULL,
                    created_date_utc INTEGER NOT NULL,
                    updated_date_utc INTEGER NOT NULL,
                    PRIMARY KEY (repository_id, name),
                    FOREIGN KEY (repository_id) REFERENCES registry_repositories (id) ON DELETE CASCADE,
                    FOREIGN KEY (manifest_id) REFERENCES registry_manifests (id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_uploads (
                    id TEXT PRIMARY KEY,
                    repository_id INTEGER NOT NULL,
                    storage_key TEXT NOT NULL,
                    offset_bytes INTEGER NOT NULL DEFAULT 0,
                    started_date_utc INTEGER NOT NULL,
                    expires_date_utc INTEGER NOT NULL,
                    state TEXT NOT NULL,
                    completed_digest TEXT,
                    FOREIGN KEY (repository_id) REFERENCES registry_repositories (id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_principals (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT NOT NULL UNIQUE,
                    password_hash TEXT,
                    is_bootstrap_admin INTEGER NOT NULL DEFAULT 0,
                    created_date_utc INTEGER NOT NULL,
                    disabled_date_utc INTEGER
                );
                CREATE TABLE IF NOT EXISTS registry_roles (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE,
                    description TEXT
                );
                CREATE TABLE IF NOT EXISTS registry_role_assignments (
                    principal_id INTEGER NOT NULL,
                    role_id INTEGER NOT NULL,
                    scope_type TEXT NOT NULL,
                    scope_value TEXT NOT NULL DEFAULT '',
                    PRIMARY KEY (principal_id, role_id, scope_type, scope_value),
                    FOREIGN KEY (principal_id) REFERENCES registry_principals (id) ON DELETE CASCADE,
                    FOREIGN KEY (role_id) REFERENCES registry_roles (id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_tokens (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    principal_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    hashed_secret TEXT NOT NULL,
                    scope_type TEXT NOT NULL,
                    scope_value TEXT NOT NULL DEFAULT '',
                    can_pull INTEGER NOT NULL DEFAULT 1,
                    can_push INTEGER NOT NULL DEFAULT 0,
                    can_delete INTEGER NOT NULL DEFAULT 0,
                    created_date_utc INTEGER NOT NULL,
                    last_used_date_utc INTEGER,
                    revoked_date_utc INTEGER,
                    FOREIGN KEY (principal_id) REFERENCES registry_principals (id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS registry_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS registry_audit_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    principal_name TEXT,
                    action TEXT NOT NULL,
                    target_type TEXT NOT NULL,
                    target_value TEXT NOT NULL,
                    created_date_utc INTEGER NOT NULL,
                    metadata_json TEXT
                );
                CREATE INDEX IF NOT EXISTS ix_registry_repositories_namespace ON registry_repositories (namespace);
                CREATE INDEX IF NOT EXISTS ix_registry_tags_manifest_id ON registry_tags (manifest_id);
                CREATE INDEX IF NOT EXISTS ix_registry_tags_repository_updated ON registry_tags (repository_id, updated_date_utc);
                CREATE INDEX IF NOT EXISTS ix_registry_blobs_algorithm ON registry_blobs (algorithm);
                CREATE INDEX IF NOT EXISTS ix_registry_manifests_repository_digest ON registry_manifests (repository_id, digest);
                CREATE INDEX IF NOT EXISTS ix_registry_uploads_repository_state ON registry_uploads (repository_id, state);
                CREATE INDEX IF NOT EXISTS ix_registry_tokens_principal_id ON registry_tokens (principal_id);");
            log_TablesInitialized(logger, total);
        }
    }
}
