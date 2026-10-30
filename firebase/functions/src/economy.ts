// Pure game-economy rules. No Firebase imports here, so everything in this
// file can be unit tested without emulators. The Cloud Functions in index.ts
// load data, call these, and write the results.

export type MatchResult = "win" | "loss" | "draw";

export const ECONOMY = {
  pointsFor: { win: 100, draw: 50, loss: 25 } as Record<MatchResult, number>,
  // Matches shorter than this are treated as fake (a best-of-3 can't end faster).
  minMatchSeconds: 20,
  // Matches left open longer than this are abandoned and earn nothing.
  maxMatchSeconds: 30 * 60,
  // Most points a free player can earn per UTC day.
  dailyCap: 1000,
  // Premium players earn more and can earn more per day.
  premiumMultiplier: 1.5,
  premiumDailyCap: 2000,
  // How many special abilities a fighter can take into a match.
  maxEquipped: 2,
  // Points every new player starts with, enough for one cheap ability.
  startingPoints: 100,
  // Abilities every new player owns from the start.
  starterAbilities: ["roar", "pounce"],
} as const;

export class EconomyError extends Error {
  constructor(
    public readonly code:
      | "invalid-argument"
      | "not-found"
      | "failed-precondition"
      | "already-exists"
      | "permission-denied",
    message: string,
  ) {
    super(message);
  }
}

export interface PlayerState {
  fightPoints: number;
  isPremium: boolean;
  dailyEarned: { day: string; points: number } | null;
}

export interface AbilityDef {
  id: string;
  cost: number;
  premiumOnly: boolean;
  fighterId: string; // "any" or a specific fighter id
  enabled: boolean;
}

export interface FighterDef {
  id: string;
  premiumOnly: boolean;
  enabled: boolean;
}

export function utcDay(nowMs: number): string {
  return new Date(nowMs).toISOString().slice(0, 10);
}

export function isMatchResult(value: unknown): value is MatchResult {
  return value === "win" || value === "loss" || value === "draw";
}

export function checkCanStartMatch(player: PlayerState, fighter: FighterDef | null): void {
  if (!fighter || !fighter.enabled) {
    throw new EconomyError("not-found", "That fighter does not exist.");
  }
  if (fighter.premiumOnly && !player.isPremium) {
    throw new EconomyError("permission-denied", "That fighter needs the premium version.");
  }
}

/**
 * Works out how many points a finished match earns, after the duration
 * sanity check, the premium multiplier and the daily cap.
 */
export function pointsForMatch(
  player: PlayerState,
  result: MatchResult,
  startedAtMs: number,
  nowMs: number,
): { award: number; dailyEarned: { day: string; points: number }; reason: string } {
  const day = utcDay(nowMs);
  const earnedToday = player.dailyEarned?.day === day ? player.dailyEarned.points : 0;
  const seconds = (nowMs - startedAtMs) / 1000;

  if (seconds < ECONOMY.minMatchSeconds) {
    return { award: 0, dailyEarned: { day, points: earnedToday }, reason: "too-short" };
  }
  if (seconds > ECONOMY.maxMatchSeconds) {
    return { award: 0, dailyEarned: { day, points: earnedToday }, reason: "abandoned" };
  }

  const base = ECONOMY.pointsFor[result];
  const raw = player.isPremium ? Math.round(base * ECONOMY.premiumMultiplier) : base;
  const cap = player.isPremium ? ECONOMY.premiumDailyCap : ECONOMY.dailyCap;
  const award = Math.max(0, Math.min(raw, cap - earnedToday));

  return {
    award,
    dailyEarned: { day, points: earnedToday + award },
    reason: award < raw ? "daily-cap" : "ok",
  };
}

/** Throws unless the player may buy this ability right now. Returns the price. */
export function checkPurchase(
  player: PlayerState,
  ability: AbilityDef | null,
  alreadyOwned: boolean,
): number {
  if (!ability || !ability.enabled) {
    throw new EconomyError("not-found", "That ability does not exist.");
  }
  if (alreadyOwned) {
    throw new EconomyError("already-exists", "You already own that ability.");
  }
  if (ability.premiumOnly && !player.isPremium) {
    throw new EconomyError("permission-denied", "That ability needs the premium version.");
  }
  if (!Number.isInteger(ability.cost) || ability.cost < 0) {
    throw new EconomyError("failed-precondition", "That ability has a bad price in the catalog.");
  }
  if (player.fightPoints < ability.cost) {
    throw new EconomyError("failed-precondition", "Not enough points.");
  }
  return ability.cost;
}

/** Throws unless every ability is owned and usable by the selected fighter. */
export function checkLoadout(
  abilityIds: unknown,
  owned: ReadonlySet<string>,
  catalog: ReadonlyMap<string, AbilityDef>,
  fighterId: string,
): string[] {
  if (!Array.isArray(abilityIds) || abilityIds.some((id) => typeof id !== "string")) {
    throw new EconomyError("invalid-argument", "abilityIds must be a list of ids.");
  }
  const ids = abilityIds as string[];
  if (new Set(ids).size !== ids.length) {
    throw new EconomyError("invalid-argument", "An ability can only be equipped once.");
  }
  if (ids.length > ECONOMY.maxEquipped) {
    throw new EconomyError(
      "invalid-argument",
      `You can equip at most ${ECONOMY.maxEquipped} abilities.`,
    );
  }
  for (const id of ids) {
    if (!owned.has(id)) {
      throw new EconomyError("permission-denied", `You don't own ${id}.`);
    }
    const def = catalog.get(id);
    if (!def || !def.enabled) {
      throw new EconomyError("not-found", `${id} is not in the catalog.`);
    }
    if (def.fighterId !== "any" && def.fighterId !== fighterId) {
      throw new EconomyError("failed-precondition", `${id} can't be used by ${fighterId}.`);
    }
  }
  return ids;
}
