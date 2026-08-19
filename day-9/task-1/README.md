# Day 9 - Isolation Levels and Read Anomalies

## What I did

For this task, I used two separate SQL sessions connected to the same database and tested how different isolation levels handle concurrent changes.

I used the `dbo.IndexPractice` table for all the tests.

## Dirty read

I used `READ UNCOMMITTED` in Session 2 while Session 1 had an uncommitted update.

Session 1 changed the amount from `648.91` to `748.91`, but did not commit it. Session 2 was still able to read `748.91`.

Session 1 then rolled back the change, so the original value stayed `648.91`.

This showed a **dirty read**.

**Lowest isolation level that prevents it:** `READ COMMITTED`

## Non-repeatable read

I used `READ COMMITTED` and read the same row twice in Session 1.

While Session 1 was waiting, Session 2 changed the amount from `648.91` to `848.91`.

The first read returned `648.91` and the second read returned `848.91`.

This showed a **non-repeatable read**.

**Lowest isolation level that prevents it:** `REPEATABLE READ`

## Phantom read

I used a range query for rows where `Amount > 900`.

The first query returned `11929` rows. While Session 1 was waiting, Session 2 inserted a new row with an amount of `1500`.

The second query returned `11930` rows.

The new matching row was the phantom read.

I also tested `SERIALIZABLE`. In that test, the insert in Session 2 was blocked until Session 1 finished.

**Lowest isolation level that prevents it:** `SERIALIZABLE`

## Summary

| Anomaly             | Lowest isolation level that prevents it |
| ------------------- | --------------------------------------- |
| Dirty read          | READ COMMITTED                          |
| Non-repeatable read | REPEATABLE READ                         |
| Phantom read        | SERIALIZABLE                            |

The main thing I learned is that higher isolation levels give more consistent reads, but they can also cause more blocking between transactions.
