IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [Discriminator] nvarchar(21) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [file_type] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NOT NULL,
        [value] text NOT NULL,
        CONSTRAINT [PK_File_Type] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [resource_type] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NULL,
        CONSTRAINT [PK_Resource_Type] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [score] (
        [id] bigint NOT NULL IDENTITY,
        CONSTRAINT [PK_Score] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [videogame] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NOT NULL,
        [release_date] date NULL,
        [publisher] text NULL,
        CONSTRAINT [PK_Videogame] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [file] (
        [id] bigint NOT NULL IDENTITY,
        [url] text NOT NULL,
        [file_type_id] bigint NOT NULL,
        CONSTRAINT [PK_File] PRIMARY KEY ([id]),
        CONSTRAINT [FK_file_file_type] FOREIGN KEY ([file_type_id]) REFERENCES [file_type] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [comment] (
        [id] bigint NOT NULL IDENTITY,
        [publish_date] datetime2 NOT NULL,
        [text] text NOT NULL,
        [user_id] nvarchar(450) NOT NULL,
        [score_id] bigint NOT NULL,
        CONSTRAINT [PK_Comment] PRIMARY KEY ([id]),
        CONSTRAINT [FK_comment_score] FOREIGN KEY ([score_id]) REFERENCES [score] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_comment_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [like_users] (
        [id] bigint NOT NULL IDENTITY,
        [score_id] bigint NOT NULL,
        [user_id] nvarchar(450) NOT NULL,
        [value] bit NOT NULL,
        CONSTRAINT [PK_like_users] PRIMARY KEY ([id]),
        CONSTRAINT [FK_like_users_score] FOREIGN KEY ([score_id]) REFERENCES [score] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_like_users_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [score_users] (
        [id] bigint NOT NULL IDENTITY,
        [score_id] bigint NOT NULL,
        [user_id] nvarchar(450) NOT NULL,
        [value] bigint NOT NULL,
        CONSTRAINT [PK_Score_Users] PRIMARY KEY ([id]),
        CONSTRAINT [FK_score_users_score] FOREIGN KEY ([score_id]) REFERENCES [score] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_score_users_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [resource] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NULL,
        [videogame_id] bigint NULL,
        [resource_type_id] bigint NULL,
        [usage] text NULL,
        [description] text NULL,
        [version] text NULL,
        [downloads] int NULL,
        [file_id] bigint NOT NULL,
        [submitter_user_id] nvarchar(450) NOT NULL,
        [score_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_file] FOREIGN KEY ([file_id]) REFERENCES [file] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_resource_type] FOREIGN KEY ([resource_type_id]) REFERENCES [resource_type] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_score] FOREIGN KEY ([score_id]) REFERENCES [score] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_submitter_user] FOREIGN KEY ([submitter_user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_videogame] FOREIGN KEY ([videogame_id]) REFERENCES [videogame] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE TABLE [resource_comments] (
        [id] bigint NOT NULL IDENTITY,
        [comment_id] bigint NOT NULL,
        [resource_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource_Comments] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_comments_comment] FOREIGN KEY ([comment_id]) REFERENCES [comment] ([id]),
        CONSTRAINT [FK_resource_comments_resource] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_comment_score_id] ON [comment] ([score_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_comment_user_id] ON [comment] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_file_file_type_id] ON [file] ([file_type_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_like_users_score_id] ON [like_users] ([score_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_like_users_user_id] ON [like_users] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_file_id] ON [resource] ([file_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_resource_type_id] ON [resource] ([resource_type_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_score_id] ON [resource] ([score_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_submitter_user_id] ON [resource] ([submitter_user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_videogame_id] ON [resource] ([videogame_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_comments_comment_id] ON [resource_comments] ([comment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_resource_comments_resource_id] ON [resource_comments] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_score_users_score_id] ON [score_users] ([score_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_score_users_user_id] ON [score_users] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240913143913_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240913143913_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE TABLE [time_zone] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NOT NULL,
        [value] int NOT NULL,
        CONSTRAINT [PK_Time_Zone] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE TABLE [visibility] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NULL,
        CONSTRAINT [PK_Visibility] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE TABLE [user_info] (
        [id] bigint NOT NULL IDENTITY,
        [username_color] text NULL,
        [title] text NULL,
        [avatar_id] bigint NOT NULL,
        [birthday] date NOT NULL,
        [bio] text NULL,
        [discord] text NULL,
        [twitter] text NULL,
        [facebook] text NULL,
        [youtube] text NULL,
        [instagram] text NULL,
        [steam] text NULL,
        [twitch] text NULL,
        [github] text NULL,
        [nintendo_network] text NULL,
        [email_visibility_id] bigint NOT NULL,
        [time_zone_id] bigint NOT NULL,
        [user_id] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_User_Info] PRIMARY KEY ([id]),
        CONSTRAINT [FK_user_info_file] FOREIGN KEY ([avatar_id]) REFERENCES [file] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_user_info_time_zone] FOREIGN KEY ([time_zone_id]) REFERENCES [time_zone] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_user_info_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_user_info_visibility] FOREIGN KEY ([email_visibility_id]) REFERENCES [visibility] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE UNIQUE INDEX [IX_user_info_avatar_id] ON [user_info] ([avatar_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE INDEX [IX_user_info_email_visibility_id] ON [user_info] ([email_visibility_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE INDEX [IX_user_info_time_zone_id] ON [user_info] ([time_zone_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    CREATE UNIQUE INDEX [IX_user_info_user_id] ON [user_info] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915004027_update14'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240915004027_update14', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD [includes_gore] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD [includes_politics] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD [includes_porn] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD [includes_sensitive_content] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD [includes_slurs] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD [visibility_id] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE TABLE [reason] (
        [id] bigint NOT NULL IDENTITY,
        [description] text NOT NULL,
        CONSTRAINT [PK_Reason] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE TABLE [team] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NOT NULL,
        CONSTRAINT [PK_Team] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE TABLE [flag] (
        [id] bigint NOT NULL IDENTITY,
        [user_id] nvarchar(450) NOT NULL,
        [reason_id] bigint NOT NULL,
        CONSTRAINT [PK_Flag] PRIMARY KEY ([id]),
        CONSTRAINT [FK_flag_reason] FOREIGN KEY ([reason_id]) REFERENCES [reason] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_flag_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE TABLE [team_users] (
        [id] bigint NOT NULL IDENTITY,
        [user_id] nvarchar(450) NOT NULL,
        [team_id] bigint NOT NULL,
        CONSTRAINT [PK_Team_Users] PRIMARY KEY ([id]),
        CONSTRAINT [FK_team_users_team] FOREIGN KEY ([team_id]) REFERENCES [team] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_team_users_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE TABLE [resource_flags] (
        [id] bigint NOT NULL IDENTITY,
        [flag_id] bigint NOT NULL,
        [resource_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource_Flags] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_flags_flag] FOREIGN KEY ([flag_id]) REFERENCES [flag] ([id]),
        CONSTRAINT [FK_resource_flags_resource] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE INDEX [IX_resource_visibility_id] ON [resource] ([visibility_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE INDEX [IX_flag_reason_id] ON [flag] ([reason_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE INDEX [IX_flag_user_id] ON [flag] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE UNIQUE INDEX [IX_resource_flags_flag_id] ON [resource_flags] ([flag_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE INDEX [IX_resource_flags_resource_id] ON [resource_flags] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE INDEX [IX_team_users_team_id] ON [team_users] ([team_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    CREATE INDEX [IX_team_users_user_id] ON [team_users] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    ALTER TABLE [resource] ADD CONSTRAINT [FK_resource_visibility] FOREIGN KEY ([visibility_id]) REFERENCES [visibility] ([id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915015606_update15'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240915015606_update15', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    ALTER TABLE [team] ADD [created_at] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    ALTER TABLE [resource] ADD [created_at] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    ALTER TABLE [resource] ADD [last_update] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    ALTER TABLE [flag] ADD [date] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE TABLE [resource_authors] (
        [id] bigint NOT NULL IDENTITY,
        [user_id] nvarchar(450) NULL,
        [resource_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource_Authors] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_authors_resource] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_authors_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE TABLE [resource_external_author] (
        [id] bigint NOT NULL IDENTITY,
        [author] text NOT NULL,
        [resource_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource_External_Author] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_external_author_resource] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE TABLE [resource_teams] (
        [id] bigint NOT NULL IDENTITY,
        [team_id] bigint NOT NULL,
        [resource_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource_Teams] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_teams_resource] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_teams_team] FOREIGN KEY ([team_id]) REFERENCES [team] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE INDEX [IX_resource_authors_resource_id] ON [resource_authors] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE INDEX [IX_resource_authors_user_id] ON [resource_authors] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE INDEX [IX_resource_external_author_resource_id] ON [resource_external_author] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE INDEX [IX_resource_teams_resource_id] ON [resource_teams] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    CREATE INDEX [IX_resource_teams_team_id] ON [resource_teams] ([team_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915022219_update16'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240915022219_update16', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [ban] (
        [id] bigint NOT NULL IDENTITY,
        [user_id] nvarchar(450) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [expire_date] datetime2 NOT NULL,
        [is_permanent] bit NOT NULL,
        [reasons] text NOT NULL,
        CONSTRAINT [PK_Ban] PRIMARY KEY ([id]),
        CONSTRAINT [FK_ban_user] FOREIGN KEY ([user_id]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [memory_address_type] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NOT NULL,
        CONSTRAINT [PK_Memory_Address_Type] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [tag] (
        [id] bigint NOT NULL IDENTITY,
        [name] text NOT NULL,
        CONSTRAINT [PK_Tag] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [memory_address] (
        [id] bigint NOT NULL IDENTITY,
        [created_at] datetime2 NOT NULL,
        [last_update] datetime2 NOT NULL,
        [size] bigint NOT NULL,
        [address] text NOT NULL,
        [name] text NOT NULL,
        [description] text NOT NULL,
        [memory_address_type_id] bigint NOT NULL,
        [videogame_id] bigint NOT NULL,
        CONSTRAINT [PK_Memory_Address] PRIMARY KEY ([id]),
        CONSTRAINT [FK_memory_address_memory_address_type] FOREIGN KEY ([memory_address_type_id]) REFERENCES [memory_address_type] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_memory_address_videogame] FOREIGN KEY ([videogame_id]) REFERENCES [videogame] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [resource_tags] (
        [id] bigint NOT NULL IDENTITY,
        [resource_id] bigint NOT NULL,
        [tag_id] bigint NOT NULL,
        CONSTRAINT [PK_Resource_Tags] PRIMARY KEY ([id]),
        CONSTRAINT [FK_resource_tags_resource] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_tags_tag] FOREIGN KEY ([tag_id]) REFERENCES [tag] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [memory_address_flags] (
        [id] bigint NOT NULL IDENTITY,
        [flag_id] bigint NOT NULL,
        [memory_address_id] bigint NOT NULL,
        CONSTRAINT [PK_Memory_Address_Flags] PRIMARY KEY ([id]),
        CONSTRAINT [FK_memory_address_flags_flag] FOREIGN KEY ([flag_id]) REFERENCES [flag] ([id]),
        CONSTRAINT [FK_memory_address_flags_memory_address] FOREIGN KEY ([memory_address_id]) REFERENCES [memory_address] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [memory_address_tags] (
        [id] bigint NOT NULL IDENTITY,
        [memory_address_id] bigint NOT NULL,
        [tag_id] bigint NOT NULL,
        CONSTRAINT [PK_Memory_Address_Tags] PRIMARY KEY ([id]),
        CONSTRAINT [FK_memory_address_tags_memory_address] FOREIGN KEY ([memory_address_id]) REFERENCES [memory_address] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_memory_address_tags_tag] FOREIGN KEY ([tag_id]) REFERENCES [tag] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE TABLE [memory_address_values] (
        [id] bigint NOT NULL IDENTITY,
        [memory_address_id] bigint NOT NULL,
        [value] text NOT NULL,
        [description] text NOT NULL,
        CONSTRAINT [PK_Memory_Address_Values] PRIMARY KEY ([id]),
        CONSTRAINT [FK_memory_address_values_memory_address] FOREIGN KEY ([memory_address_id]) REFERENCES [memory_address] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_ban_user_id] ON [ban] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_memory_address_memory_address_type_id] ON [memory_address] ([memory_address_type_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_memory_address_videogame_id] ON [memory_address] ([videogame_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE UNIQUE INDEX [IX_memory_address_flags_flag_id] ON [memory_address_flags] ([flag_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_memory_address_flags_memory_address_id] ON [memory_address_flags] ([memory_address_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_memory_address_tags_memory_address_id] ON [memory_address_tags] ([memory_address_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_memory_address_tags_tag_id] ON [memory_address_tags] ([tag_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_memory_address_values_memory_address_id] ON [memory_address_values] ([memory_address_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_resource_tags_resource_id] ON [resource_tags] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    CREATE INDEX [IX_resource_tags_tag_id] ON [resource_tags] ([tag_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915032043_update17'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240915032043_update17', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915195326_update18'
)
BEGIN
    ALTER TABLE [user_info] ADD [buy_me_a_coffee] text NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915195326_update18'
)
BEGIN
    ALTER TABLE [user_info] ADD [patreon] text NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915195326_update18'
)
BEGIN
    ALTER TABLE [user_info] ADD [paypal] text NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915195326_update18'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240915195326_update18', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915220401_update19'
)
BEGIN
    ALTER TABLE [visibility] ADD [resource_only] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20240915220401_update19'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240915220401_update19', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001163102_NombreDelCambio'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Discriminator');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [AspNetUsers] DROP COLUMN [Discriminator];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001163102_NombreDelCambio'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001163102_NombreDelCambio', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    ALTER TABLE [resource] DROP CONSTRAINT [FK_resource_file];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    ALTER TABLE [resource] DROP CONSTRAINT [FK_resource_resource_type];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    ALTER TABLE [resource] DROP CONSTRAINT [FK_resource_submitter_user];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DROP INDEX [IX_resource_resource_type_id] ON [resource];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DROP INDEX [IX_resource_submitter_user_id] ON [resource];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'downloads');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [resource] DROP COLUMN [downloads];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'last_update');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [resource] DROP COLUMN [last_update];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'resource_type_id');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [resource] DROP COLUMN [resource_type_id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'submitter_user_id');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [resource] DROP COLUMN [submitter_user_id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'usage');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [resource] DROP COLUMN [usage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'version');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [resource] DROP COLUMN [version];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    EXEC sp_rename N'[resource].[file_id]', N'FileId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    EXEC sp_rename N'[resource].[IX_resource_file_id]', N'IX_resource_FileId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'FileId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [resource] ALTER COLUMN [FileId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    ALTER TABLE [resource] ADD [ApplicationUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE TABLE [resource_resource_types] (
        [Id] bigint NOT NULL IDENTITY,
        [resource_id] bigint NOT NULL,
        [resource_type_id] bigint NOT NULL,
        CONSTRAINT [PK_resource_resource_types] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_resource_resource_types_resource_resource_id] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_resource_types_resource_type_resource_type_id] FOREIGN KEY ([resource_type_id]) REFERENCES [resource_type] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE TABLE [ResourceVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [Version] nvarchar(50) NULL,
        [Downloads] int NOT NULL DEFAULT 0,
        [ResourceId] bigint NOT NULL,
        [FileId] bigint NOT NULL,
        [SubmitterUserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT [PK_ResourceVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceVersions_AspNetUsers_SubmitterUserId] FOREIGN KEY ([SubmitterUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ResourceVersions_file_FileId] FOREIGN KEY ([FileId]) REFERENCES [file] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ResourceVersions_resource_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [resource] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE INDEX [IX_resource_ApplicationUserId] ON [resource] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE INDEX [IX_resource_resource_types_resource_id] ON [resource_resource_types] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE INDEX [IX_resource_resource_types_resource_type_id] ON [resource_resource_types] ([resource_type_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE INDEX [IX_ResourceVersions_FileId] ON [ResourceVersions] ([FileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE INDEX [IX_ResourceVersions_ResourceId] ON [ResourceVersions] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    CREATE INDEX [IX_ResourceVersions_SubmitterUserId] ON [ResourceVersions] ([SubmitterUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    ALTER TABLE [resource] ADD CONSTRAINT [FK_resource_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    ALTER TABLE [resource] ADD CONSTRAINT [FK_resource_file_FileId] FOREIGN KEY ([FileId]) REFERENCES [file] ([id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002213817_AddResource'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002213817_AddResource', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [ResourceVersions] DROP CONSTRAINT [FK_ResourceVersions_AspNetUsers_SubmitterUserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [ResourceVersions] DROP CONSTRAINT [FK_ResourceVersions_file_FileId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [ResourceVersions] DROP CONSTRAINT [FK_ResourceVersions_resource_ResourceId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [ResourceVersions] DROP CONSTRAINT [PK_ResourceVersions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    EXEC sp_rename N'[ResourceVersions]', N'resource_version', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    EXEC sp_rename N'[resource_version].[IX_ResourceVersions_SubmitterUserId]', N'IX_resource_version_SubmitterUserId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    EXEC sp_rename N'[resource_version].[IX_ResourceVersions_ResourceId]', N'IX_resource_version_ResourceId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    EXEC sp_rename N'[resource_version].[IX_ResourceVersions_FileId]', N'IX_resource_version_FileId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [resource_version] ADD CONSTRAINT [PK_resource_version] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [resource_version] ADD CONSTRAINT [FK_resource_version_AspNetUsers_SubmitterUserId] FOREIGN KEY ([SubmitterUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [resource_version] ADD CONSTRAINT [FK_resource_version_file_FileId] FOREIGN KEY ([FileId]) REFERENCES [file] ([id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    ALTER TABLE [resource_version] ADD CONSTRAINT [FK_resource_version_resource_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [resource] ([id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002214330_AddResource2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002214330_AddResource2', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002215958_AddResource3'
)
BEGIN
    EXEC sp_rename N'[resource_version].[Version]', N'version', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002215958_AddResource3'
)
BEGIN
    EXEC sp_rename N'[resource_version].[Downloads]', N'downloads', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002215958_AddResource3'
)
BEGIN
    EXEC sp_rename N'[resource_version].[CreatedAt]', N'created_at', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002215958_AddResource3'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_version]') AND [c].[name] = N'version');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [resource_version] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [resource_version] ALTER COLUMN [version] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002215958_AddResource3'
)
BEGIN
    ALTER TABLE [resource_version] ADD [change_log] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002215958_AddResource3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002215958_AddResource3', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002224630_AddResource4'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[videogame]') AND [c].[name] = N'publisher');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [videogame] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [videogame] ALTER COLUMN [publisher] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002224630_AddResource4'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[videogame]') AND [c].[name] = N'name');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [videogame] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [videogame] ALTER COLUMN [name] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002224630_AddResource4'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_type]') AND [c].[name] = N'name');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [resource_type] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [resource_type] ALTER COLUMN [name] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002224630_AddResource4'
)
BEGIN
    DROP INDEX [IX_resource_authors_user_id] ON [resource_authors];
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_authors]') AND [c].[name] = N'user_id');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [resource_authors] DROP CONSTRAINT ' + @var12 + ';');
    EXEC(N'UPDATE [resource_authors] SET [user_id] = N'''' WHERE [user_id] IS NULL');
    ALTER TABLE [resource_authors] ALTER COLUMN [user_id] nvarchar(450) NOT NULL;
    ALTER TABLE [resource_authors] ADD DEFAULT N'' FOR [user_id];
    CREATE INDEX [IX_resource_authors_user_id] ON [resource_authors] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002224630_AddResource4'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002224630_AddResource4', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002234307_AddResource5'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_external_author]') AND [c].[name] = N'author');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [resource_external_author] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [resource_external_author] ALTER COLUMN [author] nvarchar(200) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002234307_AddResource5'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002234307_AddResource5', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE TABLE [resource_dependency] (
        [Id] bigint NOT NULL IDENTITY,
        [resource_id] bigint NOT NULL,
        [dependency_resource_id] bigint NOT NULL,
        CONSTRAINT [PK_resource_dependency] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_resource_dependency_dependency_resource_id] FOREIGN KEY ([dependency_resource_id]) REFERENCES [resource] ([id]),
        CONSTRAINT [FK_resource_dependency_resource_id] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE TABLE [resource_external_dependency] (
        [Id] bigint NOT NULL IDENTITY,
        [resource_id] bigint NOT NULL,
        [dependency_name] varchar(100) NOT NULL,
        [dependency_url] varchar(255) NOT NULL,
        [mirror_1] varchar(255) NULL,
        [mirror_2] varchar(255) NULL,
        [mirror_3] varchar(255) NULL,
        [mirror_4] varchar(255) NULL,
        CONSTRAINT [PK_resource_external_dependency] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_resource_external_dependency_resource_id] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE TABLE [resource_previews] (
        [Id] bigint NOT NULL IDENTITY,
        [resource_id] bigint NOT NULL,
        [file_id] bigint NOT NULL,
        CONSTRAINT [PK_resource_previews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_resource_previews_file_id] FOREIGN KEY ([file_id]) REFERENCES [file] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_resource_previews_resource_id] FOREIGN KEY ([resource_id]) REFERENCES [resource] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE INDEX [IX_resource_dependency_dependency_resource_id] ON [resource_dependency] ([dependency_resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE INDEX [IX_resource_dependency_resource_id] ON [resource_dependency] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE INDEX [IX_resource_external_dependency_resource_id] ON [resource_external_dependency] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE INDEX [IX_resource_previews_file_id] ON [resource_previews] ([file_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    CREATE INDEX [IX_resource_previews_resource_id] ON [resource_previews] ([resource_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003041418_AddResourceDependencies'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003041418_AddResourceDependencies', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202325_AddCategories'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[file_type]') AND [c].[name] = N'value');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [file_type] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [file_type] ALTER COLUMN [value] varchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202325_AddCategories'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[file_type]') AND [c].[name] = N'name');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [file_type] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [file_type] ALTER COLUMN [name] varchar(10) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202325_AddCategories'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[file]') AND [c].[name] = N'url');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [file] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [file] ALTER COLUMN [url] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202325_AddCategories'
)
BEGIN
    CREATE INDEX [IX_resource_type_category_id] ON [resource_type] ([category_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202325_AddCategories'
)
BEGIN
    ALTER TABLE [resource_type] ADD CONSTRAINT [FK_resource_type_category] FOREIGN KEY ([category_id]) REFERENCES [resource_category] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202325_AddCategories'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005202325_AddCategories', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005202559_AddCategories2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005202559_AddCategories2', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[visibility]') AND [c].[name] = N'name');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [visibility] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [visibility] ALTER COLUMN [name] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'youtube');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [youtube] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'username_color');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [username_color] nvarchar(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'twitter');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [twitter] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'twitch');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [twitch] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'title');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [title] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'steam');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [steam] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'paypal');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [paypal] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'patreon');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [patreon] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'nintendo_network');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [nintendo_network] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'instagram');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [instagram] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'github');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var28 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [github] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'facebook');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [facebook] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'discord');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var30 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [discord] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var31 nvarchar(max);
    SELECT @var31 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'buy_me_a_coffee');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var31 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [buy_me_a_coffee] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var32 nvarchar(max);
    SELECT @var32 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[user_info]') AND [c].[name] = N'bio');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [user_info] DROP CONSTRAINT ' + @var32 + ';');
    ALTER TABLE [user_info] ALTER COLUMN [bio] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var33 nvarchar(max);
    SELECT @var33 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[time_zone]') AND [c].[name] = N'name');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [time_zone] DROP CONSTRAINT ' + @var33 + ';');
    ALTER TABLE [time_zone] ALTER COLUMN [name] nvarchar(10) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var34 nvarchar(max);
    SELECT @var34 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[team]') AND [c].[name] = N'name');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [team] DROP CONSTRAINT ' + @var34 + ';');
    ALTER TABLE [team] ALTER COLUMN [name] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var35 nvarchar(max);
    SELECT @var35 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tag]') AND [c].[name] = N'name');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [tag] DROP CONSTRAINT ' + @var35 + ';');
    ALTER TABLE [tag] ALTER COLUMN [name] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var36 nvarchar(max);
    SELECT @var36 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'name');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var36 + ';');
    ALTER TABLE [resource] ALTER COLUMN [name] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var37 nvarchar(max);
    SELECT @var37 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'description');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var37 + ';');
    ALTER TABLE [resource] ALTER COLUMN [description] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var38 nvarchar(max);
    SELECT @var38 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[reason]') AND [c].[name] = N'description');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [reason] DROP CONSTRAINT ' + @var38 + ';');
    ALTER TABLE [reason] ALTER COLUMN [description] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var39 nvarchar(max);
    SELECT @var39 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[memory_address_values]') AND [c].[name] = N'value');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [memory_address_values] DROP CONSTRAINT ' + @var39 + ';');
    ALTER TABLE [memory_address_values] ALTER COLUMN [value] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var40 nvarchar(max);
    SELECT @var40 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[memory_address_values]') AND [c].[name] = N'description');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [memory_address_values] DROP CONSTRAINT ' + @var40 + ';');
    ALTER TABLE [memory_address_values] ALTER COLUMN [description] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var41 nvarchar(max);
    SELECT @var41 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[memory_address_type]') AND [c].[name] = N'name');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [memory_address_type] DROP CONSTRAINT ' + @var41 + ';');
    ALTER TABLE [memory_address_type] ALTER COLUMN [name] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var42 nvarchar(max);
    SELECT @var42 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[memory_address]') AND [c].[name] = N'name');
    IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [memory_address] DROP CONSTRAINT ' + @var42 + ';');
    ALTER TABLE [memory_address] ALTER COLUMN [name] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var43 nvarchar(max);
    SELECT @var43 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[memory_address]') AND [c].[name] = N'address');
    IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [memory_address] DROP CONSTRAINT ' + @var43 + ';');
    ALTER TABLE [memory_address] ALTER COLUMN [address] nvarchar(15) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var44 nvarchar(max);
    SELECT @var44 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[comment]') AND [c].[name] = N'text');
    IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [comment] DROP CONSTRAINT ' + @var44 + ';');
    ALTER TABLE [comment] ALTER COLUMN [text] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    DECLARE @var45 nvarchar(max);
    SELECT @var45 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ban]') AND [c].[name] = N'reasons');
    IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [ban] DROP CONSTRAINT ' + @var45 + ';');
    ALTER TABLE [ban] ALTER COLUMN [reasons] nvarchar(255) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005204028_fixtexts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005204028_fixtexts', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource_version] ADD [already_moderated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource_version] ADD [seal_of_quality] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource_version] ADD [seal_of_quality_requested] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource_version] ADD [updated_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    DROP INDEX [IX_resource_videogame_id] ON [resource];
    DECLARE @var46 nvarchar(max);
    SELECT @var46 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'videogame_id');
    IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var46 + ';');
    EXEC(N'UPDATE [resource] SET [videogame_id] = CAST(0 AS bigint) WHERE [videogame_id] IS NULL');
    ALTER TABLE [resource] ALTER COLUMN [videogame_id] bigint NOT NULL;
    ALTER TABLE [resource] ADD DEFAULT CAST(0 AS bigint) FOR [videogame_id];
    CREATE INDEX [IX_resource_videogame_id] ON [resource] ([videogame_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    DECLARE @var47 nvarchar(max);
    SELECT @var47 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'name');
    IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var47 + ';');
    EXEC(N'UPDATE [resource] SET [name] = N'''' WHERE [name] IS NULL');
    ALTER TABLE [resource] ALTER COLUMN [name] nvarchar(50) NOT NULL;
    ALTER TABLE [resource] ADD DEFAULT N'' FOR [name];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    DECLARE @var48 nvarchar(max);
    SELECT @var48 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'description');
    IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var48 + ';');
    EXEC(N'UPDATE [resource] SET [description] = N'''' WHERE [description] IS NULL');
    ALTER TABLE [resource] ALTER COLUMN [description] nvarchar(max) NOT NULL;
    ALTER TABLE [resource] ADD DEFAULT N'' FOR [description];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource] ADD [already_moderated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource] ADD [repository_url] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    ALTER TABLE [resource] ADD [updated_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006162324_resourceUpdate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006162324_resourceUpdate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    ALTER TABLE [resource] DROP CONSTRAINT [FK_resource_AspNetUsers_ApplicationUserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    DROP INDEX [IX_resource_ApplicationUserId] ON [resource];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    DECLARE @var49 nvarchar(max);
    SELECT @var49 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'ApplicationUserId');
    IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var49 + ';');
    ALTER TABLE [resource] DROP COLUMN [ApplicationUserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    ALTER TABLE [resource] ADD [submitter_id] nvarchar(450) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    CREATE INDEX [IX_resource_submitter_id] ON [resource] ([submitter_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    ALTER TABLE [resource] ADD CONSTRAINT [FK_resource_submitter] FOREIGN KEY ([submitter_id]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006235532_resourceUpdate2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006235532_resourceUpdate2', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007035805_commentUpdate'
)
BEGIN
    ALTER TABLE [comment] ADD [Deleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007035805_commentUpdate'
)
BEGIN
    ALTER TABLE [comment] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007035805_commentUpdate'
)
BEGIN
    CREATE TABLE [comment_comments] (
        [Id] bigint NOT NULL IDENTITY,
        [child_parent_id] bigint NOT NULL,
        [child_comment_id] bigint NOT NULL,
        CONSTRAINT [id] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_child_comment_comment] FOREIGN KEY ([child_comment_id]) REFERENCES [comment] ([id]),
        CONSTRAINT [FK_parent_comment_comment] FOREIGN KEY ([child_parent_id]) REFERENCES [comment] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007035805_commentUpdate'
)
BEGIN
    CREATE INDEX [IX_comment_comments_child_comment_id] ON [comment_comments] ([child_comment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007035805_commentUpdate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_comment_comments_child_parent_id] ON [comment_comments] ([child_parent_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007035805_commentUpdate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007035805_commentUpdate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007165433_FixCascade'
)
BEGIN
    DROP INDEX [IX_comment_comments_child_comment_id] ON [comment_comments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007165433_FixCascade'
)
BEGIN
    DROP INDEX [IX_comment_comments_child_parent_id] ON [comment_comments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007165433_FixCascade'
)
BEGIN
    CREATE UNIQUE INDEX [IX_comment_comments_child_comment_id] ON [comment_comments] ([child_comment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007165433_FixCascade'
)
BEGIN
    CREATE INDEX [IX_comment_comments_child_parent_id] ON [comment_comments] ([child_parent_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007165433_FixCascade'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007165433_FixCascade', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007180902_CommentCardinality'
)
BEGIN
    DROP INDEX [IX_resource_comments_comment_id] ON [resource_comments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007180902_CommentCardinality'
)
BEGIN
    CREATE UNIQUE INDEX [IX_resource_comments_comment_id] ON [resource_comments] ([comment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007180902_CommentCardinality'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007180902_CommentCardinality', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DROP INDEX [IX_score_users_score_id] ON [score_users];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DROP INDEX [IX_resource_score_id] ON [resource];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DROP INDEX [IX_comment_score_id] ON [comment];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DECLARE @var50 nvarchar(max);
    SELECT @var50 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_version]') AND [c].[name] = N'version');
    IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [resource_version] DROP CONSTRAINT ' + @var50 + ';');
    ALTER TABLE [resource_version] ALTER COLUMN [version] nvarchar(25) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DECLARE @var51 nvarchar(max);
    SELECT @var51 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_version]') AND [c].[name] = N'change_log');
    IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [resource_version] DROP CONSTRAINT ' + @var51 + ';');
    ALTER TABLE [resource_version] ALTER COLUMN [change_log] nvarchar(2048) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DECLARE @var52 nvarchar(max);
    SELECT @var52 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'repository_url');
    IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var52 + ';');
    ALTER TABLE [resource] ALTER COLUMN [repository_url] nvarchar(2048) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    DECLARE @var53 nvarchar(max);
    SELECT @var53 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource]') AND [c].[name] = N'name');
    IF @var53 IS NOT NULL EXEC(N'ALTER TABLE [resource] DROP CONSTRAINT ' + @var53 + ';');
    ALTER TABLE [resource] ALTER COLUMN [name] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    CREATE UNIQUE INDEX [IX_score_users_score_id_user_id] ON [score_users] ([score_id], [user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    CREATE UNIQUE INDEX [IX_resource_score_id] ON [resource] ([score_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    CREATE UNIQUE INDEX [IX_comment_score_id] ON [comment] ([score_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011022654_FixLimits'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011022654_FixLimits', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011215745_News'
)
BEGIN
    DECLARE @var54 nvarchar(max);
    SELECT @var54 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_version]') AND [c].[name] = N'change_log');
    IF @var54 IS NOT NULL EXEC(N'ALTER TABLE [resource_version] DROP CONSTRAINT ' + @var54 + ';');
    ALTER TABLE [resource_version] ALTER COLUMN [change_log] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011215745_News'
)
BEGIN
    CREATE TABLE [new] (
        [id] bigint NOT NULL IDENTITY,
        [poster_user_id] nvarchar(450) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [text] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_New] PRIMARY KEY ([id]),
        CONSTRAINT [FK_new_poster_user] FOREIGN KEY ([poster_user_id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011215745_News'
)
BEGIN
    CREATE INDEX [IX_new_poster_user_id] ON [new] ([poster_user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011215745_News'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011215745_News', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012000440_News2'
)
BEGIN
    ALTER TABLE [new] ADD [title] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012000440_News2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012000440_News2', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012144558_Discord'
)
BEGIN
    ALTER TABLE [user_info] ADD [discord_linked_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012144558_Discord'
)
BEGIN
    ALTER TABLE [user_info] ADD [discord_user_id] varchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012144558_Discord'
)
BEGIN
    ALTER TABLE [user_info] ADD [discord_username] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012144558_Discord'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_user_info_discord_user_id] ON [user_info] ([discord_user_id]) WHERE [discord_user_id] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012144558_Discord'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012144558_Discord', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014155035_AddUniqueCommentLike'
)
BEGIN
    DROP INDEX [IX_like_users_score_id] ON [like_users];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014155035_AddUniqueCommentLike'
)
BEGIN
    DECLARE @var55 nvarchar(max);
    SELECT @var55 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[visibility]') AND [c].[name] = N'name');
    IF @var55 IS NOT NULL EXEC(N'ALTER TABLE [visibility] DROP CONSTRAINT ' + @var55 + ';');
    EXEC(N'UPDATE [visibility] SET [name] = N'''' WHERE [name] IS NULL');
    ALTER TABLE [visibility] ALTER COLUMN [name] nvarchar(100) NOT NULL;
    ALTER TABLE [visibility] ADD DEFAULT N'' FOR [name];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014155035_AddUniqueCommentLike'
)
BEGIN
    DECLARE @var56 nvarchar(max);
    SELECT @var56 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[resource_type]') AND [c].[name] = N'name');
    IF @var56 IS NOT NULL EXEC(N'ALTER TABLE [resource_type] DROP CONSTRAINT ' + @var56 + ';');
    EXEC(N'UPDATE [resource_type] SET [name] = N'''' WHERE [name] IS NULL');
    ALTER TABLE [resource_type] ALTER COLUMN [name] nvarchar(max) NOT NULL;
    ALTER TABLE [resource_type] ADD DEFAULT N'' FOR [name];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014155035_AddUniqueCommentLike'
)
BEGIN
    CREATE UNIQUE INDEX [IX_like_users_score_id_user_id] ON [like_users] ([score_id], [user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014155035_AddUniqueCommentLike'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014155035_AddUniqueCommentLike', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014162548_AddScoreCleanupTriggers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014162548_AddScoreCleanupTriggers', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    ALTER TABLE [comment] DROP CONSTRAINT [FK_comment_score];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    ALTER TABLE [resource] DROP CONSTRAINT [FK_resource_score];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    ALTER TABLE [comment] ADD CONSTRAINT [FK_comment_score] FOREIGN KEY ([score_id]) REFERENCES [score] ([id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    ALTER TABLE [resource] ADD CONSTRAINT [FK_resource_score] FOREIGN KEY ([score_id]) REFERENCES [score] ([id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    EXEC(N'
        CREATE OR ALTER TRIGGER [dbo].[TR_comment_delete_score]
        ON [dbo].[comment]
        AFTER DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF NOT EXISTS (SELECT 1 FROM deleted)
                RETURN;

            DELETE s
            FROM [dbo].[score] AS s
            INNER JOIN deleted AS d ON d.[score_id] = s.[id]
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[comment] AS c
                WHERE c.[score_id] = s.[id]
            )
            AND NOT EXISTS (
                SELECT 1 FROM [dbo].[resource] AS r
                WHERE r.[score_id] = s.[id]
            );
        END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    EXEC(N'
        CREATE OR ALTER TRIGGER [dbo].[TR_resource_delete_score]
        ON [dbo].[resource]
        AFTER DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF NOT EXISTS (SELECT 1 FROM deleted)
                RETURN;

            DELETE s
            FROM [dbo].[score] AS s
            INNER JOIN deleted AS d ON d.[score_id] = s.[id]
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[comment] AS c
                WHERE c.[score_id] = s.[id]
            )
            AND NOT EXISTS (
                SELECT 1 FROM [dbo].[resource] AS r
                WHERE r.[score_id] = s.[id]
            );
        END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014165115_ConfigureScoreCleanupTriggers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014165115_ConfigureScoreCleanupTriggers', N'10.0.13');
END;

COMMIT;
GO

