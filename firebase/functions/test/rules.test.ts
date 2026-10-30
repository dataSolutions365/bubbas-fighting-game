// Firestore security rules tests. Needs the Firestore emulator:
//   npm run test:rules
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  assertFails,
  assertSucceeds,
  initializeTestEnvironment,
  RulesTestEnvironment,
} from "@firebase/rules-unit-testing";
import { doc, getDoc, setDoc, updateDoc } from "firebase/firestore";
import { afterAll, beforeAll, beforeEach, describe, it } from "vitest";

const emulator = process.env.FIRESTORE_EMULATOR_HOST;
let env: RulesTestEnvironment;

describe.skipIf(!emulator)("firestore.rules", () => {
  beforeAll(async () => {
    const [host, port] = emulator!.split(":");
    env = await initializeTestEnvironment({
      projectId: "demo-bubba",
      firestore: {
        host,
        port: Number(port),
        rules: readFileSync(join(__dirname, "..", "..", "firestore.rules"), "utf8"),
      },
    });
  });

  afterAll(() => env?.cleanup());

  beforeEach(async () => {
    await env.clearFirestore();
    await env.withSecurityRulesDisabled(async (ctx) => {
      const db = ctx.firestore();
      await setDoc(doc(db, "players/alice"), {
        displayName: "Alice",
        fightPoints: 100,
        isPremium: false,
        selectedFighter: "bear",
      });
      await setDoc(doc(db, "players/alice/ownedAbilities/roar"), { pricePaid: 0 });
      await setDoc(doc(db, "abilities/roar"), { cost: 50 });
    });
  });

  const alice = () => env.authenticatedContext("alice").firestore();
  const bob = () => env.authenticatedContext("bob").firestore();

  it("lets a player read their own profile but not someone else's", async () => {
    await assertSucceeds(getDoc(doc(alice(), "players/alice")));
    await assertFails(getDoc(doc(bob(), "players/alice")));
  });

  it("lets a player rename themselves and pick a fighter", async () => {
    await assertSucceeds(updateDoc(doc(alice(), "players/alice"), { displayName: "Bubba" }));
    await assertSucceeds(updateDoc(doc(alice(), "players/alice"), { selectedFighter: "gorilla" }));
  });

  it("blocks players from giving themselves points or premium", async () => {
    await assertFails(updateDoc(doc(alice(), "players/alice"), { fightPoints: 999999 }));
    await assertFails(updateDoc(doc(alice(), "players/alice"), { isPremium: true }));
    await assertFails(
      updateDoc(doc(alice(), "players/alice"), { displayName: "x", fightPoints: 5000 }),
    );
  });

  it("blocks bad display names", async () => {
    await assertFails(updateDoc(doc(alice(), "players/alice"), { displayName: "" }));
    await assertFails(updateDoc(doc(alice(), "players/alice"), { displayName: "x".repeat(25) }));
  });

  it("blocks players from creating profiles or granting abilities", async () => {
    await assertFails(setDoc(doc(bob(), "players/bob"), { fightPoints: 999999 }));
    await assertFails(setDoc(doc(alice(), "players/alice/ownedAbilities/frenzy"), {}));
    await assertFails(setDoc(doc(alice(), "players/alice/ledger/x"), { delta: 1000 }));
    await assertFails(setDoc(doc(alice(), "players/alice/matches/x"), { status: "open" }));
  });

  it("lets signed-in players read the catalog but never edit it", async () => {
    await assertSucceeds(getDoc(doc(alice(), "abilities/roar")));
    await assertFails(getDoc(doc(env.unauthenticatedContext().firestore(), "abilities/roar")));
    await assertFails(setDoc(doc(alice(), "abilities/roar"), { cost: 0 }));
  });
});
