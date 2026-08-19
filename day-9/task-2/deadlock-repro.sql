-- Day 9 - Task 1
-- Reproduce and resolve a deadlock

-- Setup
-- Run this section once

IF OBJECT_ID('dbo.DeadlockAccounts', 'U') IS NOT NULL
    DROP TABLE dbo.DeadlockAccounts;

CREATE TABLE dbo.DeadlockAccounts
(
    AccountId INT NOT NULL PRIMARY KEY,
    Balance DECIMAL(10,2) NOT NULL
);

INSERT INTO dbo.DeadlockAccounts (AccountId, Balance)
VALUES
    (1, 1000.00),
    (2, 1000.00);

SELECT *
FROM dbo.DeadlockAccounts
ORDER BY AccountId;


-- Deadlock reproduction
-- Run Session 1 and Session 2 in two separate query windows
-- Start both sessions within a few seconds of each other


-- Session 1

BEGIN TRANSACTION;

UPDATE dbo.DeadlockAccounts
SET Balance = Balance - 100
WHERE AccountId = 1;

WAITFOR DELAY '00:00:05';

UPDATE dbo.DeadlockAccounts
SET Balance = Balance + 100
WHERE AccountId = 2;

COMMIT TRANSACTION;


-- Session 2

BEGIN TRANSACTION;

UPDATE dbo.DeadlockAccounts
SET Balance = Balance - 50
WHERE AccountId = 2;

WAITFOR DELAY '00:00:05';

UPDATE dbo.DeadlockAccounts
SET Balance = Balance + 50
WHERE AccountId = 1;

COMMIT TRANSACTION;


-- Check the data after reproducing the deadlock

SELECT *
FROM dbo.DeadlockAccounts
ORDER BY AccountId;


-- Reset the balances before testing the fix

UPDATE dbo.DeadlockAccounts
SET Balance = 1000.00;

SELECT *
FROM dbo.DeadlockAccounts
ORDER BY AccountId;


-- Fixed Session 1
-- AccountId 1 is accessed before AccountId 2

BEGIN TRANSACTION;

UPDATE dbo.DeadlockAccounts
SET Balance = Balance - 100
WHERE AccountId = 1;

WAITFOR DELAY '00:00:05';

UPDATE dbo.DeadlockAccounts
SET Balance = Balance + 100
WHERE AccountId = 2;

COMMIT TRANSACTION;


-- Fixed Session 2
-- AccountId 1 is accessed before AccountId 2

BEGIN TRANSACTION;

UPDATE dbo.DeadlockAccounts
SET Balance = Balance - 50
WHERE AccountId = 1;

WAITFOR DELAY '00:00:05';

UPDATE dbo.DeadlockAccounts
SET Balance = Balance + 50
WHERE AccountId = 2;

COMMIT TRANSACTION;


-- Verify the fix

SELECT *
FROM dbo.DeadlockAccounts
ORDER BY AccountId;