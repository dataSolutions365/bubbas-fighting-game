// Hard spending cap. Google Cloud budgets only send alerts; they never stop
// charges. This function listens to the budget's Pub/Sub topic and, once
// actual spend reaches the budget, unlinks billing from the project.
//
// WARNING: with billing unlinked, Cloud Functions and anything beyond the
// free Spark limits stop working until you re-link billing in the console.
// Setup steps are in the README ("Spending cap"). It does nothing unless
// KILL_SWITCH_ENABLED=true is set in functions/.env.

import { CloudBillingClient } from "@google-cloud/billing";
import { logger } from "firebase-functions/v2";
import { onMessagePublished } from "firebase-functions/v2/pubsub";

export interface BudgetNotification {
  costAmount: number;
  budgetAmount: number;
  budgetDisplayName?: string;
}

export function shouldCutBilling(n: BudgetNotification): boolean {
  return (
    Number.isFinite(n.costAmount) &&
    Number.isFinite(n.budgetAmount) &&
    n.budgetAmount > 0 &&
    n.costAmount >= n.budgetAmount
  );
}

export const billingKillSwitch = onMessagePublished("billing-budget", async (event) => {
  const notification = event.data.message.json as BudgetNotification;
  logger.info("Budget notification", notification);

  if (process.env.KILL_SWITCH_ENABLED !== "true") {
    logger.info("Kill switch disabled; not touching billing.");
    return;
  }
  if (!shouldCutBilling(notification)) {
    return;
  }

  const projectId = process.env.GCLOUD_PROJECT;
  if (!projectId) {
    logger.error("No project id available; cannot disable billing.");
    return;
  }
  const billing = new CloudBillingClient();
  const name = `projects/${projectId}`;
  const [info] = await billing.getProjectBillingInfo({ name });
  if (!info.billingEnabled) {
    logger.info("Billing already disabled.");
    return;
  }
  await billing.updateProjectBillingInfo({
    name,
    projectBillingInfo: { billingAccountName: "" },
  });
  logger.warn(
    `Spend ${notification.costAmount} reached budget ${notification.budgetAmount}; billing disabled for ${projectId}.`,
  );
});
