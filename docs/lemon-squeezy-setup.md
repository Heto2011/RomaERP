# Lemon Squeezy card payments — setup (outside Egypt)

Egypt customers keep paying by InstaPay. Everyone else sees a **Pay by card** button on the subscription page.
Lemon Squeezy charges in **USD**, owns the recurring charge, and tells us through a webhook; we never see card numbers.

## 1. In Lemon Squeezy (start in **Test mode**)
1. Create a **Store** (name: Roma Group).
2. Create one **subscription product** per plan, with a **monthly** variant and an **annual** variant:

**Gulf and everywhere except Egypt/UK** (the SAR list ÷ 3.75):

| Plan | Monthly (USD) | Annual (USD, 10 months) | Variant keys |
|---|---|---|---|
| Essential | 40 | 400 | `essential-monthly` / `essential-annual` |
| Business | 93 | 930 | `business-monthly` / `business-annual` |
| Professional | 173 | 1,730 | `professional-monthly` / `professional-annual` |
| Roma HR | 26 | 260 | `people-monthly` / `people-annual` |

**UK and Guernsey** (the sterling list £49 / £99 / £179 / £49 for Roma HR at about 1.35 USD per GBP — the rate was 1.3559 on 9 Sep 2026):

| Plan | Monthly (USD) | Annual (USD, 10 months) | Variant keys |
|---|---|---|---|
| Essential | 66 | 660 | `essential-monthly-uk` / `essential-annual-uk` |
| Business | 134 | 1,340 | `business-monthly-uk` / `business-annual-uk` |
| Professional | 242 | 2,420 | `professional-monthly-uk` / `professional-annual-uk` |
| Roma HR | 66 | 660 | `people-monthly-uk` / `people-annual-uk` |

   (Change prices freely; the app just opens whichever variant you map. Lemon's fee is about 5% + $0.50, plus 0.5% for subscriptions and 1.5% for international cards or PayPal.)
   Roma HR launch offer: make it a Lemon **discount code** (50%, repeating for 3 months), not a lower price — a variant's price never changes by itself.
3. **Settings → API**: create an API key.
4. **Settings → Webhooks**: URL `https://romagroup.app/api/webhooks/lemonsqueezy`, choose a signing secret, and tick the
   events: subscription_created, subscription_updated, subscription_cancelled, subscription_resumed, subscription_expired,
   subscription_payment_success, subscription_payment_failed, subscription_payment_refunded.
5. Note each variant's id (the number in its URL).

## 2. GitHub repository secrets (Settings → Secrets and variables → Actions)
| Secret | Value |
|---|---|
| `LEMON_API_KEY` | the API key |
| `LEMON_STORE_ID` | optional — leave unset and the app finds the account's store from the API key |
| `LEMON_WEBHOOK_SECRET` | the signing secret from step 4 |
| `LEMON_VARIANTS` | JSON, e.g. `{"essential-monthly":"111","essential-annual":"112","people-monthly":"130"}` — only list variants that exist |

Then run the **Deploy** workflow. The button appears once all four are set.

## 3. Test (Test mode, Lemon's test card 4242 4242 4242 4242 — never a real card)
Pay for a trial company → it becomes Active, an invoice appears as Paid (USD), and the company's **Activity** log shows
"Card subscription started" / "Card payment received". Switch Lemon to Live mode, replace the key/secret/variants, redeploy.

## Known limits
- Extra branches/users beyond the plan are **not** billed through Lemon yet (the card charge is the plan's flat price).
- Plan changes for a card customer are done in Lemon's customer portal / by the owner.
