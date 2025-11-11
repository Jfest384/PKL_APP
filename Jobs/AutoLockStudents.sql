USE PKL_APP;
GO

UPDATE StudentValidations
SET 
    isLock = 1,
    update_daily = GETDATE()
WHERE 
    isLock = 0
    AND (update_daily IS NULL OR update_daily < CAST(GETDATE() AS DATE));

UPDATE StudentValidations
SET 
    isPresence = 0,
    update_daily = GETDATE()
WHERE 
    isPresence = 1
    AND (update_daily IS NULL OR update_daily < CAST(GETDATE() AS DATE));

UPDATE StudentValidations
SET 
    isDailyReport = 0,
    update_daily = GETDATE()
WHERE 
    isDailyReport = 1
    AND (update_daily IS NULL OR update_daily < CAST(GETDATE() AS DATE));
GO
