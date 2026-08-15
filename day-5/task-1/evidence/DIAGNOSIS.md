# Diagnosis note

The slow span was the root `GET /api/quotes` span itself, not one of its
children. In the before-trace, the request span took **~1.57s**
(trace `934ffe4e6f8ad843a46c3d512a59c0e5`), while the nested EF Core `main`
span — the actual SQLite query — took only **630 microseconds**. Over 1.5
seconds were unaccounted for inside the handler, outside any instrumented
child span, which is exactly the signature of a synchronous blocking call
(`Thread.Sleep(1500)`) rather than slow I/O. The fix was removing that call.
After the fix, the same endpoint's root span dropped to **~2.3ms**
(trace `470426934cbbfd02ef4cba333bb9744c`), in line with the EF Core query
time, confirming the artificial delay is gone and the database was never the
bottleneck.
