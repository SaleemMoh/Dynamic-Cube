# ADR-0022: Payments as a module: bills through the Integration Manager, signed callbacks, a workflow step that waits

**Status:** Accepted (Phase 2b)

## Context
Paid service levels (the reference service's Premium and Diamond) must be paid before processing starts, through
SADAD in production and other channels (Tahseel, a cashier) later or elsewhere. Each provider has its own API, bill
numbers and notification format, and a bill's life (issued, paid, expired, cancelled) outlives the request step that
asked for it. Requests belong to Services and processing to Workflow; neither should know a provider.

## Decision
- **A Payments module (schema `pay`) owns bills.** A bill has the platform's number (`BILL-YYYY-000001`), the
  provider's number (e.g. the SADAD number), the payer, the lines (net, VAT, total), a due date and a history.
  Other modules use the kernel contract `IPayments` (issue a bill, list a subject's bills) inside their own unit of
  work, and are told when a bill closes through `IPaymentListener` (`BillClosed`: paid, expired or cancelled).
- **Providers are configuration (`pay.provider`).** Registering a bill with the provider is an Integration Manager
  operation (`createOperation`) that receives `billNumber`, `amount`, `dueDate`, `payerId`, `payerName` and
  `description`; `billNumberOutput` says where its output carries the provider's number. A provider without an
  operation is manual (cashier, transfer): staff record the payment. The provider also has the biller code, the days
  to pay, a payment page template (`{billNumber}`, `{amount}`), the payer's instructions and where its notifications
  carry the bill number, status, amount and reference.
- **Payment notifications are anonymous, signed calls** on the external host
  (`POST /api/payments/v1/callbacks/{provider}`): HMAC-SHA256 of the raw body with the key of the provider's
  `callbackSecret` (from the secret store, never a table), hex in `X-Signature`. A notification is applied only when
  its amount equals the bill's total; repeating it changes nothing; one that arrives after the bill expired is still
  recorded as paid (the money was taken) but does not reopen the request. Every notification is kept in the history.
- **A workflow PAYMENT step bills the request's fees** (the `fees` facts, or a JSONLogic `amount` with a VAT rate)
  and waits: the token is `WAITING_PAYMENT`, the request `PENDING_PAYMENT` (display status `AWAITING_PAYMENT`) and the
  deadline stands still. Paid, it continues at `next`; expired or cancelled, it goes to `expired` (required). With
  nothing to pay it passes straight on.
- **Expiry is a job**: the worker closes unpaid bills past their due date every five minutes.
- Staff see bills (search, details, history), record a payment made elsewhere and cancel a bill
  (`payments.bills`, EDIT); the payer sees the bill on the request, with the pay button while unpaid and the receipt
  once paid. Notifications `PAY_BILL_ISSUED`, `PAY_BILL_PAID`, `PAY_BILL_EXPIRED` are ordinary communication rules on
  the `BILL_*` events.

## Consequences
- Adding Tahseel or a bank gateway is configuration: an integration operation and a provider; nothing in Services
  or Workflow changes.
- Where payment happens in processing is the workflow designer's choice: before review (the reference service),
  after approval, or twice.
- Refunds, partial payments and reconciliation reports are not covered yet; the bill history holds what they will
  need.
