# Session Log: Transaction Categories — 2026-03-14T09:43 UTC

**Linus (Backend Dev)** fixed HTTP 404 warnings from querying unsupported transaction categories.

- Added `AssetCategory.HasTransactions()` filter extension
- Updated `TransactionsSheet` to skip Crypto, RealEstate, PreciousMetals, Vehicles, Cryptocurrency, Crypto-Wallets, Life-Insurance, Mortgage
- Reduced spurious warnings by ~30 per export
- Build clean, all 134 tests pass
