# DistroFlow: MVP Documentation

**Document type:** Product and technical MVP specification
**Status:** Initial scope
**Product:** DistroFlow, a B2B ordering and credit-control system for small distributors
**Primary goal:** Build a credible, working business application that demonstrates practical C# and ASP.NET Core skills (concurrency, transactions, rule engines, state machines).

---

## 1. Product Overview

DistroFlow lets a distributor (FMCG, hardware, pharma, stationery) take orders from its retailers in one place. It applies each retailer's price list and schemes, checks the retailer's credit limit against outstanding balance, reserves stock safely under concurrent orders, and tracks each order through dispatch, invoicing and payment.

### Problem

Small distributors often take orders by phone, WhatsApp or salesmen's notebooks. This leads to:

- Pricing and scheme errors
- Orders accepted for stock that is already gone
- Credit limits exceeded without anyone noticing
- No clear history of who changed or approved what

### Proposed MVP solution

One workflow connecting:

1. Product, price list and scheme setup
2. Retailer management with credit limits
3. Order placement with server-side pricing
4. Credit-limit check with a hold-for-approval path
5. Safe stock reservation under concurrency
6. Order lifecycle (confirm, pick, dispatch, deliver, cancel)
7. Invoice generation and outstanding balance tracking
8. Payment recording
9. Audit history

The MVP supports operational tracking. It does not move real money, provide accounting advice, or replace a full accounting system.

---

## 2. Goals and Non-Goals

### Goals

- Deliver an end-to-end order-to-payment workflow.
- Demonstrate sound C# and ASP.NET Core implementation.
- Prove correct stock reservation under concurrent orders with an automated test.
- Use a relational database with constraints and transactional consistency.
- Enforce authorization on the server.
- Include meaningful unit and integration tests and run instructions.
- Stay small enough to finish in about 6 weeks and explain in 10 minutes.

### Non-Goals (first MVP)

- WhatsApp ordering or notification integration
- Online payment gateway integration
- Full multi-tenant isolation for multiple customer companies
- Full accounting ledger, GST filing or e-invoicing
- Multi-warehouse stock transfers and batch/expiry tracking
- Mobile apps, salesman route planning, GPS tracking
- Microservices, Kubernetes, message brokers
- Complex promotion stacking (only two scheme types in the MVP)

---

## 3. Users and Roles

| Role | Main permissions |
|---|---|
| Administrator | Manage users, roles, products, price lists, schemes, retailers, credit limits |
| Retailer | Browse catalog (own prices), place and cancel own orders, view own orders and outstanding |
| Sales / Dispatch Staff | Confirm orders, pick, dispatch, mark delivered, cancel orders |
| Approver | Approve or reject orders on credit hold, record overrides with a reason |
| Accounts Staff | Record payments, view invoices and outstanding reports |

- One user may hold multiple roles.
- Permissions are enforced on the API. Hiding buttons in the UI is not authorization.
- A retailer user can only access records belonging to their own retailer account.

---

## 4. MVP Modules

### 4.1 Authentication and Access Control

- Sign in / sign out using ASP.NET Core Identity with JWT (or cookie) authentication.
- Role- and policy-based endpoint authorization.
- Consistent 401 (unauthenticated) and 403 (forbidden) responses.

**Acceptance criteria**
- Unauthenticated requests cannot reach protected endpoints.
- A retailer cannot view another retailer's orders.
- A user without the Approver role cannot release a held order.

### 4.2 Product and Stock Management

- Product: SKU, name, unit of measure, active flag.
- Stock: on-hand quantity and reserved quantity per product (single warehouse in the MVP).
- Available quantity = on-hand minus reserved. It is derived, not stored independently.
- Stock adjustments are recorded as movements (receipt, adjustment, reservation, release, dispatch) rather than silent overwrites.

### 4.3 Price Lists

- Named price lists (for example "Retail A", "Retail B").
- Each price list holds a price per product.
- Each retailer is assigned exactly one price list.
- The price used on an order line is **snapshotted** on the line. Later price changes must not alter existing orders.

### 4.4 Schemes (Promotions)

The MVP supports two scheme types:

1. **Free-quantity scheme:** buy N, get M free (for example buy 10, get 1 free).
2. **Volume discount:** percentage discount at or above a quantity threshold.

Rules:
- A scheme has a product, validity dates, and an active flag.
- At most one scheme applies per order line (deterministic; the best benefit for the retailer wins, and ties are broken by lowest scheme ID).
- The applied scheme is recorded on the order line for auditability.

### 4.5 Retailer Management

- Retailer: name, code, contact details, price list, credit limit, active flag.
- Retailer code is unique.
- Inactive retailers cannot place orders.

### 4.6 Order Placement

- Retailer submits product IDs and quantities only.
- The **server** determines price, scheme benefits, line totals and order total. Client-supplied amounts are ignored.
- Order lines record: product, ordered quantity, free quantity, unit price (snapshot), discount, line total, applied scheme.
- Order totals use `decimal`, with the currency precision and rounding rule documented (2 decimal places, round half away from zero, applied per line).

### 4.7 Credit Check

On submit:

```
Outstanding balance + new order total <= credit limit  → continue to stock reservation
Otherwise                                              → order status = OnHold (no stock reserved)
```

- Outstanding balance = sum of unpaid invoice amounts, minus recorded payments.
- An Approver may release a held order with a recorded reason, which then proceeds to stock reservation.
- The credit decision (values used and result) is stored with the order for reproducibility.

### 4.8 Stock Reservation (core technical feature)

- Performed inside a single database transaction with the order creation.
- For each line, reserve `ordered quantity + free quantity`.
- If any line lacks available stock, the whole order fails with a clear conflict response and nothing is reserved.
- Concurrent orders for the same product must never over-reserve.

Chosen strategy (decided and documented in the README):
- Conditional atomic update, for example `UPDATE Stock SET Reserved = Reserved + @qty WHERE ProductId = @id AND OnHand - Reserved >= @qty`, checking the affected row count; **or**
- A concurrency token (`rowversion`) with retry on `DbUpdateConcurrencyException`.

### 4.9 Order Lifecycle

```
Placed → Confirmed → Picked → Dispatched → Delivered
   │          │
   ├── OnHold ─┴─→ (released by Approver) → Placed/Confirmed
   └── Cancelled   (allowed before Dispatched)
```

| From | To | Who | Effect |
|---|---|---|---|
| (new) | Placed | Retailer / Staff | Stock reserved |
| (new) | OnHold | System | Credit limit exceeded, no reservation |
| OnHold | Placed | Approver | Reason required; reservation attempted |
| OnHold | Cancelled | Approver / Retailer | No stock effect |
| Placed | Confirmed | Staff | None |
| Confirmed | Picked | Staff | None |
| Picked | Dispatched | Staff | Reserved stock converted to a permanent deduction; invoice generated |
| Dispatched | Delivered | Staff | None |
| Placed / Confirmed / Picked | Cancelled | Staff / Retailer (own, before Confirmed) | Reserved stock released |

- Allowed transitions are defined in the backend. Arbitrary status updates are rejected.
- Cancelling after dispatch is not allowed in the MVP (a returns/credit-note process is out of scope).

### 4.10 Invoice and Outstanding

- Dispatch generates one invoice per order (amounts copied from the order lines).
- Invoice number is sequential and unique, with a documented format.
- Outstanding balance per retailer = sum of invoice totals minus sum of payments.
- The retailer's available credit = credit limit minus outstanding balance.

### 4.11 Payment Recording

- Record payment amount, date, reference and method label (cash, UPI, cheque, bank transfer).
- Support partial payments, allocated to a specific invoice.
- A payment cannot exceed the invoice's outstanding amount.
- Invoice is marked Paid only when its outstanding amount reaches zero.
- The MVP records payments only. It does not initiate them.

### 4.12 Audit and Reporting

**Audit entry** contains: actor, timestamp, entity, entity ID, action, and before/after values or a safe change summary. Passwords and tokens are never logged.

Audited actions: order placement, hold, release (with reason), confirmation, dispatch, cancellation, stock adjustments, price list and scheme changes, credit limit changes, payments.

**Basic reports**
- Pending orders (Placed / Confirmed / Picked)
- Orders on credit hold
- Low stock (available quantity below a threshold)
- Retailer outstanding and ageing (0–30, 31–60, 60+ days)
- Scheme usage (times applied, free quantity given)

---

## 5. Core Business Rules

1. All monetary and quantity calculations are done server-side using `decimal`.
2. Price and scheme benefits are snapshotted on the order line at placement time.
3. Stock can never be reserved beyond available quantity, even under concurrent requests.
4. Reservation and order creation happen in one transaction.
5. An order on credit hold holds no stock.
6. Order status transitions are validated on the server.
7. Dispatch converts reservation into a permanent deduction and generates the invoice in one transaction.
8. Cancelling an order before dispatch releases its reserved stock exactly once (idempotent).
9. Manual overrides (credit release, stock adjustments) require a reason and are audited.
10. Concurrent edits must not silently overwrite each other. Return a clear 409 Conflict.
11. Payments cannot exceed the invoice's outstanding amount.
12. Critical rules are reinforced by database constraints (unique codes, non-negative stock, foreign keys), not only by application validation.

---

## 6. Technical Architecture

### Stack

| Concern | Choice |
|---|---|
| Backend | C# 12 / .NET 8, ASP.NET Core Web API |
| Data access | Entity Framework Core (with raw SQL only where justified, such as the atomic stock update) |
| Database | SQL Server (PostgreSQL is acceptable if you can explain the differences) |
| Auth | ASP.NET Core Identity + JWT |
| Validation | FluentValidation or data annotations |
| Background jobs | Hangfire (low-stock alerts, nightly outstanding/ageing refresh) |
| Frontend | React + TypeScript (minimal screens) |
| API docs | OpenAPI / Swagger |
| Testing | xUnit, ASP.NET Core integration tests, Testcontainers (real database) |
| Logging | Serilog (structured) |
| Deployment | Docker, docker-compose; CI via GitHub Actions |

### Architecture style

Modular monolith. Keep business rules testable and separate from HTTP and persistence concerns. Add abstractions only where they provide a real boundary; EF Core's `DbContext` is already a unit of work.

```
DistroFlow/
├── src/
│   ├── DistroFlow.Api/              (controllers, middleware, auth policies, Program.cs)
│   ├── DistroFlow.Application/      (use cases: Catalog, Pricing, Orders, Inventory, Billing, Payments)
│   ├── DistroFlow.Domain/           (entities, enums, rules, exceptions)
│   └── DistroFlow.Infrastructure/   (EF Core, configurations, migrations, audit)
├── tests/
│   ├── DistroFlow.UnitTests/
│   └── DistroFlow.IntegrationTests/
├── frontend/
├── docker-compose.yml
└── README.md
```

### Tenancy decision

The MVP runs for **one distributor**. Records carry a single `TenantId` (seeded, never client-supplied) so the model is ready for multiple distributors later. This is not tenant isolation. Before enabling multiple customers, trusted tenant resolution, query filters, cross-tenant constraints and isolation tests must be added.

### API conventions

- Versioned routes, for example `/api/v1/orders`.
- DTOs only; never expose entities directly.
- Request validation and Problem Details error responses.
- Pagination on list endpoints.
- `CancellationToken` on async request and data paths.
- Secrets in environment configuration or a secret manager, never in source control.

---

## 7. Initial Data Model

| Entity | Notes |
|---|---|
| ApplicationUser, Role | ASP.NET Core Identity |
| Retailer | Linked to a user account, price list, credit limit |
| Product | SKU unique per tenant |
| PriceList, PriceListItem | One price per product per list |
| Scheme | Type, product, parameters, validity dates, active flag |
| StockItem | Product, OnHand, Reserved, RowVersion (CHECK: OnHand >= 0, Reserved >= 0, Reserved <= OnHand) |
| StockMovement | Type, quantity, reference (order or adjustment), reason, timestamp |
| Order | Retailer, status, totals, credit check snapshot, RowVersion |
| OrderLine | Product, quantity, free quantity, unit price snapshot, discount, line total, applied scheme |
| Invoice, InvoiceLine | Generated at dispatch |
| Payment | Invoice, amount, date, reference, method |
| AuditEntry | Actor, entity, action, before/after summary, timestamp |

Guidelines:
- Use explicit foreign keys, unique constraints and indexes (for example on `Order(RetailerId, Status)` and `StockItem(ProductId)`).
- Do not store derived values (available stock, outstanding balance) redundantly without a documented reason.
- Store currency code on monetary documents; exchange-rate handling is out of scope.

---

## 8. API Outline (examples)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/auth/login` | Sign in |
| GET | `/api/v1/catalog` | Products with prices for the logged-in retailer |
| POST | `/api/v1/orders` | Place an order (product IDs and quantities only) |
| GET | `/api/v1/orders` | List orders (paged, filtered by role) |
| POST | `/api/v1/orders/{id}/release` | Approver releases a held order (reason required) |
| POST | `/api/v1/orders/{id}/confirm` | Confirm |
| POST | `/api/v1/orders/{id}/dispatch` | Dispatch and generate invoice |
| POST | `/api/v1/orders/{id}/cancel` | Cancel and release stock |
| POST | `/api/v1/invoices/{id}/payments` | Record a payment |
| GET | `/api/v1/reports/outstanding` | Retailer outstanding and ageing |

---

## 9. Testing Requirements

The MVP is not complete just because the screens work.

### Unit tests
- Price list lookup and snapshotting
- Scheme selection and benefit calculation (including boundary quantities)
- Order total and rounding calculations
- Credit-limit decision
- Allowed and disallowed order status transitions
- Outstanding balance and ageing calculation

### Integration tests (real database via Testcontainers)
- Authentication and authorization, including a retailer accessing another retailer's order
- Order placement persists correct totals and snapshots
- **Concurrency:** N parallel orders for limited stock result in no over-reservation and clear failures for the rest
- Cancellation releases stock exactly once, even if called twice
- Dispatch deducts stock and creates the invoice in one transaction (and rolls back on failure)
- Payment cannot exceed outstanding; partial payments update the balance
- Concurrent edit returns 409 Conflict
- Audit entries are written for key actions

Avoid relying only on EF Core's in-memory provider for relational behaviour.

---

## 10. MVP Acceptance Criteria

Ready for demonstration when:

1. An admin can create products, a price list, a scheme and a retailer with a credit limit.
2. A retailer can place an order and see server-calculated prices and scheme benefits.
3. An order exceeding the credit limit goes on hold, and an Approver can release it with a reason.
4. Stock is reserved safely, and the concurrency test passes.
5. Staff can move an order through confirm, pick, dispatch and deliver, and cancellation releases stock.
6. Dispatch generates an invoice and updates the retailer's outstanding balance.
7. A payment can be recorded in full or in part, and available credit updates.
8. Important state changes appear in the audit log.
9. Core rules and critical persistence workflows are covered by automated tests.
10. The API and frontend can be started from documented instructions (Docker preferred).

---

## 11. Suggested Build Sequence (about 6 weeks)

| Week | Focus |
|---|---|
| 1 | Solution setup, EF Core, migrations, Identity, API conventions, seed data |
| 2 | Products, stock, price lists, retailers, schemes, with unit tests for pricing |
| 3 | Order placement with server-side calculation and credit check |
| 4 | Concurrent stock reservation, lifecycle transitions, concurrency integration test |
| 5 | Dispatch and invoice, payments, outstanding and ageing, audit trail |
| 6 | Minimal React UI, Docker, CI, README and architecture write-up |

Build one vertical slice at a time, and test the full flow before adding the next feature.

---

## 12. Deferred for Later

- Multi-tenant isolation and onboarding
- Multiple warehouses, batches and expiry tracking
- WhatsApp ordering and notifications
- Online payment links and webhooks
- Returns, credit notes and GST e-invoicing
- Salesman app and route planning
- Message-based processing

---

## 13. Success Definition

DistroFlow succeeds as an MVP when it completes the order-to-payment workflow reliably, never over-reserves stock under concurrency, and applies pricing, schemes and credit rules in a way that can be explained and tested.

Its portfolio value comes from correct business rules, tests, reliability and clear engineering decisions, not from the number of technologies used. Do not claim commercial adoption, production scale or measured business savings unless they have been independently demonstrated. Validate demand by speaking with real distributors before investing further.
