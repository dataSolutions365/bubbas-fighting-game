import { describe, expect, it } from "vitest";
import {
  AbilityDef,
  checkCanStartMatch,
  checkLoadout,
  checkPurchase,
  ECONOMY,
  EconomyError,
  PlayerState,
  pointsForMatch,
} from "../src/economy";
import { shouldCutBilling } from "../src/billingKillSwitch";

const NOW = Date.parse("2026-10-30T12:00:00Z");
const MIN = 60_000;
const free = (over: Partial<PlayerState> = {}): PlayerState => ({
  fightPoints: 100,
  isPremium: false,
  dailyEarned: null,
  ...over,
});
const ability = (over: Partial<AbilityDef> = {}): AbilityDef => ({
  id: "charge",
  cost: 80,
  premiumOnly: false,
  fighterId: "any",
  enabled: true,
  ...over,
});
const code = (fn: () => unknown) => {
  try {
    fn();
  } catch (e) {
    return (e as EconomyError).code;
  }
  return "no-error";
};

describe("pointsForMatch", () => {
  it("pays the table amount for a normal match", () => {
    expect(pointsForMatch(free(), "win", NOW - 3 * MIN, NOW).award).toBe(100);
    expect(pointsForMatch(free(), "draw", NOW - 3 * MIN, NOW).award).toBe(50);
    expect(pointsForMatch(free(), "loss", NOW - 3 * MIN, NOW).award).toBe(25);
  });

  it("pays nothing for a match that is impossibly short", () => {
    const r = pointsForMatch(free(), "win", NOW - 5_000, NOW);
    expect(r).toMatchObject({ award: 0, reason: "too-short" });
  });

  it("pays nothing for an abandoned match", () => {
    const r = pointsForMatch(free(), "win", NOW - 31 * MIN, NOW);
    expect(r).toMatchObject({ award: 0, reason: "abandoned" });
  });

  it("stops at the daily cap", () => {
    const p = free({ dailyEarned: { day: "2026-10-30", points: ECONOMY.dailyCap - 30 } });
    const r = pointsForMatch(p, "win", NOW - 3 * MIN, NOW);
    expect(r).toMatchObject({ award: 30, reason: "daily-cap" });
    expect(r.dailyEarned.points).toBe(ECONOMY.dailyCap);
  });

  it("resets the daily total on a new UTC day", () => {
    const p = free({ dailyEarned: { day: "2026-10-29", points: ECONOMY.dailyCap } });
    const r = pointsForMatch(p, "win", NOW - 3 * MIN, NOW);
    expect(r.award).toBe(100);
    expect(r.dailyEarned).toEqual({ day: "2026-10-30", points: 100 });
  });

  it("gives premium players the multiplier and higher cap", () => {
    const r = pointsForMatch(free({ isPremium: true }), "win", NOW - 3 * MIN, NOW);
    expect(r.award).toBe(150);
  });
});

describe("checkPurchase", () => {
  it("returns the catalog price when the player can afford it", () => {
    expect(checkPurchase(free(), ability(), false)).toBe(80);
  });

  it("rejects when points are short", () => {
    expect(code(() => checkPurchase(free({ fightPoints: 79 }), ability(), false))).toBe(
      "failed-precondition",
    );
  });

  it("rejects buying twice", () => {
    expect(code(() => checkPurchase(free(), ability(), true))).toBe("already-exists");
  });

  it("rejects premium-only abilities for free players", () => {
    expect(code(() => checkPurchase(free(), ability({ premiumOnly: true }), false))).toBe(
      "permission-denied",
    );
    expect(checkPurchase(free({ isPremium: true }), ability({ premiumOnly: true }), false)).toBe(80);
  });

  it("rejects missing, disabled or badly priced abilities", () => {
    expect(code(() => checkPurchase(free(), null, false))).toBe("not-found");
    expect(code(() => checkPurchase(free(), ability({ enabled: false }), false))).toBe("not-found");
    expect(code(() => checkPurchase(free(), ability({ cost: -5 }), false))).toBe(
      "failed-precondition",
    );
  });
});

describe("checkLoadout", () => {
  const catalog = new Map<string, AbilityDef>([
    ["roar", ability({ id: "roar" })],
    ["pounce", ability({ id: "pounce" })],
    ["maul", ability({ id: "maul", fighterId: "bear" })],
    ["chest-beat", ability({ id: "chest-beat", fighterId: "gorilla" })],
  ]);
  const owned = new Set(["roar", "pounce", "maul", "chest-beat"]);

  it("accepts owned abilities the fighter can use", () => {
    expect(checkLoadout(["roar", "maul"], owned, catalog, "bear")).toEqual(["roar", "maul"]);
  });

  it("rejects unowned, duplicate, too many, or wrong-fighter abilities", () => {
    expect(code(() => checkLoadout(["roar"], new Set(), catalog, "bear"))).toBe("permission-denied");
    expect(code(() => checkLoadout(["roar", "roar"], owned, catalog, "bear"))).toBe(
      "invalid-argument",
    );
    expect(code(() => checkLoadout(["roar", "pounce", "maul"], owned, catalog, "bear"))).toBe(
      "invalid-argument",
    );
    expect(code(() => checkLoadout(["chest-beat"], owned, catalog, "bear"))).toBe(
      "failed-precondition",
    );
    expect(code(() => checkLoadout("roar", owned, catalog, "bear"))).toBe("invalid-argument");
  });
});

describe("checkCanStartMatch", () => {
  it("blocks premium fighters for free players", () => {
    const fighter = { id: "lion", premiumOnly: true, enabled: true };
    expect(code(() => checkCanStartMatch(free(), fighter))).toBe("permission-denied");
    expect(code(() => checkCanStartMatch(free({ isPremium: true }), fighter))).toBe("no-error");
    expect(code(() => checkCanStartMatch(free(), null))).toBe("not-found");
  });
});

describe("shouldCutBilling", () => {
  it("only cuts once spend reaches the budget", () => {
    expect(shouldCutBilling({ costAmount: 4.99, budgetAmount: 5 })).toBe(false);
    expect(shouldCutBilling({ costAmount: 5, budgetAmount: 5 })).toBe(true);
    expect(shouldCutBilling({ costAmount: 5, budgetAmount: 0 })).toBe(false);
  });
});
