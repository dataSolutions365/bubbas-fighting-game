// Cloud Functions: the only code allowed to change a player's points,
// owned abilities, matches or ledger. The game calls these as "callable"
// functions; Firebase passes along who is signed in.

import { initializeApp } from "firebase-admin/app";
import { FieldValue, getFirestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CallableRequest, HttpsError, onCall } from "firebase-functions/v2/https";
import {
  AbilityDef,
  checkCanStartMatch,
  checkLoadout,
  checkPurchase,
  ECONOMY,
  EconomyError,
  FighterDef,
  isMatchResult,
  PlayerState,
  pointsForMatch,
} from "./economy";

export { billingKillSwitch } from "./billingKillSwitch";

initializeApp();
const db = getFirestore();

// Turn on once App Check is set up in the Firebase console (see README).
const callableOptions = {
  enforceAppCheck: process.env.ENFORCE_APP_CHECK === "true",
  region: process.env.FUNCTIONS_REGION || "us-central1",
};

const DEFAULT_FIGHTER = "jacke";

function requireUid(request: CallableRequest): string {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Sign in first.");
  }
  return request.auth.uid;
}

function requireString(value: unknown, name: string): string {
  if (typeof value !== "string" || value.length === 0 || value.length > 64) {
    throw new HttpsError("invalid-argument", `${name} is required.`);
  }
  return value;
}

// Converts rule failures into errors the game can show.
async function run<T>(fn: () => Promise<T>): Promise<T> {
  try {
    return await fn();
  } catch (err) {
    if (err instanceof EconomyError) {
      throw new HttpsError(err.code, err.message);
    }
    throw err;
  }
}

const playerRef = (uid: string) => db.collection("players").doc(uid);

function toPlayerState(data: FirebaseFirestore.DocumentData | undefined): PlayerState {
  if (!data) {
    throw new HttpsError("failed-precondition", "Player profile missing. Call ensurePlayer first.");
  }
  return {
    fightPoints: data.fightPoints ?? 0,
    isPremium: data.isPremium === true,
    dailyEarned: data.dailyEarned ?? null,
  };
}

function toAbility(id: string, data: FirebaseFirestore.DocumentData | undefined): AbilityDef | null {
  if (!data) return null;
  return {
    id,
    cost: data.cost,
    premiumOnly: data.premiumOnly === true,
    fighterId: data.fighterId ?? "any",
    enabled: data.enabled !== false,
  };
}

function toFighter(id: string, data: FirebaseFirestore.DocumentData | undefined): FighterDef | null {
  if (!data) return null;
  return { id, premiumOnly: data.premiumOnly === true, enabled: data.enabled !== false };
}

function addLedgerEntry(
  tx: Transaction,
  uid: string,
  delta: number,
  reason: string,
  refId: string | null,
): void {
  tx.create(playerRef(uid).collection("ledger").doc(), {
    delta,
    reason,
    refId,
    at: FieldValue.serverTimestamp(),
  });
}

/** Creates the player's profile on first launch. Safe to call every launch. */
export const ensurePlayer = onCall(callableOptions, (request) =>
  run(async () => {
    const uid = requireUid(request);
    return db.runTransaction(async (tx) => {
      const ref = playerRef(uid);
      const snap = await tx.get(ref);
      if (snap.exists) {
        return { created: false };
      }
      tx.create(ref, {
        displayName: "Fighter",
        fightPoints: ECONOMY.startingPoints,
        lifetimePoints: ECONOMY.startingPoints,
        isPremium: false,
        selectedFighter: DEFAULT_FIGHTER,
        equippedAbilities: [...ECONOMY.starterAbilities],
        dailyEarned: null,
        createdAt: FieldValue.serverTimestamp(),
      });
      for (const abilityId of ECONOMY.starterAbilities) {
        tx.create(ref.collection("ownedAbilities").doc(abilityId), {
          purchasedAt: FieldValue.serverTimestamp(),
          pricePaid: 0,
        });
      }
      addLedgerEntry(tx, uid, ECONOMY.startingPoints, "starting-points", null);
      return { created: true };
    });
  }),
);

/** Opens a match record. Points are only paid against an open match. */
export const startMatch = onCall(callableOptions, (request) =>
  run(async () => {
    const uid = requireUid(request);
    const fighterId = requireString(request.data?.fighterId, "fighterId");
    const opponentId = requireString(request.data?.opponentId, "opponentId");

    const [playerSnap, fighterSnap] = await Promise.all([
      playerRef(uid).get(),
      db.collection("fighters").doc(fighterId).get(),
    ]);
    checkCanStartMatch(toPlayerState(playerSnap.data()), toFighter(fighterId, fighterSnap.data()));

    const matchRef = playerRef(uid).collection("matches").doc();
    await matchRef.create({
      fighterId,
      opponentId,
      status: "open",
      startedAt: Timestamp.now(),
      endedAt: null,
      result: null,
      pointsAwarded: 0,
    });
    return { matchId: matchRef.id };
  }),
);

/** Closes a match and pays points, once, within the daily cap. */
export const endMatch = onCall(callableOptions, (request) =>
  run(async () => {
    const uid = requireUid(request);
    const matchId = requireString(request.data?.matchId, "matchId");
    const result = request.data?.result;
    if (!isMatchResult(result)) {
      throw new HttpsError("invalid-argument", "result must be win, loss or draw.");
    }

    return db.runTransaction(async (tx) => {
      const pRef = playerRef(uid);
      const mRef = pRef.collection("matches").doc(matchId);
      const [playerSnap, matchSnap] = await Promise.all([tx.get(pRef), tx.get(mRef)]);

      if (!matchSnap.exists) {
        throw new HttpsError("not-found", "No such match.");
      }
      if (matchSnap.get("status") !== "open") {
        throw new HttpsError("failed-precondition", "That match is already finished.");
      }

      const player = toPlayerState(playerSnap.data());
      const startedAt = matchSnap.get("startedAt") as Timestamp;
      const now = Timestamp.now();
      const outcome = pointsForMatch(player, result, startedAt.toMillis(), now.toMillis());

      tx.update(mRef, {
        status: outcome.reason === "abandoned" ? "abandoned" : "finished",
        endedAt: now,
        result,
        pointsAwarded: outcome.award,
        payoutReason: outcome.reason,
      });
      tx.update(pRef, {
        fightPoints: FieldValue.increment(outcome.award),
        lifetimePoints: FieldValue.increment(outcome.award),
        dailyEarned: outcome.dailyEarned,
      });
      if (outcome.award > 0) {
        addLedgerEntry(tx, uid, outcome.award, `match-${result}`, matchId);
      }
      return {
        pointsAwarded: outcome.award,
        fightPoints: player.fightPoints + outcome.award,
        reason: outcome.reason,
      };
    });
  }),
);

/** Spends points on an ability. The price always comes from the catalog. */
export const purchaseAbility = onCall(callableOptions, (request) =>
  run(async () => {
    const uid = requireUid(request);
    const abilityId = requireString(request.data?.abilityId, "abilityId");

    return db.runTransaction(async (tx) => {
      const pRef = playerRef(uid);
      const ownedRef = pRef.collection("ownedAbilities").doc(abilityId);
      const abilityRef = db.collection("abilities").doc(abilityId);
      const [playerSnap, ownedSnap, abilitySnap] = await Promise.all([
        tx.get(pRef),
        tx.get(ownedRef),
        tx.get(abilityRef),
      ]);

      const player = toPlayerState(playerSnap.data());
      const price = checkPurchase(player, toAbility(abilityId, abilitySnap.data()), ownedSnap.exists);

      tx.update(pRef, { fightPoints: FieldValue.increment(-price) });
      tx.create(ownedRef, { purchasedAt: FieldValue.serverTimestamp(), pricePaid: price });
      addLedgerEntry(tx, uid, -price, "purchase-ability", abilityId);
      return { abilityId, fightPoints: player.fightPoints - price };
    });
  }),
);

/** Saves which owned abilities the selected fighter takes into matches. */
export const equipAbilities = onCall(callableOptions, (request) =>
  run(async () => {
    const uid = requireUid(request);
    const pRef = playerRef(uid);

    const [playerSnap, ownedSnap] = await Promise.all([
      pRef.get(),
      pRef.collection("ownedAbilities").get(),
    ]);
    if (!playerSnap.exists) {
      throw new HttpsError("failed-precondition", "Player profile missing. Call ensurePlayer first.");
    }
    const owned = new Set(ownedSnap.docs.map((d) => d.id));
    const requested: unknown = request.data?.abilityIds;
    const ids = Array.isArray(requested) ? requested.filter((id) => typeof id === "string") : [];
    const abilitySnaps = ids.length
      ? await db.getAll(...ids.map((id) => db.collection("abilities").doc(id)))
      : [];
    const catalog = new Map<string, AbilityDef>();
    for (const snap of abilitySnaps) {
      const def = toAbility(snap.id, snap.data());
      if (def) catalog.set(snap.id, def);
    }

    const fighterId = playerSnap.get("selectedFighter") ?? DEFAULT_FIGHTER;
    const loadout = checkLoadout(requested, owned, catalog, fighterId);
    await pRef.update({ equippedAbilities: loadout });
    return { equippedAbilities: loadout };
  }),
);
