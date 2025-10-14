USE PKL_APP;
UPDATE Students
SET isLock = 1, update_at = GETDATE()
WHERE isLock = 0 AND CAST(update_at AS DATE) < CAST(GETDATE() AS DATE);