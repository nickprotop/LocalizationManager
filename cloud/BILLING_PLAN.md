# LRM Cloud Billing Implementation Plan

## Overview
Integrate Stripe billing into LRM Cloud for the $0/$9/$29 pricing tiers (Free/Team/Enterprise).

**Pricing:**
| Plan | Price | LRM Chars | BYOK Chars | Projects | Team Members |
|------|-------|-----------|------------|----------|--------------|
| Free | $0/mo | 5K | 25K | 3 | - |
| Team | $9/mo | 50K | 250K | Unlimited | 10 |
| Enterprise | $29/mo | 500K | 2.5M | Unlimited | Unlimited |

---

## Progress Summary

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 1: Configuration | Complete | 3/3 |
| Phase 2: BillingService | Complete | 6/6 |
| Phase 3: DTOs | Complete | 4/4 |
| Phase 4: API Endpoints | Complete | 2/2 |
| Phase 5: Database | Complete | 2/2 |
| Phase 6: Blazor UI | Complete | 3/3 |
| Phase 7: Testing | Not Started | 0/4 |
| **Overall** | **In Progress** | **20/24** |

---

## Existing Infrastructure (Already Built)

- [x] `User.StripeCustomerId` field exists
- [x] `User.Plan` field with limits tracking
- [x] `UsageService` for usage calculations
- [x] `TranslationUsageHistory` for per-provider tracking
- [x] `CloudConfiguration.LimitsConfiguration` with plan helpers
- [x] Blazor billing page placeholder ("Coming soon")

---

## Phase 1: Stripe Configuration & Setup

### Tasks
- [x] **1.1** Add Stripe NuGet package to `LrmCloud.Api.csproj`
- [x] **1.2** Add `StripeConfiguration` class to `CloudConfiguration.cs`
- [x] **1.3** Add stripe section to `config.json` and `config.sample.json`

### Files
| File | Action |
|------|--------|
| `cloud/src/LrmCloud.Api/LrmCloud.Api.csproj` | Add package |
| `cloud/src/LrmCloud.Shared/Configuration/CloudConfiguration.cs` | Add class |
| `cloud/deploy/config.json` | Add section |

### Code: StripeConfiguration
```csharp
public sealed class StripeConfiguration
{
    public string SecretKey { get; init; } = "";
    public string PublishableKey { get; init; } = "";
    public string WebhookSecret { get; init; } = "";
    public string TeamPriceId { get; init; } = "";
    public string EnterprisePriceId { get; init; } = "";
}
```

---

## Phase 2: Backend Billing Service

### Tasks
- [x] **2.1** Create `IBillingService` interface
- [x] **2.2** Implement `GetOrCreateCustomerAsync` (Stripe customer)
- [x] **2.3** Implement `CreateCheckoutSessionAsync` (Stripe Checkout)
- [x] **2.4** Implement `CreatePortalSessionAsync` (Customer Portal)
- [x] **2.5** Implement `GetSubscriptionAsync` / `CancelSubscriptionAsync`
- [x] **2.6** Implement `HandleWebhookAsync` (event processing)

### Files
| File | Action |
|------|--------|
| `cloud/src/LrmCloud.Api/Services/IBillingService.cs` | Create |
| `cloud/src/LrmCloud.Api/Services/BillingService.cs` | Create |

### Webhook Events to Handle
| Event | Action |
|-------|--------|
| `checkout.session.completed` | Activate subscription, update User.Plan |
| `customer.subscription.updated` | Handle plan changes |
| `customer.subscription.deleted` | Downgrade to free |
| `invoice.payment_failed` | Mark subscription at risk |
| `invoice.paid` | Confirm payment |

---

## Phase 3: DTOs

### Tasks
- [x] **3.1** Create `SubscriptionDto.cs`
- [x] **3.2** Create `CheckoutSessionDto.cs`
- [x] **3.3** Create `PortalSessionDto.cs`
- [x] **3.4** Create `CreateCheckoutRequest.cs` + `CreatePortalRequest.cs`

### Files
| File | Action |
|------|--------|
| `cloud/src/LrmCloud.Shared/DTOs/Billing/SubscriptionDto.cs` | Create |
| `cloud/src/LrmCloud.Shared/DTOs/Billing/CheckoutSessionDto.cs` | Create |
| `cloud/src/LrmCloud.Shared/DTOs/Billing/PortalSessionDto.cs` | Create |
| `cloud/src/LrmCloud.Shared/DTOs/Billing/CreateCheckoutRequest.cs` | Create |

---

## Phase 4: API Endpoints

### Tasks
- [x] **4.1** Create `BillingController.cs` with endpoints
- [x] **4.2** Create `WebhooksController.cs` for Stripe webhooks

### Files
| File | Action |
|------|--------|
| `cloud/src/LrmCloud.Api/Controllers/BillingController.cs` | Create |
| `cloud/src/LrmCloud.Api/Controllers/WebhooksController.cs` | Create |

### Endpoints
| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| GET | `/api/billing/subscription` | Yes | Get current subscription |
| POST | `/api/billing/checkout` | Yes | Create Stripe Checkout session |
| POST | `/api/billing/portal` | Yes | Create Customer Portal session |
| POST | `/api/billing/cancel` | Yes | Cancel subscription |
| POST | `/api/webhooks/stripe` | No* | Stripe webhook handler |

*Webhook uses signature verification instead of JWT

---

## Phase 5: Database Changes

### Tasks
- [x] **5.1** Add subscription fields to `User.cs` entity
- [x] **5.2** Create and run EF migration

### Files
| File | Action |
|------|--------|
| `cloud/src/LrmCloud.Shared/Entities/User.cs` | Modify |
| `cloud/src/LrmCloud.Api/Data/Migrations/` | New migration |

### New Fields on User
```csharp
public string? StripeSubscriptionId { get; set; }
public string SubscriptionStatus { get; set; } = "none"; // none, active, past_due, canceled
public DateTime? SubscriptionCurrentPeriodEnd { get; set; }
public bool CancelAtPeriodEnd { get; set; }
```

---

## Phase 6: Blazor UI Updates

### Tasks
- [x] **6.1** Create `BillingService.cs` (client-side API wrapper)
- [x] **6.2** Create/update user `Billing.razor` page
- [x] **6.3** Update org `Billing.razor` (replace placeholder)

### Files
| File | Action |
|------|--------|
| `cloud/src/LrmCloud.Web/Services/BillingService.cs` | Create |
| `cloud/src/LrmCloud.Web/Pages/Settings/Billing.razor` | Create |
| `cloud/src/LrmCloud.Web/Pages/Organizations/Billing.razor` | Update |

### UI Features
- Current plan display with status badge
- Upgrade buttons → redirect to Stripe Checkout
- "Manage Subscription" → redirect to Stripe Portal
- Cancel subscription with confirmation
- Next billing date display
- Usage stats integration

---

## Phase 7: Testing & Integration

### Tasks
- [ ] **7.1** Configure Stripe test mode keys
- [ ] **7.2** Test checkout flow (free → team → enterprise)
- [ ] **7.3** Test webhook handling with Stripe CLI
- [ ] **7.4** Test edge cases (cancel, payment failure, downgrade)

### Test Cards
| Card | Result |
|------|--------|
| `4242424242424242` | Success |
| `4000000000000002` | Decline |
| `4000000000009995` | Insufficient funds |

### Stripe CLI Command
```bash
stripe listen --forward-to localhost:5000/api/webhooks/stripe
```

---

## DI Registration (Program.cs)

```csharp
// Add after other services
builder.Services.AddScoped<IBillingService, BillingService>();

// Configure Stripe
StripeConfiguration.ApiKey = config.Stripe.SecretKey;
```

---

## Stripe Dashboard Setup (External)

- [ ] Create Stripe account (if needed)
- [ ] Create Product: "LRM Team" → $9/month
- [ ] Create Product: "LRM Enterprise" → $29/month
- [ ] Note Price IDs for config
- [ ] Configure Customer Portal branding
- [ ] Add webhook endpoint URL
- [ ] Note webhook signing secret

---

## Security Checklist

- [ ] Webhook signature verification
- [ ] No sensitive data in DTOs (Stripe IDs only)
- [ ] HTTPS enforcement
- [ ] Rate limiting on checkout
- [ ] Audit logging for plan changes

---

## Estimated Effort

| Phase | Effort | Status |
|-------|--------|--------|
| Phase 1: Configuration | 1 hour | Not Started |
| Phase 2: BillingService | 4 hours | Not Started |
| Phase 3: DTOs | 30 min | Not Started |
| Phase 4: API Endpoints | 2 hours | Not Started |
| Phase 5: Database | 30 min | Not Started |
| Phase 6: Blazor UI | 3 hours | Not Started |
| Phase 7: Testing | 2 hours | Not Started |
| **Total** | **~13 hours** | |
