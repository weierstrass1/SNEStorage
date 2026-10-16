-- Remove the Seal of Quality report category and its reasons.
-- Single-statement version for Monster.ASP / Adminer. Does not delete existing report history.
-- Execute the complete file in the SNEStorage database.

EXEC sys.sp_executesql N'SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@TRANCOUNT <> 0
    THROW 51000, N''Run this script outside an existing transaction.'', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (
        SELECT 1
        FROM [dbo].[flag] AS report
        INNER JOIN [dbo].[reason] AS reason ON reason.[id] = report.[reason_id]
        INNER JOIN [dbo].[reason_category] AS category ON category.[id] = reason.[reason_category_id]
        WHERE CONVERT(nvarchar(max), category.[name]) = N''Seal of Quality''
    )
        THROW 51005, N''Seal of Quality reasons have existing reports. They are hidden in the frontend; review their history before deleting or reassigning them.'', 1;

    DELETE reason
    FROM [dbo].[reason] AS reason
    INNER JOIN [dbo].[reason_category] AS category ON category.[id] = reason.[reason_category_id]
    WHERE CONVERT(nvarchar(max), category.[name]) = N''Seal of Quality'';
    DECLARE @DeletedReasons int = @@ROWCOUNT;

    DELETE FROM [dbo].[reason_category]
    WHERE CONVERT(nvarchar(max), [name]) = N''Seal of Quality'';
    DECLARE @DeletedCategories int = @@ROWCOUNT;
    COMMIT TRANSACTION;

    SELECT @DeletedReasons AS [DeletedReasons], @DeletedCategories AS [DeletedCategories];
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
';
