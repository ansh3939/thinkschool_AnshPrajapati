-- Day 9 - Isolation levels and read anomalies
-- Table used: dbo.IndexPractice
--
-- These tests use two separate SQL sessions.
-- Run Session 2 while Session 1 is still inside the WAITFOR delay.


-- Check the two sessions

-- Run this in Session 1

SELECT @@SPID AS SessionId;


-- Run this in Session 2

SELECT @@SPID AS SessionId;


-- Check the database isolation settings

SELECT
    name,
    is_read_committed_snapshot_on,
    snapshot_isolation_state_desc
FROM sys.databases
WHERE name = DB_NAME();


-- Check the starting value used for the dirty read
-- and non-repeatable read tests

SELECT
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;


-- Dirty read

-- Session 1
-- Change the amount but leave the transaction open.

SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

BEGIN TRANSACTION;

UPDATE dbo.IndexPractice
SET Amount = 748.91
WHERE Id = 1;

SELECT
    @@SPID AS SessionId,
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;

WAITFOR DELAY '00:01:00';

ROLLBACK;


-- Session 2
-- Run this while Session 1 is waiting.
-- READ UNCOMMITTED can read the uncommitted value.

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    @@SPID AS SessionId,
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;

-- Result observed:
-- Original amount = 648.91
-- Uncommitted amount = 748.91
-- Session 2 was able to read 748.91 before Session 1 rolled back.
-- This is a dirty read.


-- Check that the rollback restored the original value

SELECT
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;


-- Non-repeatable read

-- Session 1
-- Read the same row twice in one transaction.

SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

BEGIN TRANSACTION;

SELECT
    @@SPID AS SessionId,
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;

WAITFOR DELAY '00:01:00';

SELECT
    @@SPID AS SessionId,
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;

COMMIT;


-- Session 2
-- Run this while Session 1 is waiting.

UPDATE dbo.IndexPractice
SET Amount = 848.91
WHERE Id = 1;

COMMIT;

-- Result observed:
-- First read = 648.91
-- Second read = 848.91
-- The same row returned a different value.
-- This is a non-repeatable read.


-- Restore the original value

UPDATE dbo.IndexPractice
SET Amount = 648.91
WHERE Id = 1;

SELECT
    Id,
    CustomerId,
    Status,
    Amount
FROM dbo.IndexPractice
WHERE Id = 1;


-- Phantom read setup

-- Check the number of rows that currently match the range.

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;

-- Check the current highest Id.
-- Id is an IDENTITY column, so we do not insert an Id manually.

SELECT MAX(Id) AS MaxId
FROM dbo.IndexPractice;


-- Phantom read

-- Session 1
-- Run the same range query twice.

SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

BEGIN TRANSACTION;

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;

WAITFOR DELAY '00:01:00';

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;

COMMIT;


-- Session 2
-- Run this while Session 1 is waiting.
-- The new row matches the same Amount > 900 condition.

INSERT INTO dbo.IndexPractice
(
    CustomerId,
    Status,
    OrderDate,
    Amount
)
VALUES
(
    9999,
    'Pending',
    '2026-08-19',
    1500.00
);

-- Result observed:
-- First count = 11929
-- Second count = 11930
-- The extra row appeared in the second query.
-- This is a phantom read.


-- Remove the temporary row

DELETE FROM dbo.IndexPractice
WHERE CustomerId = 9999
  AND Status = 'Pending'
  AND OrderDate = '2026-08-19'
  AND Amount = 1500.00;


-- Check that the original count is back

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;


-- SERIALIZABLE prevents the phantom

-- Session 1
-- SERIALIZABLE protects the range while the transaction is open.

SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRANSACTION;

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;

WAITFOR DELAY '00:01:00';

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;

COMMIT;


-- Session 2
-- Run this while Session 1 is waiting.
-- The insert is blocked until Session 1 finishes.

INSERT INTO dbo.IndexPractice
(
    CustomerId,
    Status,
    OrderDate,
    Amount
)
VALUES
(
    9999,
    'Pending',
    '2026-08-19',
    1500.00
);


-- Remove the temporary row after the test

DELETE FROM dbo.IndexPractice
WHERE CustomerId = 9999
  AND Status = 'Pending'
  AND OrderDate = '2026-08-19'
  AND Amount = 1500.00;


-- Final check that the temporary row is gone

SELECT COUNT(*) AS MatchingRows
FROM dbo.IndexPractice
WHERE Amount > 900;


-- Lowest isolation level that prevents each anomaly
--
-- Dirty read          -> READ COMMITTED
-- Non-repeatable read -> REPEATABLE READ
-- Phantom read        -> SERIALIZABLE