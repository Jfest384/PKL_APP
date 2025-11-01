USE PKL_APP;
GO

UPDATE StudentValidations
SET 
    isLock = 1, 
    isPresence = 0, 
    isDailyReport = 0, 
    update_daily = GETDATE()
WHERE 
    isLock = 0 
    AND isPresence = 1 
    AND isDailyReport = 1
    AND (update_daily IS NULL OR update_daily < CAST(GETDATE() AS DATE));
GO
