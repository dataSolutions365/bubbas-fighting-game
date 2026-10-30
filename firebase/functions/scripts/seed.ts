// Loads ../../seed/catalog.json into Firestore (fighters and abilities).
//
// Emulator:  FIRESTORE_EMULATOR_HOST=localhost:8080 npm run seed -- demo-bubba
// Real project: GOOGLE_APPLICATION_CREDENTIALS=path/to/service-account.json npm run seed -- <project-id>

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { initializeApp } from "firebase-admin/app";
import { getFirestore } from "firebase-admin/firestore";

const projectId = process.argv[2];
if (!projectId) {
  console.error("Usage: npm run seed -- <project-id>");
  process.exit(1);
}

initializeApp({ projectId });
const db = getFirestore();

type Catalog = Record<"fighters" | "abilities", Record<string, Record<string, unknown>>>;
const catalog: Catalog = JSON.parse(
  readFileSync(join(__dirname, "..", "..", "seed", "catalog.json"), "utf8"),
);

async function main() {
  const batch = db.batch();
  for (const [collection, docs] of Object.entries(catalog)) {
    for (const [id, data] of Object.entries(docs)) {
      batch.set(db.collection(collection).doc(id), data);
    }
  }
  await batch.commit();
  console.log(
    `Seeded ${Object.keys(catalog.fighters).length} fighters and ` +
      `${Object.keys(catalog.abilities).length} abilities into ${projectId}.`,
  );
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
