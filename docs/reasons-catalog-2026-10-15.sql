-- SNEStorage: report reasons based on site-rules.json and seal-of-quality.json.
-- Catalog: 5 categories and 20 reasons. Seal of Quality reporting was removed.
-- Run in the SNEStorage database AFTER the ReasonCategory2 schema migration.
-- Inserts missing rows only. Existing IDs, priorities, descriptions and flags are preserved.
-- Priorities: 5 urgent, 4 high, 3 medium, 2 normal, 1 low. These are NOT automatic penalties.
-- RuleIds and AppliesTo below document the proposal; they are NOT new database columns.
-- No USE statement: select the correct database in your SQL editor before running.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF @@TRANCOUNT <> 0
    THROW 51000, N'Run this script outside an existing transaction.', 1;

IF OBJECT_ID(N'dbo.reason_category', N'U') IS NULL
    OR OBJECT_ID(N'dbo.reason', N'U') IS NULL
    THROW 51001, N'Apply the reason/category schema migrations before running this script.', 1;

IF COL_LENGTH(N'dbo.reason_category', N'id') IS NULL
    OR COL_LENGTH(N'dbo.reason_category', N'name') IS NULL
    OR COL_LENGTH(N'dbo.reason', N'id') IS NULL
    OR COL_LENGTH(N'dbo.reason', N'description') IS NULL
    OR COL_LENGTH(N'dbo.reason', N'priority') IS NULL
    OR COL_LENGTH(N'dbo.reason', N'reason_category_id') IS NULL
    THROW 51002, N'The reason/category schema does not match the current models.', 1;

DECLARE @Categories TABLE ([Name] nvarchar(120) NOT NULL PRIMARY KEY);
INSERT INTO @Categories ([Name]) VALUES
    (N'Safety and distribution'),
    (N'Conduct and discussions'),
    (N'Resource content and previews'),
    (N'Authorship and resource information'),
    (N'Dependencies and visibility');

DECLARE @Reasons TABLE (
    [Number] int NOT NULL PRIMARY KEY,
    [CategoryName] nvarchar(120) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Priority] int NOT NULL CHECK ([Priority] BETWEEN 1 AND 5),
    [RuleIds] nvarchar(200) NOT NULL,
    [AppliesTo] nvarchar(40) NOT NULL
);

INSERT INTO @Reasons ([Number], [CategoryName], [Description], [Priority], [RuleIds], [AppliesTo]) VALUES
    (1, N'Safety and distribution', N'Suspected illegal material.', 5, N'illegal-material', N'Resources and comments'),
    (2, N'Safety and distribution', N'Malicious or phishing links.', 5, N'safe-links', N'Resources and comments'),
    (3, N'Safety and distribution', N'A ROM is distributed instead of a BPS or IPS patch, excluding non-infringing homebrew.', 4, N'rom-uploads', N'Resources'),
    (4, N'Safety and distribution', N'Someone else''s work is distributed without permission.', 4, N'redistribution-permission', N'Resources'),
    (5, N'Safety and distribution', N'Suspicious or unrelated links.', 2, N'safe-links, comment-spam', N'Resources and comments'),

    (6, N'Conduct and discussions', N'Harassment or intimidation of another user.', 4, N'no-harassment', N'Resources and comments'),
    (7, N'Conduct and discussions', N'Language deliberately used to provoke, insult, or upset others.', 3, N'language-in-context', N'Resources and comments'),
    (8, N'Conduct and discussions', N'Spam, repeated messages, or unsolicited promotion.', 2, N'comment-spam', N'Comments'),
    (9, N'Conduct and discussions', N'Political, religious, or other sensitive discussion unrelated to the context.', 2, N'sensitive-discussions', N'Comments'),
    (10, N'Conduct and discussions', N'Vague, nonsensical, or unrelated comment.', 1, N'meaningful-comments, comment-spam', N'Comments'),

    (11, N'Resource content and previews', N'The uploaded files do not match the resource description.', 3, N'accurate-uploads', N'Resources'),
    (12, N'Resource content and previews', N'Required sensitive-content warnings are missing or incorrect.', 3, N'content-warnings', N'Resources'),
    (13, N'Resource content and previews', N'The resource is unrelated to the SNES.', 2, N'snes-material', N'Resources'),
    (14, N'Resource content and previews', N'Previews contain unrelated or inappropriate material.', 2, N'relevant-previews', N'Resources'),

    (15, N'Authorship and resource information', N'An author or contributor is missing from the credits.', 3, N'author-credits', N'Resources'),
    (16, N'Authorship and resource information', N'The videogame, resource types, or categories are incorrect.', 2, N'accurate-classification', N'Resources'),
    (17, N'Authorship and resource information', N'Required dependencies are missing or incorrectly specified.', 2, N'precise-information, quality-complete-information', N'Resources'),
    (18, N'Authorship and resource information', N'The resource information is vague, incomplete, or inaccurate.', 1, N'precise-information', N'Resources'),

    (19, N'Dependencies and visibility', N'A dependency is private or became inaccessible after a visibility change.', 3, N'dependency-visibility', N'Resources'),
    (20, N'Dependencies and visibility', N'The resource has no remaining versions.', 3, N'minimum-versions', N'Resources');


DECLARE @InsertedCategories int = 0, @InsertedReasons int = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Names are lookup values here, not assumed identity IDs.
    IF EXISTS (
        SELECT 1
        FROM @Categories AS wanted
        WHERE (
            SELECT COUNT_BIG(*)
            FROM [dbo].[reason_category] AS existing WITH (UPDLOCK, HOLDLOCK)
            WHERE CONVERT(nvarchar(max), existing.[name]) = wanted.[Name]
        ) > 1
    )
        THROW 51003, N'A proposed category name already occurs more than once. Resolve the ambiguity before importing.', 1;

    INSERT INTO [dbo].[reason_category] ([name])
    SELECT wanted.[Name]
    FROM @Categories AS wanted
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[reason_category] AS existing WITH (UPDLOCK, HOLDLOCK)
        WHERE CONVERT(nvarchar(max), existing.[name]) = wanted.[Name]
    );
    SET @InsertedCategories = @@ROWCOUNT;

    IF EXISTS (
        SELECT 1
        FROM @Reasons AS wanted
        INNER JOIN [dbo].[reason_category] AS category
            ON CONVERT(nvarchar(max), category.[name]) = wanted.[CategoryName]
        WHERE (
            SELECT COUNT_BIG(*)
            FROM [dbo].[reason] AS existing WITH (UPDLOCK, HOLDLOCK)
            WHERE existing.[reason_category_id] = category.[id]
                AND CONVERT(nvarchar(max), existing.[description]) = wanted.[Description]
        ) > 1
    )
        THROW 51004, N'A proposed reason already occurs more than once in its category. Resolve the ambiguity before importing.', 1;

    INSERT INTO [dbo].[reason] ([description], [priority], [reason_category_id])
    SELECT wanted.[Description], wanted.[Priority], category.[id]
    FROM @Reasons AS wanted
    INNER JOIN [dbo].[reason_category] AS category
        ON CONVERT(nvarchar(max), category.[name]) = wanted.[CategoryName]
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[reason] AS existing WITH (UPDLOCK, HOLDLOCK)
        WHERE existing.[reason_category_id] = category.[id]
            AND CONVERT(nvarchar(max), existing.[description]) = wanted.[Description]
    );
    SET @InsertedReasons = @@ROWCOUNT;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT @InsertedCategories AS [InsertedCategories], @InsertedReasons AS [InsertedReasons];

-- Review the actual IDs and stored priorities. Existing reasons are not overwritten.
SELECT category.[id] AS [CategoryId], category.[name] AS [Category],
    reason.[id] AS [ReasonId], reason.[description] AS [Description],
    reason.[priority] AS [StoredPriority], wanted.[Priority] AS [ProposedPriority],
    wanted.[AppliesTo], wanted.[RuleIds]
FROM @Reasons AS wanted
INNER JOIN [dbo].[reason_category] AS category
    ON CONVERT(nvarchar(max), category.[name]) = wanted.[CategoryName]
INNER JOIN [dbo].[reason] AS reason
    ON reason.[reason_category_id] = category.[id]
    AND CONVERT(nvarchar(max), reason.[description]) = wanted.[Description]
ORDER BY wanted.[Priority] DESC, category.[name], wanted.[Number];
