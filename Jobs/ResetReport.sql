USE PKL_APP;
GO

UPDATE StudentValidations
SET  
    isReport = 0, 
    update_weekly = GETDATE()
WHERE  
    AND isReport = 1
    AND (update_weekly IS NULL OR update_weekly < CAST(GETDATE() AS DATE));
GO
