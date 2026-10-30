# Bubba's Fighting Game

A realistic 1v1 fighting game with animal fighters, built in Unity for phones.
You win matches to earn points, and spend the points on special abilities.
The game is free to play, and a premium tier can be switched on later.

The full plan covers the engine choice, Firebase schema, free/paid split and
prototype scope: [Animal Fighting Game: Plan](https://claude.ai/artifact/XfrVDDyW4mN8mzVf8uY1oh).

## What's in the repo

| Folder | What it is |
| --- | --- |
| `game/` | The Unity project. Game code is in `game/Assets/Scripts`. |
| `firebase/` | Firestore security rules, Cloud Functions (points, shop, matches) and the starter catalog of fighters and abilities. |
| `ci/` | A compile check that lets GitHub build the C# scripts without Unity. |

### How points stay cheat-proof

The game never writes points or abilities itself. It calls Cloud Functions, and
only they can change that data. The Firestore rules block the app from writing
to those fields directly.

| Function | What it does |
| --- | --- |
| `ensurePlayer` | Creates the profile on first launch: 100 points plus the Roar and Pounce abilities. |
| `startMatch` | Opens a match record with a server timestamp. |
| `endMatch` | Pays points once per match: 100 for a win, 50 for a draw, 25 for a loss. Matches under 20 seconds or over 30 minutes earn nothing, and free players can earn at most 1,000 points per day. |
| `purchaseAbility` | Takes the price from the catalog (never from the app), checks the balance and buys the ability in one transaction. |
| `equipAbilities` | Saves up to 2 owned abilities that the selected fighter can use. |
| `billingKillSwitch` | Optional hard spending cap (see [Spending cap](#spending-cap)). |

Every change to a player's points also writes an entry to
`players/{uid}/ledger`, so the balance can always be audited.

### Controls

The player picks a control level in the menu:

- **Easy (tap):** tap to attack (tap quickly to combo), swipe toward the opponent for a heavy hit, swipe away to dodge, hold to block, and tap the ability buttons for specials. Walking is automatic.
- **Medium (pad):** drag on the left side of the screen to move, then use the Light, Heavy, Block, Dodge and two special buttons. Forward + Heavy is a lunge and Back + Heavy is an uppercut. Starting a block just before a hit lands parries it and stuns the attacker.
- **Hard:** planned for after the prototype. Specials will need a motion input, and the parry window will be tighter.

The opponent's difficulty (Easy, Medium or Hard) is a separate setting.

In the Unity editor you can also use the keyboard: **A/D** move, **J** light, **K** heavy, **L** block (hold), **Space** dodge, **1/2** specials.

## 1. Open the game in Unity

1. Install [Unity Hub](https://unity.com/download), then install **Unity 6 LTS** with **Android Build Support** (add **iOS Build Support** if you're on a Mac).
2. Clone this repo with GitHub Desktop or `git clone`, then run `git lfs install` once. Git LFS stores big files such as models and sounds.
3. In Unity Hub, choose **Add > Add project from disk** and pick the `game` folder. Choose Unity 6 if asked. On first open, Unity creates the rest of the project files (`ProjectSettings/`, `Packages/`), which you should commit.
4. Create an empty scene (**File > New Scene > Basic**), add an empty GameObject, click **Add Component** and choose **Prototype Bootstrap**. Save the scene as `Assets/Scenes/Prototype.unity`.
5. Press **Play**. You'll get a menu, a shop and a best-of-3 fight between two capsules that stand in for the bear and gorilla.

Until Firebase is connected, the game uses an offline stand-in for the server, so you can play right away. Points then only last until you stop the game.

To try the touch controls, run the game on your phone using [Unity Remote](https://docs.unity3d.com/Manual/UnityRemote5.html) or build an Android APK.

### Swapping in real animals

1. Import an animal model, ideally rigged and with animations.
2. Drag it onto the fighter's root object and delete or hide the `Body (replace with model)` capsule.
3. Give it an Animator Controller. The fighter looks for these parameters and uses any that exist:
   - Triggers: `Light`, `Heavy`, `Lunge`, `Uppercut`, `Hit`, `Dodge`, `Parry`, `KO`, `Reset`
   - Bool: `Block`
   - Float: `Speed`

To tune a fighter, create **Assets > Create > Bubba > Fighter Stats** and assign it in the Prototype Bootstrap's roster.

## 2. Connect Firebase

1. In the [Firebase console](https://console.firebase.google.com), create a project. Then:
   - Turn on **Authentication > Sign-in method > Anonymous**.
   - Create a **Firestore database**.
   - Upgrade to the pay-as-you-go **Blaze** plan, which Cloud Functions require. Set a monthly budget, such as $5, when prompted.
2. Install the Firebase CLI with `npm install -g firebase-tools`, then run `firebase login`.
3. In `firebase/`, copy `.firebaserc.example` to `.firebaserc` and put in your project ID.
4. Deploy the rules and functions:
   ```sh
   cd firebase/functions
   npm install
   npm run deploy
   ```
5. Load the starter catalog of fighters and abilities. Download a service account key from **Project settings > Service accounts** and keep it out of git. Then run:
   ```sh
   GOOGLE_APPLICATION_CREDENTIALS=/path/to/key.json npm run seed -- your-project-id
   ```
6. In Unity, set **Project Settings > Player > Android > Package Name**, for example `com.yourname.bubbafight`. In the Firebase console, add an Android app with that same package name and download `google-services.json` into `game/Assets/`. It is ignored by git.
7. Download the [Firebase Unity SDK](https://firebase.google.com/download/unity) and import `FirebaseAuth`, `FirebaseFirestore` and `FirebaseFunctions` (**Assets > Import Package > Custom Package**).
8. Add `FIREBASE_ENABLED` under **Project Settings > Player > Other Settings > Scripting Define Symbols**. The game now uses the real server.

Later, turn on [App Check](https://firebase.google.com/docs/app-check) with Play Integrity, then set `ENFORCE_APP_CHECK=true` in `firebase/functions/.env` and redeploy. Calls from modified copies of the app will then be rejected.

## Spending cap

A Google Cloud budget only emails you. It never stops charges. To turn billing
off automatically once spend reaches the budget:

1. In the Google Cloud console, go to **Billing > Budgets & alerts**. Edit your budget and, under **Manage notifications**, connect a new Pub/Sub topic named `billing-budget`.
2. In **IAM**, give the Cloud Functions service account the **Billing Account Administrator** role on your billing account.
3. Create `firebase/functions/.env` with `KILL_SWITCH_ENABLED=true` and redeploy.

When the cap trips, Cloud Functions stop working until you re-link billing in the console. Free (Spark) features keep working.

## Development

```sh
cd firebase/functions
npm test            # economy rules unit tests
npm run test:rules  # Firestore security rules tests (needs Java for the emulator)
npm run serve       # local emulators: Auth, Firestore, Functions
```

The C# scripts can be compile-checked without Unity: `dotnet build ci/UnityCompileCheck.csproj`.
Both checks run on every pull request.
